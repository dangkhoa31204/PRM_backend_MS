using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Load ocelot config: use docker version for production (Render/Docker), local for development
var ocelotFile = builder.Environment.IsDevelopment() ? "ocelot.json" : "ocelot.docker.json";
builder.Configuration.AddJsonFile(ocelotFile, optional: false, reloadOnChange: true);

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

app.MapGet("/", () => "PRM API Gateway Running - Port 5000");

await app.UseOcelot();

app.Run();
