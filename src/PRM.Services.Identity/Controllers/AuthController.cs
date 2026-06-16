using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PRM.Services.Identity.Data;
using PRM.Services.Identity.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace PRM.Services.Identity.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IdentityDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(IdentityDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    // --- DTOs ---
    public record LoginRequest(string UsernameOrEmail, string Password);
    public record LoginResponse(string AccessToken, DateTime ExpiresAt, string Username, int Role, int AccountId);
    public record AccountResponse(
        int AccountId, string Username, string Email,
        string FullName, string? PhoneNumber, int Role,
        bool IsActive, DateTime CreatedAt, DateTime? LastLoginAt);
    public record AdminCreateAccountRequest(string Username, string Email, string Password, string FullName, string? PhoneNumber);
    public record UpdateAccountRequest(string Username, string Email, string FullName, string? PhoneNumber, bool? IsActive);
    public record RegisterRequest(string Username, string Email, string Password, string FullName, string? PhoneNumber);

    private static AccountResponse ToResponse(Account a) => new(
        a.AccountId, a.Username, a.Email,
        a.FullName, a.PhoneNumber, a.Role,
        a.IsActive, a.CreatedAt, a.LastLoginAt);

    /// <summary>Đăng nhập bằng username/email, trả về JWT token.</summary>
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UsernameOrEmail) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("UsernameOrEmail and Password are required.");

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.IsActive &&
                (a.Username == request.UsernameOrEmail || a.Email == request.UsernameOrEmail));

        if (account == null) return Unauthorized();

        bool isValid;
        try { isValid = BCrypt.Net.BCrypt.Verify(request.Password, account.PasswordHash); }
        catch { return Unauthorized(); }

        if (!isValid) return Unauthorized();

        account.LastLoginAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok(CreateToken(account));
    }

    /// <summary>Đăng ký tài khoản khách hàng mới (role 0).</summary>
    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Username, Email, Password, and FullName are required.");

        if (await _context.Accounts.AnyAsync(a => a.Username == request.Username || a.Email == request.Email))
            return Conflict("Username or Email already exists.");

        var account = new Account
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = 0, // Customer
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(Login), new { accountId = account.AccountId }, ToResponse(account));
    }

    /// <summary>Admin lấy danh sách nhân viên (role 2).</summary>
    [Authorize(Roles = "1")]
    [HttpGet("staff")]
    public async Task<ActionResult<IEnumerable<AccountResponse>>> GetStaff()
    {
        var staff = await _context.Accounts
            .Where(a => a.Role == 2)
            .OrderBy(a => a.FullName)
            .Select(a => ToResponse(a))
            .ToListAsync();
        return Ok(staff);
    }

    /// <summary>Admin tạo tài khoản nhân viên mới (role 2).</summary>
    [Authorize(Roles = "1")]
    [HttpPost("admin-create")]
    public async Task<ActionResult<AccountResponse>> AdminCreate([FromBody] AdminCreateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password) || string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Username, Email, Password, and FullName are required.");

        if (await _context.Accounts.AnyAsync(a => a.Username == request.Username || a.Email == request.Email))
            return Conflict("Username or Email already exists.");

        var account = new Account
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            FullName = request.FullName,
            PhoneNumber = request.PhoneNumber,
            Role = 2, // Staff
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetStaff), new { id = account.AccountId }, ToResponse(account));
    }

    /// <summary>Admin hoặc Staff cập nhật thông tin tài khoản.</summary>
    [Authorize(Roles = "1,2")]
    [HttpPut("accounts/{id:int}")]
    public async Task<ActionResult<AccountResponse>> UpdateAccount(int id, [FromBody] UpdateAccountRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.FullName))
            return BadRequest("Username, Email, and FullName are required.");

        var currentRole = User.FindFirst(ClaimTypes.Role)?.Value;
        var currentIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst("sub");
        var isAdmin = currentRole == "1";

        if (!isAdmin)
        {
            if (currentIdClaim == null || !int.TryParse(currentIdClaim.Value, out var currentId) || currentId != id)
                return Forbid();
        }

        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return NotFound($"Account {id} not found.");

        if (await _context.Accounts.AnyAsync(a => a.AccountId != id && (a.Username == request.Username || a.Email == request.Email)))
            return Conflict("Username or Email already exists.");

        account.Username = request.Username;
        account.Email = request.Email;
        account.FullName = request.FullName;
        account.PhoneNumber = request.PhoneNumber;

        if (isAdmin && request.IsActive.HasValue)
            account.IsActive = request.IsActive.Value;

        await _context.SaveChangesAsync();
        return Ok(ToResponse(account));
    }

    /// <summary>Admin xóa mềm tài khoản (IsActive = false).</summary>
    [Authorize(Roles = "1")]
    [HttpDelete("accounts/{id:int}")]
    public async Task<IActionResult> DeleteAccount(int id)
    {
        var account = await _context.Accounts.FindAsync(id);
        if (account == null) return NotFound($"Account {id} not found.");

        var currentIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst("sub");
        if (currentIdClaim != null && int.TryParse(currentIdClaim.Value, out var currentId) && currentId == id)
            return BadRequest("Admin cannot delete their own account.");

        account.IsActive = false;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private LoginResponse CreateToken(Account account)
    {
        var key = _configuration["Jwt:Key"] ?? string.Empty;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.AccountId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, account.Username),
            new(ClaimTypes.Role, account.Role.ToString())
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddHours(4);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, account.Username, account.Role, account.AccountId);
    }
}
