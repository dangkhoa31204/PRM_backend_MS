using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using PRM.Services.Order.Data;
using PRM.Services.Order.Models;
using PRM.Services.Order.Hubs;

namespace PRM.Services.Order.Controllers;

[ApiController]
[Route("api/feedbacks")]
public class FeedbacksController : ControllerBase
{
    private readonly OrderDbContext _context;
    private readonly IHubContext<FeedbackHub> _feedbackHub;

    public FeedbacksController(OrderDbContext context, IHubContext<FeedbackHub> feedbackHub)
    {
        _context = context;
        _feedbackHub = feedbackHub;
    }

    // --- DTOs ---
    public record CreateFeedbackRequest(int OrderId, int TableId, int Rating, string? Comment);
    public record FeedbackResponse(
        int FeedbackId, int OrderId, int TableId,
        int Rating, string? Comment, bool IsHidden, DateTime CreatedAt);

    private static FeedbackResponse ToResponse(Feedback f) =>
        new(f.FeedbackId, f.OrderId, f.TableId, f.Rating, f.Comment, f.IsHidden, f.CreatedAt);

    /// <summary>Lấy danh sách feedback active (Public cho khách xem).</summary>
    [AllowAnonymous]
    [HttpGet("active")]
    public async Task<ActionResult<IEnumerable<FeedbackResponse>>> GetActive()
    {
        var feedbacks = await _context.Feedbacks
            .Where(f => !f.IsHidden)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => ToResponse(f))
            .ToListAsync();
        return Ok(feedbacks);
    }

    /// <summary>
    /// Khách gửi feedback ẩn danh (Public).
    /// Anti-spam: chỉ gửi được nếu order Completed, trong 24h, và chưa có feedback.
    /// </summary>
    [AllowAnonymous]
    [HttpPost]
    public async Task<ActionResult<FeedbackResponse>> Create([FromBody] CreateFeedbackRequest request)
    {
        if (request.Rating < 1 || request.Rating > 5)
            return BadRequest("Rating must be between 1 and 5.");

        // Kiểm tra order tồn tại và đã hoàn thành
        var order = await _context.Orders.FindAsync(request.OrderId);
        if (order == null) return NotFound($"Order {request.OrderId} not found.");
        if (order.Status != 4) return Conflict("Feedback can only be submitted for completed orders.");

        // [Anti-spam] Kiểm tra 24h kể từ khi order hoàn thành
        var completedAt = order.UpdatedAt ?? order.CreatedAt;
        if (DateTime.UtcNow - completedAt > TimeSpan.FromHours(24))
            return Conflict("Feedback window has expired (24 hours after order completion).");

        // [Anti-spam] Kiểm tra unique: 1 feedback duy nhất cho mỗi order
        var alreadyExists = await _context.Feedbacks.AnyAsync(f => f.OrderId == request.OrderId);
        if (alreadyExists)
            return Conflict($"Feedback for order {request.OrderId} has already been submitted.");

        var feedback = new Feedback
        {
            OrderId = request.OrderId,
            TableId = request.TableId,
            Rating = request.Rating,
            Comment = request.Comment,
            IsHidden = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Feedbacks.Add(feedback);
        await _context.SaveChangesAsync();

        var response = ToResponse(feedback);
        // Broadcast real-time
        await _feedbackHub.Clients.All.SendAsync("FeedbackCreated", response);

        return CreatedAtAction(nameof(GetByOrder), new { orderId = request.OrderId }, response);
    }

    /// <summary>Lấy feedback của 1 đơn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("order/{orderId:int}")]
    public async Task<ActionResult<FeedbackResponse>> GetByOrder(int orderId)
    {
        var feedback = await _context.Feedbacks.FirstOrDefaultAsync(f => f.OrderId == orderId);
        if (feedback == null) return NotFound($"No feedback for order {orderId}.");
        return Ok(ToResponse(feedback));
    }

    /// <summary>Lấy tất cả feedback của 1 bàn (Staff/Admin).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("table/{tableId:int}")]
    public async Task<ActionResult<IEnumerable<FeedbackResponse>>> GetByTable(int tableId)
    {
        var feedbacks = await _context.Feedbacks
            .Where(f => f.TableId == tableId)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => ToResponse(f))
            .ToListAsync();
        return Ok(feedbacks);
    }

    /// <summary>Lấy toàn bộ feedback, lọc theo rating và isHidden (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FeedbackResponse>>> GetAll(
        [FromQuery] int? rating, [FromQuery] bool? isHidden)
    {
        var query = _context.Feedbacks.AsQueryable();
        if (rating.HasValue) query = query.Where(f => f.Rating == rating.Value);
        if (isHidden.HasValue) query = query.Where(f => f.IsHidden == isHidden.Value);

        var feedbacks = await query
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => ToResponse(f))
            .ToListAsync();
        return Ok(feedbacks);
    }

    /// <summary>
    /// Admin ẩn/hiện feedback để kiểm soát spam (Admin only).
    /// PATCH /api/feedbacks/{id}/visibility
    /// </summary>
    [Authorize(Roles = "1")]
    [HttpPatch("{id:int}/visibility")]
    public async Task<ActionResult<FeedbackResponse>> ToggleVisibility(int id)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null) return NotFound($"Feedback {id} not found.");

        feedback.IsHidden = !feedback.IsHidden;
        await _context.SaveChangesAsync();

        var response = ToResponse(feedback);
        // Broadcast real-time visibility update
        await _feedbackHub.Clients.All.SendAsync("FeedbackVisibilityChanged", response);

        return Ok(response);
    }
}
