using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PRM.Services.Restaurant.Data;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Add PRM.Shared reference for enums
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
connectionString = ConvertPostgresConnectionString(connectionString);
builder.Services.AddDbContext<RestaurantDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Cloudinary Settings & Service
builder.Services.Configure<PRM.Services.Restaurant.Models.CloudinarySettings>(
    builder.Configuration.GetSection("CloudinarySettings"));
builder.Services.AddScoped<PRM.Services.Restaurant.Services.IPhotoService, PRM.Services.Restaurant.Services.PhotoService>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "PRM Restaurant Service", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization", Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer", BearerFormat = "JWT", In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Nhập JWT token."
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                    { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => "PRM Restaurant Service Running - Port 5002");
app.MapControllers();

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<RestaurantDbContext>();
    db.Database.Migrate();

    // Reset and seed MenuItems to match user's custom items
    try
    {
        if (!db.MenuItems.Any())
        {
            db.MenuItems.AddRange(new List<PRM.Services.Restaurant.Models.MenuItem>
            {
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "matcha",
                    Description = "trà olong thanh mát",
                    Price = 10000,
                    Category = PRM.Shared.Enums.MenuCategory.Tea,
                    ImageUrl = null,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "banhngot",
                    Description = "banhngotmoi",
                    Price = 10000,
                    Category = PRM.Shared.Enums.MenuCategory.Cake,
                    ImageUrl = null,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "7up",
                    Description = "nước 7up giải khát",
                    Price = 10000,
                    Category = PRM.Shared.Enums.MenuCategory.Juice,
                    ImageUrl = "/data/user/0/com.example.qr_order/cache/5c5443fa-a6be-4b07-9b2f-b4b1a4570b20/IMG_20260630_102115.jpg",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "Pudding siêu ngọt",
                    Description = "khó lỗi",
                    Price = 120000,
                    Category = PRM.Shared.Enums.MenuCategory.Cake,
                    ImageUrl = "/data/user/0/com.example.qr_order/cache/5c5443fa-a6be-4b07-9b2f-b4b1a4570b20/IMG_20260630_102128.jpg",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "bánh siêu ngọt",
                    Description = "bánh ngon",
                    Price = 20000,
                    Category = PRM.Shared.Enums.MenuCategory.Cake,
                    ImageUrl = null,
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                },
                new PRM.Services.Restaurant.Models.MenuItem
                {
                    Name = "trà vải",
                    Description = "trà vải siêu ngọt",
                    Price = 20000,
                    Category = PRM.Shared.Enums.MenuCategory.Tea,
                    ImageUrl = "https://res.cloudinary.com/dkppq6bsj/image/upload/v1719374026/restaurant_menu/n04iayuhqspq71u8swy1.jpg",
                    IsAvailable = true,
                    CreatedAt = DateTime.UtcNow
                }
            });

            db.SaveChanges();
            Console.WriteLine("✅ Database seeded successfully with user's custom MenuItems.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"❌ Error seeding database: {ex.Message}");
    }
}

app.Run();

static string? ConvertPostgresConnectionString(string? connectionString)
{
    if (string.IsNullOrEmpty(connectionString))
        return connectionString;

    if (connectionString.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        connectionString.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var databaseUri = new Uri(connectionString);
        var userInfo = databaseUri.UserInfo.Split(':');
        
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var host = Uri.UnescapeDataString(databaseUri.Host);
        var port = databaseUri.Port == -1 ? 5432 : databaseUri.Port;
        var database = Uri.UnescapeDataString(databaseUri.AbsolutePath.TrimStart('/'));

        return $"Host={host};Port={port};Database={database};Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true;";
    }

    return connectionString;
}
