using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Pnle.Api.Auth;
using Pnle.Api.Common;
using Pnle.Api.Tutoring;
using Pnle.Application.Auth;
using Pnle.Application.Common;
using Pnle.Application.Tutoring;
using Pnle.Infrastructure.Ai;
using Pnle.Infrastructure.Auth;
using Pnle.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Core services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Options
builder.Services.AddOptions<GoogleOptions>()
    .Bind(builder.Configuration.GetSection("Google"))
    .Validate(options => options.ClientIds.Length > 0,
        "Google:ClientIds is required.")
    .ValidateOnStart();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options => options.SigningKey is { Length: >= 64 },
        "Auth:SigningKey must be at least 64 characters.")
    .ValidateOnStart();

builder.Services.AddOptions<RefreshTokenOptions>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options => options.RefreshTokenDays is > 0 and <= 365,
        "Auth:RefreshTokenDays must be between 1 and 365.")
    .ValidateOnStart();

builder.Services.AddOptions<AuthCookieOptions>()
    .Bind(builder.Configuration.GetSection("Auth"))
    .Validate(options =>
            Enum.TryParse<SameSiteMode>(options.CookieSameSite, ignoreCase: true, out var sameSite) &&
            sameSite != SameSiteMode.Unspecified,
        "Auth:CookieSameSite must be one of: None, Lax, Strict.")
    .ValidateOnStart();

builder.Services.AddOptions<AiServiceOptions>()
    .Bind(builder.Configuration.GetSection("AiService"))
    .Validate(options =>
            !string.IsNullOrWhiteSpace(options.BaseUrl) &&
            Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _),
        "AiService:BaseUrl must be an absolute URL.")
    .Validate(options => options.ApiKey is { Length: >= 32 },
        "AiService:ApiKey must be at least 32 characters.")
    .ValidateOnStart();

// CORS
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 20;
        limiter.QueueLimit = 0;
    });

    options.AddFixedWindowLimiter("ai", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 30;
        limiter.QueueLimit = 0;
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IUserRepository, EfUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, EfRefreshTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, EfUnitOfWork>();

// Auth infrastructure
builder.Services.AddSingleton<IRefreshTokenHasher, Sha256RefreshTokenHasher>();
builder.Services.AddSingleton<IAccessTokenIssuer, JwtAccessTokenIssuer>();
builder.Services.AddSingleton<IRefreshTokenIssuer, RefreshTokenIssuer>();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();

// Application handlers
builder.Services.AddScoped<LoginWithGoogleHandler>();
builder.Services.AddScoped<RefreshSessionHandler>();

// API services
builder.Services.AddSingleton<RefreshCookieWriter>();

// AI tutoring service client
builder.Services.AddTransient<AiApiKeyDelegatingHandler>();

builder.Services.AddHttpClient<IAiTutorClient, HttpAiTutorClient>(
        (serviceProvider, client) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptions<AiServiceOptions>>()
                .Value;

            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        })
    .AddHttpMessageHandler<AiApiKeyDelegatingHandler>();

// JWT authentication - consumes the registered JwtOptions as the single
// source of truth instead of re-reading the "Auth" configuration section.
var isDevelopment = builder.Environment.IsDevelopment();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearerOptions, jwtOptions) =>
    {
        var options = jwtOptions.Value;

        bearerOptions.MapInboundClaims = false;
        bearerOptions.RequireHttpsMetadata = !isDevelopment;

        bearerOptions.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,

            ValidateAudience = true,
            ValidAudience = options.Audience,

            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(options.SigningKey)),

            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// Development database setup
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // For production, use migrations instead.
    db.Database.EnsureCreated();
}

// Middleware
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Endpoints
app.MapGet("/healthz", () => Results.Ok(new { status = "ok" }));

app.MapAuthEndpoints();

app.MapTutoringEndpoints();

app.Run();
