using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Order.Clients;
using PRM.Services.Order.Data;
using OrderModel = PRM.Services.Order.Models.Order;
using PRM.Services.Order.Models;
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

    public record OrderItemResponse(int OrderItemId, int MenuItemId, string MenuItemName, int Quantity, decimal UnitPrice, string? Note);
    public record OrderResponse(
        int OrderId, int TableId, int Status, string StatusLabel,
        decimal TotalAmount, string? Note, DateTime CreatedAt, DateTime? UpdatedAt,
        List<OrderItemResponse> Items);

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "Pending", 2 => "Confirmed", 3 => "Serving",
        4 => "Completed", 5 => "Cancelled", _ => "Unknown"
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
            oi.Quantity, oi.UnitPrice, oi.Note)).ToList();

        return new OrderResponse(
            order.OrderId, order.TableId, order.Status,
            GetStatusLabel(order.Status), order.TotalAmount,
            order.Note, order.CreatedAt, order.UpdatedAt, items);
    }

    /// <summary>Khách đặt món mới qua QR (Public).</summary>
    [AllowAnonymous]
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
            Note = i.Note
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
    [HttpPost("{id:int}/items")]
    public async Task<ActionResult<OrderResponse>> AddItems(int id, [FromBody] AddItemsRequest request)
    {
        if (request.Items == null || request.Items.Count == 0)
            return BadRequest("Must provide at least one item.");

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");
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
            Quantity = i.Quantity, UnitPrice = menuItems[i.MenuItemId].Price, Note = i.Note
        }).ToList();

        _context.OrderItems.AddRange(newItems);
        order.TotalAmount += request.Items.Sum(i => menuItems[i.MenuItemId].Price * i.Quantity);
        order.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        var response = await BuildResponse(order, menuItems);
        await _notify.NotifyOrderUpdatedAsync(response);

        return Ok(response);
    }

    /// <summary>Lấy tất cả đơn hàng, lọc theo status (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OrderResponse>>> GetAll([FromQuery] int? status)
    {
        var query = _context.Orders.AsQueryable();
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

    /// <summary>Cập nhật trạng thái đơn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<OrderResponse>> UpdateStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (request.Status < 1 || request.Status > 5)
            return BadRequest("Status must be between 1 (Pending) and 5 (Cancelled).");

        var order = await _context.Orders.FindAsync(id);
        if (order == null) return NotFound($"Order {id} not found.");

        // Lấy accountId từ JWT token
        var accountIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst("sub");
        if (accountIdClaim != null && int.TryParse(accountIdClaim.Value, out var accountId))
            order.HandledBy = accountId;

        order.Status = request.Status;
        order.UpdatedAt = DateTime.UtcNow;

        // Nếu đơn hoàn thành hoặc hủy, kiểm tra bàn còn đơn active không
        if (request.Status is 4 or 5)
        {
            var hasOtherActive = await _context.Orders
                .AnyAsync(o => o.TableId == order.TableId && o.OrderId != id && o.Status < 4);

            if (!hasOtherActive)
                await _restaurant.ReleaseTableAsync(order.TableId);
        }

        await _context.SaveChangesAsync();

        var response = await BuildResponse(order);
        await _notify.NotifyOrderStatusUpdatedAsync(response);

        return Ok(response);
    }
}
