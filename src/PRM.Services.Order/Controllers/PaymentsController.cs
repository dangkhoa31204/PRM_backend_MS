using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Order.Data;
using PRM.Services.Order.Models;
using PRM.Services.Order.Models.Enums;

namespace PRM.Services.Order.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly OrderDbContext _context;

    public PaymentsController(OrderDbContext context) => _context = context;

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
}
