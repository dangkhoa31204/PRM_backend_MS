namespace PRM.Services.Order.Models;

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }    // Logical reference - no FK to Restaurant DB
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }
    public int Status { get; set; } = 1; // 1=Pending, 2=Preparing, 3=Ready, 4=Served

    // Internal navigation (same DB)
    public virtual Order Order { get; set; } = null!;
}
