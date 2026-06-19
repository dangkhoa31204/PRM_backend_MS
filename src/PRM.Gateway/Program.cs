using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Detect Render environment and generate Ocelot config dynamically
var identityHost = Environment.GetEnvironmentVariable("IDENTITY_HOST");
var restaurantHost = Environment.GetEnvironmentVariable("RESTAURANT_HOST");
var orderHost = Environment.GetEnvironmentVariable("ORDER_HOST");

if (!string.IsNullOrEmpty(identityHost) && !string.IsNullOrEmpty(restaurantHost) && !string.IsNullOrEmpty(orderHost))
{
    // Running on Render: generate ocelot config with HTTPS public URLs
    var ocelotJson = GenerateRenderOcelotConfig(identityHost, restaurantHost, orderHost);
    var tempPath = Path.Combine(AppContext.BaseDirectory, "ocelot.render.json");
    File.WriteAllText(tempPath, ocelotJson);
    builder.Configuration.AddJsonFile(tempPath, optional: false, reloadOnChange: false);
}
else
{
    // Local / Docker: use file-based config
    var ocelotFile = builder.Environment.IsDevelopment() ? "ocelot.json" : "ocelot.docker.json";
    builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);
}

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
    if (context.Request.Path == "/")
    {
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("PRM API Gateway Running - Port 5000");
        return;
    }
    await next();
});

await app.UseOcelot();

app.Run();

// Helper: Generate Ocelot config for Render (HTTPS public URLs)
static string GenerateRenderOcelotConfig(string identityHost, string restaurantHost, string orderHost)
{
    return $$"""
    {
      "Routes": [
        {
          "DownstreamPathTemplate": "/api/auth/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{identityHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/auth/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/menu/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurantHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/menu/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/menu",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurantHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/menu",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/tables/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurantHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/tables/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/tables",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{restaurantHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/tables",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/orders/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/orders/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/orders",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/orders",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/payments/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/payments/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/payments",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/payments",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/api/feedbacks/{everything}",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/feedbacks/{everything}",
          "UpstreamHttpMethod": [ "GET", "POST", "PUT", "DELETE", "PATCH" ]
        },
        {
          "DownstreamPathTemplate": "/api/feedbacks",
          "DownstreamScheme": "https",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/api/feedbacks",
          "UpstreamHttpMethod": [ "GET", "POST" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/staff",
          "DownstreamScheme": "wss",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
          "UpstreamPathTemplate": "/hubs/staff",
          "UpstreamHttpMethod": [ "GET" ]
        },
        {
          "DownstreamPathTemplate": "/hubs/feedback",
          "DownstreamScheme": "wss",
          "DownstreamHostAndPorts": [{ "Host": "{{orderHost}}", "Port": 443 }],
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
