using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Order.Clients;
using PRM.Services.Order.Data;
using PRM.Services.Order.Models;
using PRM.Services.Order.Models.Enums;
using PRM.Services.Order.Notifications;
using System.Text.RegularExpressions;

namespace PRM.Services.Order.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly INotificationService _notify;
    private readonly IRestaurantServiceClient _restaurant;

    public PaymentsController(
        OrderDbContext context,
        INotificationService notify,
        IRestaurantServiceClient restaurant)
    {
        _context = context;
        _notify = notify;
        _restaurant = restaurant;
    }

    // --- DTOs ---
    public record CreatePaymentRequest(int OrderId, PaymentMethod Method);
    public record UpdatePaymentStatusRequest(PaymentStatus Status);
    public record PaymentResponse(
        int PaymentId, int OrderId, decimal Amount,
        PaymentMethod Method, string MethodLabel,
        PaymentStatus Status, string StatusLabel,
        DateTime? PaidAt, DateTime CreatedAt);

    private static string GetMethodLabel(PaymentMethod m) => m switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Transfer => "Chuyển khoản",
        PaymentMethod.MoMo => "MoMo",
        PaymentMethod.VNPay => "VNPay",
        _ => "Unknown"
    };

    private static string GetStatusLabel(PaymentStatus s) => s switch
    {
        PaymentStatus.Pending => "Chờ thanh toán",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Refunded => "Đã hoàn tiền",
        _ => "Unknown"
    };

    private static PaymentResponse ToResponse(Payment p) =>
        new(p.PaymentId, p.OrderId, p.Amount,
            p.Method, GetMethodLabel(p.Method),
            p.Status, GetStatusLabel(p.Status),
            p.PaidAt, p.CreatedAt);

    /// <summary>Tạo payment cho order (Staff/Admin). Mỗi order chỉ có 1 payment.</summary>
    [Authorize(Roles = "1,2")]
    [HttpPost]
    public async Task<ActionResult<PaymentResponse>> Create([FromBody] CreatePaymentRequest request)
    {
        var order = await _context.Orders.FindAsync(request.OrderId);
        if (order == null) return NotFound($"Order {request.OrderId} not found.");
        if (order.Status == 5) return Conflict("Cannot create payment for a cancelled order.");

        // Kiểm tra payment đã tồn tại chưa
        var existingPayment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == request.OrderId);
        if (existingPayment != null) return Conflict($"Payment for order {request.OrderId} already exists.");

        var payment = new Payment
        {
            OrderId = request.OrderId,
            Amount = order.TotalAmount,
            Method = request.Method,
            Status = PaymentStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Payments.Add(payment);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetByOrder), new { orderId = request.OrderId }, ToResponse(payment));
    }

    /// <summary>Lấy payment của 1 đơn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("order/{orderId:int}")]
    public async Task<ActionResult<PaymentResponse>> GetByOrder(int orderId)
    {
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        if (payment == null) return NotFound($"No payment found for order {orderId}.");
        return Ok(ToResponse(payment));
    }

    /// <summary>Lấy payment theo id (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PaymentResponse>> GetById(int id)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return NotFound($"Payment {id} not found.");
        return Ok(ToResponse(payment));
    }

    /// <summary>Cập nhật trạng thái thanh toán (Staff/Admin). Paid → ghi PaidAt.</summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<PaymentResponse>> UpdateStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
    {
        var payment = await _context.Payments.FindAsync(id);
        if (payment == null) return NotFound($"Payment {id} not found.");

        payment.Status = request.Status;
        if (request.Status == PaymentStatus.Paid)
            payment.PaidAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(payment));
    }

    /// <summary>Lấy tất cả payments (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<PaymentResponse>>> GetAll([FromQuery] PaymentStatus? status)
    {
        var query = _context.Payments.AsQueryable();
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);
        var payments = await query.OrderByDescending(p => p.CreatedAt).Select(p => ToResponse(p)).ToListAsync();
        return Ok(payments);
    }

    public class SepayWebhookPayload
    {
        [JsonPropertyName("id")]
        [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
        public long Id { get; set; }

        [JsonPropertyName("gateway")]
        public string Gateway { get; set; } = string.Empty;

        [JsonPropertyName("transactionDate")]
        public string TransactionDate { get; set; } = string.Empty;

        [JsonPropertyName("accountNumber")]
        public string AccountNumber { get; set; } = string.Empty;

        [JsonPropertyName("transferType")]
        public string TransferType { get; set; } = string.Empty; // "in" or "out"

        [JsonPropertyName("transferAmount")]
        [System.Text.Json.Serialization.JsonNumberHandling(System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString)]
        public decimal TransferAmount { get; set; }

        [JsonPropertyName("transactionContent")]

        public string TransactionContent { get; set; } = string.Empty;

        [JsonPropertyName("content")]
        public string SepayContent { get; set; } = string.Empty; // Trường thực tế Sepay gửi chứa nội dung chuyển khoản

        [JsonPropertyName("referenceCode")] // Map từ referenceCode của Sepay
        public string ReferenceNumber { get; set; } = string.Empty;
    }

    private static readonly System.Collections.Concurrent.ConcurrentQueue<object> _webhookLogs = new System.Collections.Concurrent.ConcurrentQueue<object>();

    [AllowAnonymous]
    [HttpGet("webhook-logs")]
    public IActionResult GetWebhookLogs()
    {
        return Ok(_webhookLogs.ToArray());
    }

    /// <summary>Webhook nhận thông báo thanh toán tự động từ Sepay (Public).</summary>
    [AllowAnonymous]
    [HttpPost("sepay-webhook")]
    public async Task<IActionResult> SepayWebhook([FromBody] JsonElement rawPayloadElement)
    {
        // Log raw payload
        string rawJson = rawPayloadElement.GetRawText();
        Console.WriteLine($"[SepayWebhook] Raw Payload JSON: {rawJson}");
        
        _webhookLogs.Enqueue(new { Time = DateTime.UtcNow, Payload = rawJson });
        if (_webhookLogs.Count > 50) _webhookLogs.TryDequeue(out _);

        SepayWebhookPayload payload;
        try
        {
            payload = System.Text.Json.JsonSerializer.Deserialize<SepayWebhookPayload>(rawJson);
        }
        catch (Exception ex)
        {
            _webhookLogs.Enqueue(new { Time = DateTime.UtcNow, Error = "Deserialize failed", Message = ex.Message });
            return Ok(new { success = false, message = "Deserialize failed: " + ex.Message });
        }

        if (payload == null)
            return Ok(new { success = false, message = "Payload is null" });

        // Log nhận thông tin giao dịch raw để dễ debug
        Console.WriteLine($"[SepayWebhook] Raw Payload JSON: {System.Text.Json.JsonSerializer.Serialize(payload)}");

        // Ưu tiên đọc từ Content (Sepay chuyển khoản thật) rồi mới tới TransactionContent (nút Test giả lập)
        string transactionContent = !string.IsNullOrEmpty(payload.SepayContent) 
            ? payload.SepayContent 
            : payload.TransactionContent;

        // Log nhận thông tin giao dịch để dễ debug
        Console.WriteLine($"[SepayWebhook] Nhận giao dịch: {payload.ReferenceNumber}, Số tiền: {payload.TransferAmount}, Nội dung: {transactionContent}");

        // Chỉ chấp nhận giao dịch tiền vào
        if (!string.Equals(payload.TransferType, "in", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new { success = false, message = "Not an 'in' transfer type." });
        }

        // Regex tìm mã đơn hàng dạng AROMA<id>, AROMA_<id>, hoặc AROMA <id> (Không phân biệt chữ hoa thường)
        var match = System.Text.RegularExpressions.Regex.Match(transactionContent, @"AROMA[\s_]*(\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            Console.WriteLine($"[SepayWebhook] Không tìm thấy mã đơn hàng AROMA trong nội dung: {transactionContent}");
            return Ok(new { success = false, message = "Could not parse OrderId from transaction content." });
        }

        int orderId = int.Parse(match.Groups[1].Value);

        var order = await _context.Orders.FindAsync(orderId);
        if (order == null)
        {
            return Ok(new { success = false, message = $"Order {orderId} not found." });
        }

        // Kiểm tra đơn hàng đã hoàn thành hoặc hủy chưa
        if (order.Status is 4 or 5)
        {
            // Vẫn phát SignalR để client đồng bộ lại nếu chưa đóng dialog
            try
            {
                var dbItems = await _context.OrderItems.Where(oi => oi.OrderId == orderId).ToListAsync();
                var ids = dbItems.Select(oi => oi.MenuItemId).Distinct();
                var menuItems = await _restaurant.ValidateAndGetItemsAsync(ids);
                
                var items = dbItems.Select(oi => new OrdersController.OrderItemResponse(
                    oi.OrderItemId, oi.MenuItemId,
                    menuItems?.GetValueOrDefault(oi.MenuItemId)?.Name ?? $"Item #{oi.MenuItemId}",
                    oi.Quantity, oi.UnitPrice, oi.Note)).ToList();

                var orderResponse = new OrdersController.OrderResponse(
                    order.OrderId, order.TableId, order.Status,
                    order.Status == 4 ? "Completed" : "Cancelled", order.TotalAmount, order.Note, order.CreatedAt, order.UpdatedAt, items);

                await _notify.NotifyOrderStatusUpdatedAsync(orderResponse);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SepayWebhook] Lỗi gửi SignalR đồng bộ: {ex.Message}");
            }

            return Ok(new { success = true, message = $"Order {orderId} is already completed or cancelled." });
        }

        // Xác thực số tiền (Sepay gửi TransferAmount, đơn hàng lưu TotalAmount)
        if (payload.TransferAmount < order.TotalAmount)
        {
            Console.WriteLine($"[SepayWebhook] Số tiền không khớp. Nhận: {payload.TransferAmount}, Yêu cầu: {order.TotalAmount}");
            return Ok(new { success = false, message = $"Amount mismatch. Received: {payload.TransferAmount}, Required: {order.TotalAmount}" });
        }

        // Cập nhật hoặc tạo mới bản ghi Payment
        var payment = await _context.Payments.FirstOrDefaultAsync(p => p.OrderId == orderId);
        if (payment == null)
        {
            payment = new Payment
            {
                OrderId = orderId,
                Amount = order.TotalAmount,
                Method = PaymentMethod.Transfer,
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
            payment.Method = PaymentMethod.Transfer; // Đảm bảo ghi nhận là chuyển khoản
        }

        // Cập nhật trạng thái Order sang hoàn thành (Status = 4)
        order.Status = 4;
        order.UpdatedAt = DateTime.UtcNow;

        // Giải phóng bàn ăn nếu bàn không còn đơn nào khác đang hoạt động
        var hasOtherActive = await _context.Orders
            .AnyAsync(o => o.TableId == order.TableId && o.OrderId != orderId && o.Status < 4);

        if (!hasOtherActive)
        {
            try
            {
                await _restaurant.ReleaseTableAsync(order.TableId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SepayWebhook] Lỗi giải phóng bàn ăn {order.TableId}: {ex.Message}");
            }
        }

        await _context.SaveChangesAsync();

        // Phát SignalR thông báo thay đổi trạng thái đơn
        try
        {
            // Build Response để gửi qua SignalR
            var dbItems = await _context.OrderItems.Where(oi => oi.OrderId == orderId).ToListAsync();
            var ids = dbItems.Select(oi => oi.MenuItemId).Distinct();
            var menuItems = await _restaurant.ValidateAndGetItemsAsync(ids);
            
            var items = dbItems.Select(oi => new OrdersController.OrderItemResponse(
                oi.OrderItemId, oi.MenuItemId,
                menuItems?.GetValueOrDefault(oi.MenuItemId)?.Name ?? $"Item #{oi.MenuItemId}",
                oi.Quantity, oi.UnitPrice, oi.Note)).ToList();

            var orderResponse = new OrdersController.OrderResponse(
                order.OrderId, order.TableId, order.Status,
                "Completed", order.TotalAmount, order.Note, order.CreatedAt, order.UpdatedAt, items);

            await _notify.NotifyOrderStatusUpdatedAsync(orderResponse);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SepayWebhook] Lỗi gửi SignalR: {ex.Message}");
        }

        return Ok(new { success = true, message = $"Order {orderId} processed successfully." });
    }
}
