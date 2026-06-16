using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRM.Services.Restaurant.Data;
using PRM.Services.Restaurant.Models;
using QRCoder;

namespace PRM.Services.Restaurant.Controllers;

[ApiController]
[Route("api/tables")]
public class TablesController : ControllerBase
{
    private readonly RestaurantDbContext _context;
    private readonly IConfiguration _configuration;

    public TablesController(RestaurantDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // --- DTOs ---
    public record TableResponse(int TableId, int Capacity, int Status, string StatusLabel, DateTime CreatedAt);
    public record CreateTableRequest(int Capacity);
    public record UpdateTableRequest(int Capacity, int Status);

    private static string GetStatusLabel(int status) => status switch
    {
        1 => "Available",
        2 => "Occupied",
        3 => "Reserved",
        _ => "Unknown"
    };

    private static TableResponse ToResponse(Table t) =>
        new(t.TableId, t.Capacity, t.Status, GetStatusLabel(t.Status), t.CreatedAt);

    /// <summary>Lấy danh sách tất cả bàn (Admin/Staff).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TableResponse>>> GetAll()
    {
        var tables = await _context.Tables
            .OrderBy(t => t.TableId)
            .Select(t => ToResponse(t))
            .ToListAsync();
        return Ok(tables);
    }

    /// <summary>Chi tiết 1 bàn (Admin/Staff).</summary>
    [Authorize(Roles = "1,2")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<TableResponse>> GetById(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");
        return Ok(ToResponse(table));
    }

    /// <summary>
    /// [Internal] Lấy trạng thái bàn (int). Order Service dùng endpoint này để kiểm tra.
    /// </summary>
    [AllowAnonymous]
    [HttpGet("{id:int}/status")]
    public async Task<ActionResult<int>> GetStatus(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");
        return Ok(table.Status);
    }

    /// <summary>
    /// [Internal] Giải phóng bàn về Available. Order Service gọi khi đơn hoàn thành/hủy.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id:int}/release")]
    public async Task<IActionResult> ReleaseTable(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        table.Status = 1; // Available
        await _context.SaveChangesAsync();
        return Ok(new { tableId = id, status = 1, message = "Table released to Available." });
    }

    /// <summary>
    /// [Internal] Đặt bàn về Occupied. Order Service gọi khi tạo đơn mới.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("{id:int}/occupy")]
    public async Task<IActionResult> OccupyTable(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        table.Status = 2; // Occupied
        await _context.SaveChangesAsync();
        return Ok(new { tableId = id, status = 2, message = "Table set to Occupied." });
    }

    /// <summary>Tạo bàn mới (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpPost]
    public async Task<ActionResult<TableResponse>> Create([FromBody] CreateTableRequest request)
    {
        if (request.Capacity <= 0) return BadRequest("Capacity must be greater than 0.");

        var table = new Table
        {
            Capacity = request.Capacity,
            Status = 1,
            CreatedAt = DateTime.UtcNow
        };

        _context.Tables.Add(table);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetById), new { id = table.TableId }, ToResponse(table));
    }

    /// <summary>Cập nhật bàn (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<TableResponse>> Update(int id, [FromBody] UpdateTableRequest request)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");
        if (request.Capacity <= 0) return BadRequest("Capacity must be greater than 0.");
        if (request.Status < 1 || request.Status > 3) return BadRequest("Status must be 1, 2, or 3.");

        table.Capacity = request.Capacity;
        table.Status = request.Status;
        await _context.SaveChangesAsync();
        return Ok(ToResponse(table));
    }

    /// <summary>Xóa bàn nếu không có đơn active (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        // Kiểm tra bàn đang bị Occupied thì không cho xóa
        if (table.Status == 2) return Conflict("Cannot delete a table that is currently occupied.");

        _context.Tables.Remove(table);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Tạo và tải QR code cho bàn (Admin).</summary>
    [Authorize(Roles = "1")]
    [HttpGet("{id:int}/qrcode")]
    public async Task<IActionResult> GetQrCode(int id)
    {
        var table = await _context.Tables.FindAsync(id);
        if (table == null) return NotFound($"Table {id} not found.");

        var baseUrl = _configuration["AppBaseUrl"] ?? "http://localhost:5000";
        var qrContent = $"{baseUrl}/order?tableId={id}";

        var qrGenerator = new QRCodeGenerator();
        var qrData = qrGenerator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(qrData);
        var pngBytes = qrCode.GetGraphic(10);

        return File(pngBytes, "image/png", $"table_{id}_qrcode.png");
    }
}
