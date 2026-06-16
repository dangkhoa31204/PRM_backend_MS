namespace PRM.Services.Order.Models;

public class OrderItem
{
    public int OrderItemId { get; set; }
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }    // Logical reference - no FK to Restaurant DB
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Note { get; set; }

    // Internal navigation (same DB)
    public virtual Order Order { get; set; } = null!;
}
