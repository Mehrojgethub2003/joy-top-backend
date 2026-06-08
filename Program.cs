using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using JoyTopBackend.Application.Interfaces;
using JoyTopBackend.Application.Services;
using JoyTopBackend.Infrastructure.Persistence;
using JoyTopBackend.Infrastructure.ExternalServices;
using JoyTopBackend.Domain.Interfaces;
using JoyTopBackend.Infrastructure.Repositories;

var builder = WebApplication.CreateBuilder(args);

// 1. CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// 2. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "bu_juda_maxfiy_va_uzun_kalit_bolishi_shart_joy_top_2024";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "JoyTop",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "JoyTopUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// 3. Controllers
builder.Services.AddControllers();

// 4. Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Joy Top API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.",
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
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 5. Dependency Injection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=joy_top.db"));

// External & Auth Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<JoyTopBackend.Infrastructure.ExternalServices.OsmSyncService>();
builder.Services.AddSingleton<ITelegramService, TelegramService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IPlaceRepository, EfPlaceRepository>();
builder.Services.AddHostedService<TelegramBotManager>();

var app = builder.Build();

// 6. Pipeline
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.EnsureCreated();
    
    // Auto-create PlaceRatings table if it doesn't exist
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlaceRatings"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlaceRatings"" PRIMARY KEY AUTOINCREMENT,
            ""PlaceId"" INTEGER NOT NULL,
            ""UserPhone"" TEXT NOT NULL,
            ""Score"" INTEGER NOT NULL,
            ""CreatedAt"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT
        );
    ");

    // Auto-create PlaceVotes table if it doesn't exist
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlaceVotes"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlaceVotes"" PRIMARY KEY AUTOINCREMENT,
            ""PlaceId"" INTEGER NOT NULL,
            ""DeviceId"" TEXT NOT NULL,
            ""VoteType"" TEXT NOT NULL,
            ""Value"" TEXT NOT NULL,
            ""CreatedAt"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT
        );
    ");

    // Auto-create PlaceLikes table if it doesn't exist
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlaceLikes"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlaceLikes"" PRIMARY KEY AUTOINCREMENT,
            ""PlaceId"" INTEGER NOT NULL,
            ""DeviceId"" TEXT NOT NULL,
            ""CreatedAt"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT
        );
    ");

    // Auto-create PlaceLocationComments table if it doesn't exist
    dbContext.Database.ExecuteSqlRaw("DROP TABLE IF EXISTS \"PlaceLocationComments\";");
    dbContext.Database.ExecuteSqlRaw(@"
        CREATE TABLE IF NOT EXISTS ""PlaceLocationComments"" (
            ""Id"" INTEGER NOT NULL CONSTRAINT ""PK_PlaceLocationComments"" PRIMARY KEY AUTOINCREMENT,
            ""PlaceId"" INTEGER NOT NULL,
            ""DeviceId"" TEXT NOT NULL,
            ""UserName"" TEXT NOT NULL,
            ""CommentText"" TEXT NOT NULL,
            ""CreatedAt"" TEXT NOT NULL,
            ""UpdatedAt"" TEXT
        );
    ");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot")),
    RequestPath = ""
});
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
// 7. Request Logging Middleware
app.Use(async (context, next) =>
{
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var request = context.Request;
    
    // ANSI ranglar
    string cyan = "\x1b[36m";
    string green = "\x1b[32m";
    string red = "\x1b[31m";
    string yellow = "\x1b[33m";
    string reset = "\x1b[0m";
    string bold = "\x1b[1m";

    Console.WriteLine($"{cyan}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{reset}");
    Console.WriteLine($"{cyan}📤 [BACKEND REQ] {bold}{request.Method} {request.Path}{request.QueryString}{reset}");
    Console.WriteLine($"{cyan}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{reset}");
    
    await next();
    
    stopwatch.Stop();
    var statusColor = context.Response.StatusCode >= 400 ? red : green;
    var statusEmoji = context.Response.StatusCode >= 400 ? "❌" : "✅";

    Console.WriteLine($"{statusColor}{statusEmoji} [BACKEND RES] {context.Response.StatusCode} | {stopwatch.ElapsedMilliseconds}ms{reset}");
    Console.WriteLine($"{statusColor}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━{reset}\n");
});

app.MapControllers();

app.Run();
