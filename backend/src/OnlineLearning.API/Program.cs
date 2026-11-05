using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OnlineLearning.API.Common;
using OnlineLearning.Application;
using OnlineLearning.Application.Common.Interfaces;
using OnlineLearning.Application.Common.Options;
using OnlineLearning.Infrastructure;
using OnlineLearning.Infrastructure.Persistence;
using OnlineLearning.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);
var isTesting = builder.Environment.IsEnvironment("Testing");

// ---------- Tầng ứng dụng ----------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, configureDatabase: !isTesting);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

// ---------- Xác thực JWT ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey.PadRight(32))),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role",
        };
    });
builder.Services.AddAuthorization();

// ---------- MVC, validation, lỗi ----------
builder.Services.AddControllers(o => o.Filters.Add<ValidationFilter>())
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// ---------- Chống brute force ----------
// RateLimiting:Enabled=false chỉ dùng cho kiểm thử E2E tự động
var rateLimitOff = isTesting || !builder.Configuration.GetValue("RateLimiting:Enabled", true);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy(RateLimitPolicies.Auth, http =>
        rateLimitOff
            ? RateLimitPartition.GetNoLimiter("off")
            : RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy(RateLimitPolicies.Payment, http =>
        rateLimitOff
            ? RateLimitPartition.GetNoLimiter("off")
            : RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- CORS ----------
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy("Client", policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

// ---------- Swagger ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "EduViet API", Version = "v1", Description = "API nền tảng khóa học trực tuyến" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Dán access token (không cần chữ Bearer).",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = [],
    });
    var xml = Path.Combine(AppContext.BaseDirectory, "OnlineLearning.API.xml");
    if (File.Exists(xml)) c.IncludeXmlComments(xml);
});

builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.DocumentTitle = "EduViet API");
}
else if (!isTesting)
{
    app.UseHsts();
}

// Header bảo mật cơ bản cho API
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers.XContentTypeOptions = "nosniff";
    ctx.Response.Headers.XFrameOptions = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

// Https:Redirect=false khi TLS được xử lý ở reverse proxy (Nginx/Caddy/Cloudflare) và proxy không gửi X-Forwarded-Proto=https
if (!isTesting && !app.Environment.IsDevelopment() && app.Configuration.GetValue("Https:Redirect", true)) app.UseHttpsRedirection();
// Ảnh tải lên khi lưu cục bộ (wwwroot/uploads). Video/tài liệu KHÔNG nằm ở đây mà phát qua /api/media có chữ ký.
app.UseStaticFiles();
app.UseCors("Client");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/api/health", async (AppDbContext db) =>
{
    var canConnect = await db.Database.CanConnectAsync();
    var pending = canConnect && !isTesting && !OnlineLearning.Infrastructure.DependencyInjection.IsSqliteDemo(app.Configuration)
        ? (await db.Database.GetPendingMigrationsAsync()).Count()
        : 0;
    return Results.Ok(new { status = canConnect ? "ok" : "db_unreachable", pendingMigrations = pending, utc = DateTime.UtcNow });
}).ExcludeFromDescription();

if (!isTesting && app.Configuration.GetValue("Seed:RunOnStartup", false))
{
    // Seed đã bao gồm áp dụng migration
    await DbSeeder.SeedAsync(app.Services);
}
else if (!isTesting && app.Configuration.GetValue("Database:MigrateOnStartup", false))
{
    // Production: tự áp dụng migration khi khởi động (bật bằng Database__MigrateOnStartup=true)
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    await DbSeeder.SeedRolesAndCategoriesAsync(app.Services);
}

app.Run();

public partial class Program;
