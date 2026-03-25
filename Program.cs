using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using DocsValidator.Data;
using DocsValidator.Endpoints;
using DocsValidator.Services;
using DocsValidator.Settings;

var builder = WebApplication.CreateBuilder(args);

// ── Logging ──────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ── JWT / Authentication ──────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("Jwt"));

var jwtSettings = builder.Configuration.GetSection("Jwt").Get<JwtSettings>()
    ?? throw new InvalidOperationException("JWT settings are not configured");

if (string.IsNullOrEmpty(jwtSettings.SecretKey))
    throw new InvalidOperationException("JWT SecretKey is not configured");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
        ValidateIssuer           = true,
        ValidIssuer              = jwtSettings.Issuer,
        ValidateAudience         = true,
        ValidAudience            = jwtSettings.Audience,
        ValidateLifetime         = true,
        ClockSkew                = TimeSpan.Zero
    };
});

// ── Authorization ──────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    // Administrator-only policy – used by the admin route group
    options.AddPolicy(AdminEndpoints.AdminPolicy, policy =>
        policy.RequireRole(nameof(DocsValidator.Models.UserRole.Administrator)));
});

// ── Application services ──────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();
builder.Services.AddScoped<IDigitalSignatureValidationService, DigitalSignatureValidationService>();
builder.Services.AddScoped<IDocumentStorageService, DocumentStorageService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddHttpClient<IClamAVService, ClamAVService>();

// ── CORS ───────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    // Dev-only permissive policy
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    // Production-ready restrictive policy (configure origins via appsettings)
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
    options.AddPolicy("RestrictedCors", policy =>
        policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader());
});

// ── OpenAPI (built-in .NET 9) ──────────────────────────────────────────────────
// builder.Services.AddOpenApi();

// ─────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ── Middleware ─────────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    // app.MapOpenApi();              // GET /openapi/v1.json
    app.UseCors("AllowAll");
}
else
{
    app.UseCors("RestrictedCors");
}

// ── Upload directory ───────────────────────────────────────────────────────────
var uploadPath = builder.Configuration["FileStorage:Path"];
if (string.IsNullOrEmpty(uploadPath))
{
    app.Logger.LogWarning("FileStorage:Path is not configured – file uploads will fail");
}
else
{
    Directory.CreateDirectory(Path.Combine(uploadPath, "uploads"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ──────────────────────────────────────────────────────────────────
app.MapAuthenticationEndpoints();
app.MapDocumentEndpoints();
app.MapWorkflowEndpoints();
app.MapAdminEndpoints();

// ── Database migration ─────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "Database migration failed. The application cannot start");
        throw;
    }
}

await app.RunAsync();

// Expose Program class for integration testing
public partial class Program { }
