using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Order.Clients;
using PRM.Services.Order.Data;
using OrderModel = PRM.Services.Order.Models.Order;
using PRM.Services.Order.Models;
using PRM.Services.Order.Models.Enums;
using PRM.Services.Order.Notifications;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace PRM.Services.Order.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly INotificationService _notify;
    private readonly IRestaurantServiceClient _restaurant;

    public OrdersController(OrderDbContext context, INotificationService notify, IRestaurantServiceClient restaurant)
    {
        _context = context;
        _notify = notify;
        _restaurant = restaurant;
    }

    // --- DTOs ---
    public record OrderItemRequest(int MenuItemId, int Quantity, string? Note);
    public record PlaceOrderRequest(int TableId, List<OrderItemRequest> Items, string? Note);
    public record AddItemsRequest(List<OrderItemRequest> Items);
    public record UpdateStatusRequest(int Status);
    public record UpdateItemStatusRequest(OrderItemStatus Status);

    public record OrderItemResponse(
        int OrderItemId, int MenuItemId, string MenuItemName, int Quantity, decimal UnitPrice, string? Note,
        int Status, string StatusLabel);
    public record OrderResponse(
        int OrderId, int TableId, int Status, string StatusLabel,
        decimal TotalAmount, string? Note, DateTime CreatedAt, DateTime? UpdatedAt,
        List<OrderItemResponse> Items, string PublicToken, int? PaymentMethod, string? PaymentMethodLabel);
    public record OrderStatusResponse(int OrderId, int Status, string StatusLabel, bool IsPaid, DateTime? PaidAt);

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "Pending", 2 => "Confirmed", 3 => "Serving",
        4 => "Completed", 5 => "Cancelled", _ => "Unknown"
    };

    private static string GetPaymentMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Transfer => "Chuyển khoản",
        PaymentMethod.MoMo => "MoMo",
        PaymentMethod.VNPay => "VNPay",
        _ => "Chưa thanh toán"
    };

    internal static string GetItemStatusLabel(OrderItemStatus status) => status switch
    {
        OrderItemStatus.Pending => "Chờ chế biến",
        OrderItemStatus.Preparing => "Đang chế biến",
        OrderItemStatus.Ready => "Sẵn sàng",
        OrderItemStatus.Served => "Đã phục vụ",
        _ => "Unknown"
    };

    /// <summary>Build OrderResponse — gọi Restaurant Service để lấy MenuItemName.</summary>
    private async Task<OrderResponse> BuildResponse(OrderModel order, Dictionary<int, MenuItemInfo>? menuItemMap = null)
    {
        var dbItems = await _context.OrderItems
            .Where(oi => oi.OrderId == order.OrderId)
            .ToListAsync();

        // Nếu chưa có map, gọi Restaurant Service
        if (menuItemMap == null && dbItems.Any())
        {
            var ids = dbItems.Select(oi => oi.MenuItemId).Distinct();
            menuItemMap = await _restaurant.ValidateAndGetItemsAsync(ids);
        }

        var items = dbItems.Select(oi => new OrderItemResponse(
            oi.OrderItemId, oi.MenuItemId,
            menuItemMap?.GetValueOrDefault(oi.MenuItemId)?.Name ?? $"Item #{oi.MenuItemId}",
            oi.Quantity, oi.UnitPrice, oi.Note,
            (int)oi.Status, GetItemStatusLabel(oi.Status))).ToList();

        if (order.Payment == null)
        {
            await _context.Entry(order).Reference(o => o.Payment).LoadAsync();
        }

        int? paymentMethodValue = order.Payment != null ? (int)order.Payment.Method : null;
        string? paymentMethodText = order.Payment != null ? GetPaymentMethodLabel(order.Payment.Method) : null;

        return new OrderResponse(
            order.OrderId, order.TableId, order.Status,
            GetStatusLabel(order.Status), order.TotalAmount,
            order.Note, order.CreatedAt, order.UpdatedAt, items, order.PublicToken,
            paymentMethodValue, paymentMethodText);
    }

    /// <summary>Khách đặt món mới qua QR (Public).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("order-write")]
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> PlaceOrder([FromBody] PlaceOrderRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Order must have at least one item.");

        // Kiểm tra bàn qua Restaurant Service
        var tableStatus = await _restaurant.GetTableStatusAsync(request.TableId);
        if (tableStatus == null) return NotFound($"Table {request.TableId} not found.");

        // Validate và lấy thông tin items từ Restaurant Service
        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _restaurant.ValidateAndGetItemsAsync(menuItemIds);
        if (menuItems == null)
            return StatusCode(503, "Restaurant Service unavailable. Please try again.");

        foreach (var item in request.Items)
        {
            if (!menuItems.ContainsKey(item.MenuItemId))
                return BadRequest($"Menu item {item.MenuItemId} not found or not available.");
            if (item.Quantity <= 0)
                return BadRequest($"Quantity for item {item.MenuItemId} must be greater than 0.");
        }

        var totalAmount = request.Items.Sum(i => menuItems[i.MenuItemId].Price * i.Quantity);

        var order = new OrderModel
        {
            TableId = request.TableId,
            Status = 1, // Pending
            TotalAmount = totalAmount,
            Note = request.Note,
            CreatedAt = DateTime.UtcNow
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var orderItems = request.Items.Select(i => new OrderItem
        {
            OrderId = order.OrderId,
            MenuItemId = i.MenuItemId,
            Quantity = i.Quantity,
            UnitPrice = menuItems[i.MenuItemId].Price,
            Note = i.Note,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.OrderItems.AddRange(orderItems);
        await _context.SaveChangesAsync();

        // Đặt bàn sang Occupied qua Restaurant Service
        await _restaurant.OccupyTableAsync(request.TableId);

        var response = await BuildResponse(order, menuItems);
        await _notify.NotifyOrderCreatedAsync(response);

        return CreatedAtAction(nameof(GetById), new { id = order.OrderId }, response);
    }

    /// <summary>Khách gọi thêm món vào đơn đang mở (Public).</summary>
    [AllowAnonymous]
    [EnableRateLimiting("order-write")]
    [HttpPost("{id:int}/items")]
    public async Task<ActionResult<OrderResponse>> AddItems(int id, [FromQuery] string token, [FromBody] AddItemsRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Must provide at least one item.");

        var order = await _context.Orders.FindAsync(id);
        // Không phân biệt "không tồn tại" với "token sai" — tránh lộ thông tin orderId có tồn tại hay không.
        if (order == null || string.IsNullOrEmpty(token) || order.PublicToken != token) return NotFound($"Order {id} not found.");
        if (order.Status >= 4) return Conflict("Cannot add items to a completed or cancelled order.");

        var menuItemIds = request.Items.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _restaurant.ValidateAndGetItemsAsync(menuItemIds);
        if (menuItems == null) return StatusCode(503, "Restaurant Service unavailable.");

        foreach (var item in request.Items)
        {
            if (!menuItems.ContainsKey(item.MenuItemId))
                return BadRequest($"Menu item {item.MenuItemId} not found or not available.");
            if (item.Quantity <= 0)
                return BadRequest($"Quantity for item {item.MenuItemId} must be greater than 0.");
        }

        var newItems = request.Items.Select(i => new OrderItem
        {
            OrderId = id, MenuItemId = i.MenuItemId,
            Quantity = i.Quantity, UnitPrice = menuItems[i.MenuItemId].Price, Note = i.Note,
            CreatedAt = DateTime.UtcNow
        }).ToList();

        _context.OrderItems.AddRange(newItems);
        order.TotalAmount += request.Items.Sum(i => menuItems[i.MenuItemId].Price * i.Quantity);
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response = await BuildResponse(order, menuItems);
        await _notify.NotifyOrderUpdatedAsync(response);

        return Ok(response);
    }

    /// <summary>Cập nhật trạng thái chế biến của 1 món trong đơn (Staff/Bếp).</summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{orderId:int}/items/{itemId:int}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateItemStatus(int orderId, int itemId, [FromBody] UpdateItemStatusRequest request)
    {
        var order = await _context.Orders.FindAsync(orderId);
        if (order == null) return NotFound($"Order {orderId} not found.");
        if (order.Status >= 4) return Conflict("Cannot update item status on a completed or cancelled order.");

        var item = await _context.OrderItems.FirstOrDefaultAsync(oi => oi.OrderItemId == itemId && oi.OrderId == orderId);
        if (item == null) return NotFound($"Item {itemId} not found in order {orderId}.");

        item.Status = request.Status;
        await _context.SaveChangesAsync();

        var response = await BuildResponse(order);
        await _notify.NotifyOrderUpdatedAsync(response);

        return Ok(response);
    }
    /// <summary>Lấy tất cả đơn hàng, lọc theo status (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAll([FromQuery] int? status)
    {
        var query = _context.Orders.Include(o => o.Payment).AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status.Value);

        var orders = await query.OrderByDescending(o => o.CreatedAt).ToListAsync();
        var responses = new List<OrderResponse>();
        foreach (var o in orders) responses.Add(await BuildResponse(o));
        return Ok(responses);
    }

    /// <summary>Chi tiết 1 đơn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OrderResponse>> GetById(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");
        return Ok(await BuildResponse(order));
    }

    /// <summary>Lấy đơn đang active của 1 bàn (Public).</summary>
    [AllowAnonymous]
    [HttpGet("table/{tableId:int}")]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetByTable(int tableId)
    {
        var orders = await _context.Orders
            .Where(o => o.TableId == tableId && o.Status < 4)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync();

        var responses = new List<OrderResponse>();
        foreach (var o in orders) responses.Add(await BuildResponse(o));
        return Ok(responses);
    }

    /// <summary>Khách tự kiểm tra trạng thái đơn/thanh toán bằng orderId (Public). Dùng để poll sau khi thanh toán, vì GetByTable ẩn đơn khỏi kết quả ngay khi Completed.</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/status")]
    public async Task<ActionResult<OrderStatusResponse>> GetStatus(int id, [FromQuery] string token)
    {
        var order = await _context.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.OrderId == id);
        // Không phân biệt "không tồn tại" với "token sai" — tránh lộ thông tin orderId có tồn tại hay không.
        if (order == null || string.IsNullOrEmpty(token) || order.PublicToken != token) return NotFound($"Order {id} not found.");

        return Ok(new OrderStatusResponse(
            order.OrderId, order.Status, GetStatusLabel(order.Status),
            order.Payment?.Status == PaymentStatus.Paid, order.Payment?.PaidAt));
    }

    /// <summary>Cập nhật trạng thái đơn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (request.Status < 1 || request.Status > 5)
            return BadRequest("Status must be between 1 (Pending) and 5 (Cancelled).");

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");
        if (order.Status >= 4 && request.Status != order.Status)
            return Conflict("Cannot change status of a completed or cancelled order.");

        // Lấy accountId từ JWT token
        var accountIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst("sub");
        if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var accountId))
            order.HandledBy = accountId;

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        // Tự động tạo bản ghi thanh toán khi Đơn chuyển sang "Đang phục vụ" (Serving - 3)
        if (request.Status == 3)
        {
            var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == id);
            if (existingPayment == null)
            {
                var payment = new Payment
                {
                    OrderId = id,
                    Amount = order.TotalAmount,
                    Method = PaymentMethod.Transfer,
                    Status = PaymentStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
        }

        // Tự động cập nhật hoặc tạo thanh toán Tiền mặt khi xác nhận hoàn thành đơn hàng thủ công (Status = 4)
        if (request.Status == 4)
        {
            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == id);
            if (payment == null)
            {
                payment = new Payment
                {
                    OrderId = id,
                    Amount = order.TotalAmount,
                    Method = PaymentMethod.Cash,
                    Status = PaymentStatus.Paid,
                    PaidAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow
                };
                _context.Payments.Add(payment);
            }
            else
            {
                payment.Status = PaymentStatus.Paid;
                payment.PaidAt = DateTime.UtcNow;
                payment.Method = PaymentMethod.Cash;
            }
        }

        // Nếu đơn hoàn thành hoặc hủy, kiểm tra bàn còn đơn active không
        if (request.Status is 4 or 5)
        {
            var hasOtherActive = await _context.Orders
                .AnyAsync(o => o.TableId == order.TableId && o.OrderId != id && o.Status < 4);

            if (!hasOtherActive)
                await _restaurant.ReleaseTableAsync(order.TableId);

            // Tự động cập nhật các OrderItems sang trạng thái Served (4) hoặc Cancelled (5)
            // (Lưu ý: theo OrderItemStatus, Served=4)
            var orderItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
            foreach (var item in orderItems)
            {
                if (request.Status == 4) item.Status = OrderItemStatus.Served; // Served
                if (request.Status == 5) item.Status = OrderItemStatus.Pending; 
            }
        }

        await _context.SaveChangesAsync();

        var response = await BuildResponse(order);
        await _notify.NotifyOrderStatusUpdatedAsync(response);

        return Ok(response);
    }



    /// <summary>Xuất hóa đơn dạng HTML cho đơn hàng đã hoàn thành (Public).</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/invoice")]
    public async Task<IActionResult> GetInvoice(int id, [FromQuery] string token)
    {
        var order = await _context.Orders.Include(o => o.Payment).FirstOrDefaultAsync(o => o.OrderId == id);
        if (order == null || string.IsNullOrEmpty(token) || order.PublicToken != token) return NotFound($"Order {id} not found.");

        var dbItems = await _context.OrderItems.Where(oi => oi.OrderId == id).ToListAsync();
        var ids = dbItems.Select(oi => oi.MenuItemId).Distinct();
        var menuItems = await _restaurant.ValidateAndGetItemsAsync(ids);

        var itemsHtml = string.Join("", dbItems.Select(oi => $@"
            <tr>
                <td style='padding: 8px; border-bottom: 1px solid #ddd;'>{(menuItems?.GetValueOrDefault(oi.MenuItemId)?.Name ?? $"Món #{oi.MenuItemId}")}</td>
                <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: center;'>{oi.Quantity}</td>
                <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{oi.UnitPrice:N0}đ</td>
                <td style='padding: 8px; border-bottom: 1px solid #ddd; text-align: right;'>{(oi.UnitPrice * oi.Quantity):N0}đ</td>
            </tr>"));

        var paymentMethodText = "Chưa thanh toán";
        if (order.Payment != null)
        {
            paymentMethodText = order.Payment.Method switch
            {
                PaymentMethod.Cash => "Tiền mặt",
                PaymentMethod.Transfer => "Chuyển khoản (Sepay)",
                PaymentMethod.MoMo => "MoMo",
                PaymentMethod.VNPay => "VNPay",
                _ => "Khác"
            };
        }

        var html = $@"
        <!DOCTYPE html>
        <html>
        <head>
            <meta charset='utf-8' />
            <title>Hóa đơn #{order.OrderId}</title>
            <style>
                body {{ font-family: 'Segoe UI', Arial, sans-serif; margin: 20px; color: #33221A; background-color: #FAF7F5; }}
                .invoice-box {{ max-width: 400px; margin: auto; padding: 24px; border: 1px solid #E6DFD9; background: #fff; border-radius: 12px; box-shadow: 0 4px 10px rgba(0,0,0,0.05); }}
                .header {{ text-align: center; margin-bottom: 20px; }}
                .header h2 {{ margin: 0; color: #6F4E37; }}
                .header p {{ margin: 5px 0 0 0; font-size: 12px; color: #8B7365; }}
                .info {{ font-size: 13px; margin-bottom: 15px; border-bottom: 2px dashed #E6DFD9; padding-bottom: 10px; }}
                .info p {{ margin: 4px 0; }}
                table {{ width: 100%; border-collapse: collapse; font-size: 13px; margin-bottom: 20px; }}
                th {{ background: #F5EBE6; color: #6F4E37; padding: 8px; text-align: left; }}
                .total {{ text-align: right; font-size: 16px; font-weight: bold; color: #6F4E37; border-top: 2px dashed #E6DFD9; padding-top: 10px; }}
                .footer {{ text-align: center; margin-top: 30px; font-size: 11px; color: #8B7365; }}
                .print-btn {{ display: block; width: 100%; padding: 10px; background: #6F4E37; color: white; border: none; border-radius: 8px; font-weight: bold; cursor: pointer; text-align: center; text-decoration: none; margin-top: 15px; font-size: 13px; }}
                @media print {{ .print-btn {{ display: none; }} body {{ background: #fff; margin: 0; }} .invoice-box {{ border: none; box-shadow: none; padding: 0; }} }}
            </style>
        </head>
        <body>
            <div class='invoice-box'>
                <div class='header'>
                    <h2>AROMA BISTRO</h2>
                    <p>Hóa Đơn Thanh Toán</p>
                </div>
                <div class='info'>
                    <p><b>Mã đơn hàng:</b> #{order.OrderId}</p>
                    <p><b>Bàn phục vụ:</b> Bàn #{order.TableId}</p>
                    <p><b>Thời gian:</b> {order.CreatedAt.ToLocalTime():dd/MM/yyyy HH:mm}</p>
                    <p><b>Phương thức:</b> {paymentMethodText}</p>
                    <p><b>Trạng thái:</b> {(order.Status == 4 ? "Đã thanh toán" : "Chờ thanh toán")}</p>
                </div>
                <table>
                    <thead>
                        <tr>
                            <th style='padding: 8px;'>Tên món</th>
                            <th style='padding: 8px; text-align: center;'>SL</th>
                            <th style='padding: 8px; text-align: right;'>Đơn giá</th>
                            <th style='padding: 8px; text-align: right;'>Thành tiền</th>
                        </tr>
                    </thead>
                    <tbody>
                        {itemsHtml}
                    </tbody>
                </table>
                <div class='total'>
                    Tổng thanh toán: {order.TotalAmount:N0}đ
                </div>
                <button class='print-btn' onclick='window.print()'>IN HÓA ĐƠN</button>
                <div class='footer'>
                    Cảm ơn quý khách. Hẹn gặp lại!
                </div>
            </div>
        </body>
        </html>";

        return Content(html, "text/html", System.Text.Encoding.UTF8);
    }
}
