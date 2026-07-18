using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Read downstream service URLs from Configuration (appsettings.json overridden by environment variables)
var identityUrl = builder.Configuration["Proxy:Services:Identity"] ?? "http://localhost:5001";
var restaurantUrl = builder.Configuration["Proxy:Services:Restaurant"] ?? "http://localhost:5002";
var orderUrl = builder.Configuration["Proxy:Services:Order"] ?? "http://localhost:5003";

// Generate Ocelot config dynamically based on the configured services
var ocelotJson = GenerateOcelotConfig(identityUrl, restaurantUrl, orderUrl);
var tempPath = Path.Combine(AppContext.BaseDirectory, "ocelot.generated.json");
File.WriteAllText(tempPath, ocelotJson);
builder.Configuration.AddJsonFile(tempPath, optional: false, reloadOnChange: false);

// JWT Auth at Gateway level (centralized)
var jwtKey = builder.Configuration["Jwt:Key"] ?? string.Empty;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer("Bearer", options =>
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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader().AllowAnyHeader()));

builder.Services.AddOcelot();

var app = builder.Build();

app.UseCors();
app.UseAuthentication();

app.Use(async (context, next) =>
{
    if (context.Request.Path == "/debug-config")
    {
        var config = context.RequestServices.GetRequiredService<IConfiguration>();
        context.Response.ContentType = "application/json; charset=utf-8";
        var info = new {
            Identity = config["Proxy:Services:Identity"],
            Restaurant = config["Proxy:Services:Restaurant"],
            Order = config["Proxy:Services:Order"]
        };
        await context.Response.WriteAsJsonAsync(info);
        return;
    }
    if (context.Request.Path == "/")
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("PRM API Gateway Running - Port 5000");
        return;
    }
    await next();
});

// Gắn IP khách thật vào X-Forwarded-For trước khi Ocelot proxy xuống downstream —
// nếu không, các service phía sau (vd. rate limiter ở Order) chỉ thấy IP của Gateway,
// khiến mọi khách hàng bị gộp chung 1 hạn mức.
app.Use(async (context, next) =>
{
    var clientIp = context.Connection.RemoteIpAddress?.ToString();
    if (!string.IsNullOrEmpty(clientIp))
        context.Request.Headers["X-Forwarded-For"] = clientIp;
    await next();
});

await app.UseOcelot();

app.Run();

// Helper: Parse service URL into scheme, host, and port
static (string Scheme, string Host, int Port) ParseServiceUrl(string urlString)
{
    if (!urlString.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
        !urlString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
    {
        urlString = "http://" + urlString;
    }
    var uri = new Uri(urlString);
    var scheme = uri.Scheme;
    var host = uri.Host;
    var port = uri.Port;
    if (port == -1)
    {
        port = scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;
    }
    return (scheme, host, port);
}

// Helper: Generate Ocelot config dynamically
static string GenerateOcelotConfig(string identityUrl, string restaurantUrl, string orderUrl)
{
    var identity = ParseServiceUrl(identityUrl);
    var restaurant = ParseServiceUrl(restaurantUrl);
    var order = ParseServiceUrl(orderUrl);

    // Force HTTPS/WSS for Render hosts to avoid 301 redirects which break WebSockets
    if (order.Host.EndsWith("onrender.com", StringComparison.OrdinalIgnoreCase))
    {
        order.Scheme = "https";
        order.Port = 443;
    }
    
    var wsScheme = order.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase) ? "wss" : "ws";

    return $$"""
    {
      "Routes": [
        {
          "DownstreamPathTemplate": "/api/auth/{everything}",
          "DownstreamScheme": "{{identity.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{identity.Host}}", "Port": {{identity.Port}} }],
          "UpstreamPathTemplate": "/api/auth/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/menu/{everything}",
          "DownstreamScheme": "{{restaurant.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurant.Host}}", "Port": {{restaurant.Port}} }],
          "UpstreamPathTemplate": "/api/menu/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/menu",
          "DownstreamScheme": "{{restaurant.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurant.Host}}", "Port": {{restaurant.Port}} }],
          "UpstreamPathTemplate": "/api/menu",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/tables/{everything}",
          "DownstreamScheme": "{{restaurant.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurant.Host}}", "Port": {{restaurant.Port}} }],
          "UpstreamPathTemplate": "/api/tables/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/tables",
          "DownstreamScheme": "{{restaurant.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurant.Host}}", "Port": {{restaurant.Port}} }],
          "UpstreamPathTemplate": "/api/tables",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/orders/{everything}",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/orders/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/orders",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/orders",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/payments/{everything}",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/payments/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/payments",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/payments",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/feedbacks/{everything}",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/feedbacks/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/feedbacks",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/api/feedbacks",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/staff/negotiate",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/hubs/staff/negotiate",
          "UpstreamHttpMethod": [ "POST" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/staff",
          "DownstreamScheme": "{{wsScheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/hubs/staff",
          "UpstreamHttpMethod": [ "GET" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/feedback/negotiate",
          "DownstreamScheme": "{{order.Scheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/hubs/feedback/negotiate",
          "UpstreamHttpMethod": [ "POST" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/feedback",
          "DownstreamScheme": "{{wsScheme}}",
          "DownstreamHostAndPorts": [{ "Host": "{{order.Host}}", "Port": {{order.Port}} }],
          "UpstreamPathTemplate": "/hubs/feedback",
          "UpstreamHttpMethod": [ "GET" ]
        }
      ],
      "GlobalConfiguration": {
        "BaseUrl": "https://{{Environment.GetEnvironmentVariable("RENDER_EXTERNAL_HOSTNAME") ?? "localhost"}}"
      }
    }
    """;
}
