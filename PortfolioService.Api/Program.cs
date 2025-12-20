using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using PortfolioService.Domain.Data;
using PortfolioService.Domain.Interface;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// JWT config (copy from AuthService / MarketService)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key")!;
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// DbContext (point this at the same DB as MarketService)
builder.Services.AddDbContext<PortfolioDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MarketDb"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "market")); // same schema
});

// DI
builder.Services.AddScoped<IPortfolioService, PortfolioService.Infrastructure.Services.PortfolioService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
