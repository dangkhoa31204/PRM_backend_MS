using PRM.Services.Order.Models.Enums;

namespace PRM.Services.Order.Models;

public class Payment
{
    public int PaymentId { get; set; }
    public int OrderId { get; set; }       // FK vật lý - cùng DB
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // Internal navigation (same DB)
    public virtual Order Order { get; set; } = null!;
}
