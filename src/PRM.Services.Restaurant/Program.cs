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

    // Seed and fix MenuItems with full categories & valid image URLs
    try
    {
        // Fix existing items with invalid local device file paths
        var invalidItems = db.MenuItems.Where(m => m.ImageUrl != null && m.ImageUrl.StartsWith("/data/user")).ToList();
        foreach (var item in invalidItems)
        {
            item.ImageUrl = item.Name switch
            {
                "7up" => "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=600&auto=format&fit=crop&q=80",
                "Pudding siêu ngọt" => "https://images.unsplash.com/photo-1587314168485-3236d6710814?w=600&auto=format&fit=crop&q=80",
                _ => null
            };
        }
        db.SaveChanges();

        var defaultMenuItems = new List<PRM.Services.Restaurant.Models.MenuItem>
        {
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Cà Phê Sữa Đá",
                Description = "Cà phê phin đậm đà hòa quyện với sữa đặc ngọt ngào và đá lạnh.",
                Price = 29000,
                Category = PRM.Shared.Enums.MenuCategory.Coffee,
                ImageUrl = "https://images.unsplash.com/photo-1517701604599-bb29b565090c?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Cà Phê Muối",
                Description = "Sự kết hợp hoàn hảo giữa vị đắng cà phê và lớp kem muối béo ngậy.",
                Price = 35000,
                Category = PRM.Shared.Enums.MenuCategory.Coffee,
                ImageUrl = "https://images.unsplash.com/photo-1514432324607-a09d9b4aefdd?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Trà Đào Cam Sả",
                Description = "Trà đào thơm nức hương sả tươi và vị cam chua nhẹ thanh mát.",
                Price = 39000,
                Category = PRM.Shared.Enums.MenuCategory.Tea,
                ImageUrl = "https://images.unsplash.com/photo-1556679343-c7306c1976bc?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Trà Vải Nhãn",
                Description = "Trà vải thơm ngọt kết hợp trái vải tươi giòn mọng nước.",
                Price = 35000,
                Category = PRM.Shared.Enums.MenuCategory.Tea,
                ImageUrl = "https://res.cloudinary.com/dkppq6bsj/image/upload/v1719374026/restaurant_menu/n04iayuhqspq71u8swy1.jpg",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Matcha Latte Uji",
                Description = "Bột Matcha Uji Nhật Bản nguyên chất hòa cùng sữa tươi thanh trùng béo ngậy.",
                Price = 42000,
                Category = PRM.Shared.Enums.MenuCategory.Tea,
                ImageUrl = "https://images.unsplash.com/photo-1536256263959-770b48d82b0a?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Bánh Tiramisu Ý",
                Description = "Bánh Tiramisu Ý mềm mịn ngập tràn hương vị cà phê và cacao nguyên chất.",
                Price = 45000,
                Category = PRM.Shared.Enums.MenuCategory.Cake,
                ImageUrl = "https://images.unsplash.com/photo-1571115177098-24ec42ed204d?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Pudding Chanh Dây",
                Description = "Pudding mềm mịn béo ngậy phủ sốt chanh dây chua ngọt thanh mát.",
                Price = 35000,
                Category = PRM.Shared.Enums.MenuCategory.Cake,
                ImageUrl = "https://images.unsplash.com/photo-1587314168485-3236d6710814?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Bánh Croissant Bơ Tươi",
                Description = "Bánh sừng bò ngàn lớp thơm lừng hương bơ Pháp cao cấp.",
                Price = 28000,
                Category = PRM.Shared.Enums.MenuCategory.Cake,
                ImageUrl = "https://images.unsplash.com/photo-1555507036-ab1f4038808a?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Nước Ép Cam Tươi",
                Description = "Cam sành ép nguyên chất 100% mọng nước giàu Vitamin C.",
                Price = 38000,
                Category = PRM.Shared.Enums.MenuCategory.Juice,
                ImageUrl = "https://images.unsplash.com/photo-1613478223719-2ab802602423?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Nước 7Up Chanh Đá",
                Description = "Nước 7Up giải khát ướp lạnh cùng lát chanh tươi mát rượi.",
                Price = 20000,
                Category = PRM.Shared.Enums.MenuCategory.Juice,
                ImageUrl = "https://images.unsplash.com/photo-1622483767028-3f66f32aef97?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Mì Ý Sốt Bò Bằm",
                Description = "Mì Spaghetti sốt bò bằm Bolognese thơm lừng kèm phô mai Parmesan.",
                Price = 59000,
                Category = PRM.Shared.Enums.MenuCategory.Other,
                ImageUrl = "https://images.unsplash.com/photo-1621996346565-e3d5d6281273?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            },
            new PRM.Services.Restaurant.Models.MenuItem
            {
                Name = "Khoai Tây Chiên Giòn",
                Description = "Khoai tây chiên vàng giòn rùm rụm kèm sốt mayonnaise tương ớt.",
                Price = 29000,
                Category = PRM.Shared.Enums.MenuCategory.Other,
                ImageUrl = "https://images.unsplash.com/photo-1573080496219-bb080dd4f877?w=600&auto=format&fit=crop&q=80",
                IsAvailable = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        var existingNames = db.MenuItems.Select(m => m.Name.ToLower()).ToList();
        var itemsToAdd = defaultMenuItems.Where(m => !existingNames.Contains(m.Name.ToLower())).ToList();

        if (itemsToAdd.Any())
        {
            db.MenuItems.AddRange(itemsToAdd);
            db.SaveChanges();
            Console.WriteLine($"✅ Added {itemsToAdd.Count} new MenuItems with high-res images to database.");
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
