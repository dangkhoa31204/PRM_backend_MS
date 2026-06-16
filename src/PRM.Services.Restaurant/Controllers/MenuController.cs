using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Restaurant.Data;
using PRM.Services.Restaurant.Models;
using PRM.Shared.Enums;

namespace PRM.Services.Restaurant.Controllers;

[ApiController]
[Route("api/menu")]
public class MenuController : ControllerBase
{
    private readonly RestaurantDbContext _context;

    public MenuController(RestaurantDbContext context) => _context = context;

    // --- DTOs ---
    public record MenuItemResponse(
        int MenuItemId, string Name, string? Description,
        decimal Price, MenuCategory Category, string? ImageUrl,
        bool IsAvailable, DateTime CreatedAt, DateTime? UpdatedAt);

    public record CreateMenuItemRequest(string Name, string? Description, decimal Price, MenuCategory Category, string? ImageUrl);
    public record UpdateMenuItemRequest(string Name, string? Description, decimal Price, MenuCategory Category, string? ImageUrl, bool IsAvailable);

    private static MenuItemResponse ToResponse(MenuItem m) =>
        new(m.MenuItemId, m.Name, m.Description, m.Price, m.Category, m.ImageUrl, m.IsAvailable, m.CreatedAt, m.UpdatedAt);

    /// <summary>Lấy danh sách món đang hiển thị (Public).</summary>
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<MenuItemResponse>>> GetAvailable()
    {
        var items = await _context.MenuItems
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => ToResponse(m))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Lấy tất cả món kể cả ẩn (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<MenuItemResponse>>> GetAll()
    {
        var items = await _context.MenuItems
            .OrderBy(m => m.Category).ThenBy(m => m.Name)
            .Select(m => ToResponse(m))
            .ToListAsync();
        return Ok(items);
    }

    /// <summary>Chi tiết 1 món (Public).</summary>
    [AllowAnonymous]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<MenuItemResponse>> GetById(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");
        return Ok(ToResponse(item));
    }

    /// <summary>
    /// [Internal] Validate danh sách MenuItemId và trả về thông tin giá/tên.
    /// Order Service dùng endpoint này để kiểm tra items trước khi tạo đơn.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("validate-items")]
    public async Task<ActionResult<Dictionary<int, MenuItemResponse>>> ValidateItems([FromQuery] string ids)
    {
        if (string.IsNullOrWhiteSpace(ids)) return BadRequest("ids is required.");

        var idList = ids.Split(',')
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : -1)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var items = await _context.MenuItems
            .Where(m => idList.Contains(m.MenuItemId) && m.IsAvailable)
            .ToListAsync();

        var result = items.ToDictionary(m => m.MenuItemId, ToResponse);
        return Ok(result);
    }

    /// <summary>Tạo món mới (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpPost]
    public async Task<ActionResult<MenuItemResponse>> Create([FromBody] CreateMenuItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        if (request.Price <= 0) return BadRequest("Price must be greater than 0.");

        var item = new MenuItem
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            Category = request.Category,
            ImageUrl = request.ImageUrl,
            IsAvailable = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.MenuItems.Add(item);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = item.MenuItemId }, ToResponse(item));
    }

    /// <summary>Cập nhật món ăn (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<MenuItemResponse>> Update(int id, [FromBody] UpdateMenuItemRequest request)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");
        if (string.IsNullOrWhiteSpace(request.Name)) return BadRequest("Name is required.");
        if (request.Price <= 0) return BadRequest("Price must be greater than 0.");

        item.Name = request.Name;
        item.Description = request.Description;
        item.Price = request.Price;
        item.Category = request.Category;
        item.ImageUrl = request.ImageUrl;
        item.IsAvailable = request.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(item));
    }

    /// <summary>Xóa món khỏi menu (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");

        _context.MenuItems.Remove(item);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Ẩn/hiện món (Admin hoặc Staff).</summary>
    [Authorize(Roles = "1,2")]
    [HttpPatch("{id:int}/toggle-availability")]
    public async Task<ActionResult<MenuItemResponse>> ToggleAvailability(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null) return NotFound($"Menu item {id} not found.");

        item.IsAvailable = !item.IsAvailable;
        item.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToResponse(item));
    }
}
