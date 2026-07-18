using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PRM.Services.Order.Clients;
using PRM.Services.Order.Data;
using PRM.Services.Order.Hubs;
using PRM.Services.Order.Notifications;
using System.Text;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR(options =>
{
    options.HandshakeTimeout = TimeSpan.FromSeconds(60);
});

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
connectionString = ConvertPostgresConnectionString(connectionString);
builder.Services.AddDbContext<OrderDbContext>(options =>
    options.UseNpgsql(connectionString));

// Inter-service HTTP Client (Restaurant Service)
builder.Services.AddHttpClient<IRestaurantServiceClient, RestaurantServiceClient>(client =>
{
    var url = builder.Configuration["Services:RestaurantService"] ?? "http://localhost:5002/";
    client.BaseAddress = new Uri(url);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// Notification service (SignalR)
builder.Services.AddScoped<INotificationService, SignalRNotificationService>();

// JWT Auth
var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32 || jwtKey.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine("[STARTUP] CẢNH BÁO NGHIÊM TRỌNG: Jwt:Key đang rỗng/yếu/placeholder — mọi JWT có thể bị giả mạo. Đặt 1 khoá ngẫu nhiên mạnh (>=32 ký tự) trước khi lên production.");
}
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
        // SignalR needs token from query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments(StaffNotificationHub.Route))
                    context.Token = accessToken;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "PRM Order Service", Version = "v1" });
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

// Rate limit cho endpoint public tạo/sửa đơn (PlaceOrder, AddItems) — chặn spam đơn ảo.
// Partition theo IP để mỗi khách/bàn có hạn mức riêng, không dùng chung 1 bucket toàn hệ thống.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("order-write", httpContext => RateLimitPartition.GetFixedWindowLimiter(
        partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        factory: _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 20,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

// Mọi traffic đi qua PRM.Gateway (Ocelot) — nếu không đọc X-Forwarded-For thì
// Connection.RemoteIpAddress luôn là IP của Gateway, khiến rate limiter (theo IP,
// xem AddRateLimiter phía trên) gộp chung mọi khách vào 1 hạn mức. Gateway đã được
// cấu hình gắn IP khách thật vào header này trước khi proxy xuống đây.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor,
    KnownNetworks = { },
    KnownProxies = { }
});

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/", () => "PRM Order Service Running - Port 5003");
app.MapControllers();
app.MapHub<StaffNotificationHub>(StaffNotificationHub.Route);
app.MapHub<FeedbackHub>(FeedbackHub.Route);

// Auto-migrate on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
    db.Database.Migrate();
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
