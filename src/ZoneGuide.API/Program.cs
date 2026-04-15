using System.Text;
using ZoneGuide.API.Data;
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

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Auto migrate database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    
    // Seed Admin account if not exists
    if (!db.Users.Any(u => u.Role == ZoneGuide.Shared.Models.UserRole.Admin))
    {
        // Create password hash for admin (password: Admin@123)
        using var hmac = new System.Security.Cryptography.HMACSHA512();
        var salt = Convert.ToBase64String(hmac.Key);
        var hash = Convert.ToBase64String(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("Admin@123")));
        
        var adminUser = new ZoneGuide.API.Data.UserEntity
        {
            Email = "admin@ZoneGuide.com",
            PasswordHash = hash,
            PasswordSalt = salt,
            DisplayName = "Administrator",
            Role = ZoneGuide.Shared.Models.UserRole.Admin,
            Status = ZoneGuide.Shared.Models.UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
        
        db.Users.Add(adminUser);
        db.SaveChanges();
        
        Console.WriteLine("===========================================");
        Console.WriteLine("Admin account created:");
        Console.WriteLine("Email: admin@ZoneGuide.com");
        Console.WriteLine("Password: Admin@123");
        Console.WriteLine("===========================================");
    }

    // Seed default mobile user account if not exists
    if (!db.Users.Any(u => u.Email == "user@ZoneGuide.com"))
    {
        using var userHmac = new System.Security.Cryptography.HMACSHA512();
        var userSalt = Convert.ToBase64String(userHmac.Key);
        var userHash = Convert.ToBase64String(userHmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes("User@123")));

        var normalUser = new ZoneGuide.API.Data.UserEntity
        {
            Email = "user@ZoneGuide.com",
            PasswordHash = userHash,
            PasswordSalt = userSalt,
            DisplayName = "Mobile User",
            Role = ZoneGuide.Shared.Models.UserRole.User,
            Status = ZoneGuide.Shared.Models.UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        db.Users.Add(normalUser);
        db.SaveChanges();

        Console.WriteLine("===========================================");
        Console.WriteLine("Default user account created:");
        Console.WriteLine("Email: user@ZoneGuide.com");
        Console.WriteLine("Password: User@123");
        Console.WriteLine("===========================================");
    }
}

app.Run();
