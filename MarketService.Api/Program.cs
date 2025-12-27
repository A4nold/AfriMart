using System.Net;
using MarketService.Infrastructure.Data;
//using MarketService.Infrastructure.Services;
using System.Text;
using MarketService.Application.Gateways;
using MarketService.Application.Helper;
using MarketService.Application.Interfaces;
using MarketService.Application.Services;
using MarketService.Domain.Interface;
using MarketService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// DbContext
builder.Services.AddDbContext<MarketDbContext>(options =>
{
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("MarketDb"),
        npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "market"));
});


// JWT auth (reuse same config as BlockchainService)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection.GetValue<string>("Key")!;
var jwtIssuer = jwtSection.GetValue<string>("Issuer");
var jwtAudience = jwtSection.GetValue<string>("Audience");

Console.WriteLine($"[Startup] JWT KEY : ");
Console.WriteLine($"[Startup] JWT ISSUER : {jwtIssuer}");
Console.WriteLine($"[Startup] JWT AUDIENCE : {jwtAudience}");

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey!));

// Register domain interfaces to infrastructure implementations
//builder.Services.AddScoped<IMarketService, MarketService.Infrastructure.Services.MarketService>();
//builder.Services.AddScoped<IPositionService, PositionService>();
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddScoped<MarketActionExecutor>(); // or IMarketActionExecutor
builder.Services.AddScoped<IMarketRepository, MarketRepository>();
builder.Services.AddScoped<IMarketActionRepository, MarketActionRepository>();
builder.Services.AddScoped<IUserPositionRepository, UserPositionRepository>();
builder.Services.AddScoped<IMarketApplication, MarketApplication>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();


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
            IssuerSigningKey = signingKey
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var auth = context.Request.Headers.Authorization.ToString();
                Console.WriteLine($"Authorization header: '{auth}'");
                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"JWT Authentication Failed");
                Console.WriteLine(context.Exception);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine("JWT Challenge Triggered:");
                Console.WriteLine($"Error: {context.Error}, Description: {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = true;
    options.ValidateOnBuild = true;
});

builder.Services.Configure<BlockchainGatewayOptions>(
    builder.Configuration.GetSection("BlockchainGateway"));

builder.Services.AddAuthorization();

// HttpClient to call BlockchainService
builder.Services.AddHttpClient<IBlockchainGateway, BlockchainGatewayHttp>((sp, client) =>
{
    var opts = sp.GetRequiredService<IOptions<BlockchainGatewayOptions>>().Value;
    client.BaseAddress = new Uri(opts.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(120);
});

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Market Service API", Version = "v1" });

    // Add JWT Bearer support
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "Enter: Bearer {your JWT token}",
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

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
