using PRM.Shared.Enums;

namespace PRM.Services.Restaurant.Models;

public class MenuItem
{
    public int MenuItemId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public MenuCategory Category { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
