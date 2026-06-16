namespace PRM.Services.Order.Models;

public class Feedback
{
    public int FeedbackId { get; set; }
    public int OrderId { get; set; }       // Logical reference (FK vật lý - cùng DB)
    public int TableId { get; set; }       // Logical reference - no FK to Restaurant DB
    public int Rating { get; set; }        // 1-5 sao
    public string? Comment { get; set; }
    public bool IsHidden { get; set; }     // Admin ẩn/hiện để kiểm soát spam
    public DateTime CreatedAt { get; set; }

    // Internal navigation (same DB - for checking order status)
    public virtual Order Order { get; set; } = null!;
}
