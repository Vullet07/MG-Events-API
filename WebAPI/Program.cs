using Data;
using Data.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Context;
using Services.AuthUserService;
using Services.JwtService;
using Services.PasswordResetService;
using Services.PasswordResetService.EmailService;
using Services.Seeding;
using System.Text;
using System.Text.Json.Serialization;
using WebAPI.Controllers;
using WebAPI.Middleware;
using WebAPI.Services.Accounts;
using WebAPI.Services.Security;
using WebAPI.Services.Pins;
using WebAPI.Services.Quotas;

var builder = WebApplication.CreateBuilder(args);

if (string.IsNullOrWhiteSpace(builder.Environment.WebRootPath))
{
    builder.Environment.WebRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
}

// SERVICES

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOpenApi();

builder.Services.AddSwaggerGen(options =>
{
    options.UseInlineDefinitionsForEnums();
});

// SERILOG

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .CreateLogger();
builder.Host.UseSerilog();

// CORS

var allowedFrontendOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?.Where(origin => !string.IsNullOrWhiteSpace(origin))
    .ToArray();

if (allowedFrontendOrigins == null || allowedFrontendOrigins.Length == 0)
{
    allowedFrontendOrigins = ["http://localhost:5173"];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins(allowedFrontendOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Database

builder.Services.AddDbContext<AppDbContext>(opts =>
    opts.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Jwt settings

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("Missing Jwt configuration.");
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

// Authentication

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = jwtSettings.Issuer,
            ValidAudience = jwtSettings.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSettings.Key))
        };
    });

builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter JWT token like: Bearer {your token}"
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

// Email service

builder.Services.Configure<SmtpSettings>(
    builder.Configuration.GetSection("Smtp"));

builder.Services.AddScoped<IEmailService, EmailService>();

// Serialization

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()
        );
    });


// Interface registrations

builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IAuthUserService, AuthUserService>();
builder.Services.AddScoped<IDataSeeder, DataSeeder>();
builder.Services.AddScoped<IUserLifecycleService, UserLifecycleService>();
builder.Services.AddSingleton<ILoginAttemptQuarantineService, LoginAttemptQuarantineService>();
builder.Services.AddScoped<IUserActionQuotaService, UserActionQuotaService>();

// Other services

builder.Services.AddHttpContextAccessor();
builder.Services.AddAuthorization();
builder.Services.AddMemoryCache();
builder.Services.AddHostedService<ExpiredStudentCleanupHostedService>();
builder.Services.AddHostedService<ResolvedPinCleanupHostedService>();
builder.Services.AddHostedService<UserActionQuotaCleanupHostedService>();

var app = builder.Build();

// Configure the HTTP request pipeline.

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    var forceReseed =
        builder.Configuration.GetValue<bool>("Seeding:ForceReseedOnStartup") ||
        string.Equals(Environment.GetEnvironmentVariable("MG_EVENTS_FORCE_RESEED"), "1", StringComparison.OrdinalIgnoreCase);

    var hasUsers = await db.Users.IgnoreQueryFilters().AnyAsync();
    if (forceReseed || !hasUsers)
    {
        await seeder.SeedAsync();
    }

}

Directory.CreateDirectory(Path.Combine(app.Environment.WebRootPath!, "uploads"));

app.UseCors("AllowFrontend");

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enrich logs with authenticated user info
app.Use(async (context, next) =>
{
    var authUser = context.RequestServices.GetService<IAuthUserService>();
    using (LogContext.PushProperty("UserId", authUser?.Id))
    using (LogContext.PushProperty("Username", authUser?.Username))
    using (LogContext.PushProperty("UserRole", authUser?.Role?.ToString()))
    {
        await next();
    }
});

// HTTP request logging
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
});

app.UseAuthentication();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
};

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthorization();

app.UseMiddleware<BanMiddleware>();

app.MapControllers();

app.Run();


