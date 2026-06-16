namespace PRM.Services.Order.Models;

public class Order
{
    public int OrderId { get; set; }
    public int TableId { get; set; }       // Logical reference - no FK to Restaurant DB
    public int? HandledBy { get; set; }    // Logical reference - no FK to Identity DB
    public int Status { get; set; }        // 1=Pending, 2=Confirmed, 3=Serving, 4=Completed, 5=Cancelled
    public decimal TotalAmount { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    // Internal navigation (same DB)
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public virtual Payment? Payment { get; set; }
    public virtual ICollection<Feedback> Feedbacks { get; set; } = new List<Feedback>();
}
