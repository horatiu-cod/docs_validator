using DocsValidator.Data;
using DocsValidator.Models;
using DocsValidator.Services;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Endpoints;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/auth").WithName("Authentication");

        group.MapPost("/register", Register).WithName("Register");
        group.MapPost("/login", Login).WithName("Login");
        group.MapGet("/me", GetCurrentUser).WithName("GetCurrentUser").RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        IAuthenticationService authenticationService,
        ApplicationDbContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Username) ||
            string.IsNullOrWhiteSpace(request.Password) ||
            string.IsNullOrWhiteSpace(request.Email))
            return Results.BadRequest("Username, password, and email are required");

        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role))
            return Results.BadRequest("Invalid role");

        var existingUser = await context.Users
            .AnyAsync(u => u.Username == request.Username || u.Email == request.Email);

        if (existingUser)
            return Results.BadRequest("User already exists");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = authenticationService.HashPassword(request.Password),
            Role = role,
            IsActive = true
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var token = authenticationService.GenerateJwtToken(user);
        return Results.Created($"/api/auth/me", new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            token
        });
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        IAuthenticationService authenticationService,
        ApplicationDbContext context)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return Results.BadRequest("Username and password are required");

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Results.Unauthorized();

        // Check active status before running the bcrypt hash comparison
        if (!user.IsActive)
            return Results.BadRequest("User account is inactive");

        if (!authenticationService.VerifyPassword(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        var token = authenticationService.GenerateJwtToken(user);
        return Results.Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            token
        });
    }

    private static async Task<IResult> GetCurrentUser(
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return Results.NotFound();

        return Results.Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt
        });
    }
}

public record RegisterRequest(string Username, string Email, string Password, string Role);
public record LoginRequest(string Username, string Password);
