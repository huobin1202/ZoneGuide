using System.Text;
using Microsoft.AspNetCore.RateLimiting;
using ZoneGuide.API.Data;
using ZoneGuide.API.Hubs;
using ZoneGuide.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Kestrel để lắng nghe trên tất cả interfaces (cho phép điện thoại kết nối qua WiFi)
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(56042); // HTTP
    options.ListenAnyIP(56040, listenOptions =>
    {
        listenOptions.UseHttps(); // HTTPS
    });
});

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() 
    { 
        Title = "ZoneGuide API", 
        Version = "v1",
        Description = "API cho ứng dụng hướng dẫn du lịch tự động ZoneGuide"
    });
    
    // JWT Authentication for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ZoneGuide_Secret_Key_2024_Very_Long_Key_For_Security";
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtKey)),
        ValidateIssuer = false,
        ValidateAudience = false,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

// Services
builder.Services.AddScoped<IPOIService, POIService>();
builder.Services.AddScoped<ITourService, TourService>();
builder.Services.AddScoped<IAnalyticsService, AnalyticsService>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<PoiQrCodeService>();
builder.Services.AddScoped<IPOIContributionService, POIContributionService>();
builder.Services.AddScoped<IActivityLogService, ActivityLogService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddSingleton<IQrRealtimeMonitoringService, QrRealtimeMonitoringService>();
builder.Services.AddSingleton<IMobileLiveMonitoringService, MobileLiveMonitoringService>();

// AutoMapper
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(Program).Assembly));

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("Auth", opts =>
    {
        opts.PermitLimit = 10;
        opts.Window = TimeSpan.FromMinutes(1);
        opts.QueueLimit = 2;
    });
    options.AddFixedWindowLimiter("General", opts =>
    {
        opts.PermitLimit = 200;
        opts.Window = TimeSpan.FromMinutes(1);
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseHttpsRedirection(); // Chỉ redirect HTTPS ở Production
}

// Security headers
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["X-Permitted-Cross-Domain-Policies"] = "none";
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers["Content-Security-Policy"] = "default-src 'self'";
    }
    await next();
});

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<QrMonitoringHub>("/hubs/qr-monitor");
app.MapHub<MobileMonitoringHub>("/hubs/mobile-monitor");
app.MapHub<NotificationHub>("/hubs/notifications");

// Auto migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var qrService = scope.ServiceProvider.GetRequiredService<PoiQrCodeService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    db.Database.Migrate();

    var adminEmail = config["SeedAccounts:AdminEmail"] ?? "admin@ZoneGuide.com";
    var adminPassword = config["SeedAccounts:AdminPassword"];
    var adminDisplayName = config["SeedAccounts:AdminDisplayName"] ?? "Administrator";
    var userEmail = config["SeedAccounts:UserEmail"] ?? "user@ZoneGuide.com";
    var userPassword = config["SeedAccounts:UserPassword"];
    var userDisplayName = config["SeedAccounts:UserDisplayName"] ?? "Mobile User";

    // Seed Admin account if not exists
    if (!string.IsNullOrEmpty(adminPassword) && !db.Users.Any(u => u.Role == ZoneGuide.Shared.Models.UserRole.Admin))
    {
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        var salt = Convert.ToBase64String(hmac.Key);
        var hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(adminPassword)));

        var adminUser = new ZoneGuide.API.Data.UserEntity
        {
            Email = adminEmail,
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = adminDisplayName,
            Role = ZoneGuide.Shared.Models.UserRole.Admin,
            Status = ZoneGuide.Shared.Models.UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(adminUser);
        db.SaveChanges();

        logger.LogInformation("Admin account created: {Email}", adminEmail);
    }

    // Seed default mobile user account if not exists
    if (!string.IsNullOrEmpty(userPassword) && !db.Users.Any(u => u.Email == userEmail))
    {
        using var userHmac = new System.Security.Cryptography.HMACSHA512();
        var userSalt = Convert.ToBase64String(userHmac.Key);
        var userHash = Convert.ToBase64String(userHmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(userPassword)));

        var normalUser = new ZoneGuide.API.Data.UserEntity
        {
            Email = userEmail,
            PasswordHash = userHash,
            PasswordSalt = userSalt,
            DisplayName = userDisplayName,
            Role = ZoneGuide.Shared.Models.UserRole.User,
            Status = ZoneGuide.Shared.Models.UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(normalUser);
        db.SaveChanges();

        logger.LogInformation("Default user account created: {Email}", userEmail);
    }

    var poiIds = db.POIs
        .Where(p => p.IsActive)
        .Select(p => p.Id)
        .ToList();

    foreach (var poiId in poiIds)
    {
        await qrService.EnsureQrCodeGeneratedAsync(poiId, force: true);
    }
}

app.Run();
