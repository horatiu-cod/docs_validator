using System.Security.Claims;
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

        // Register a new user
        group.MapPost("/register", Register)
            .WithName("Register");

        // Login
        group.MapPost("/login", Login)
            .WithName("Login");

        // Get current user
        group.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .RequireAuthorization();
    }

    private static async Task<IResult> Register(
        HttpContext httpContext,
        RegisterRequest request,
        IAuthenticationService authenticationService,
        ApplicationDbContext context)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password) || string.IsNullOrEmpty(request.Email))
            return Results.BadRequest("Username, password, and email are required");

        // Check if user already exists
        var existingUser = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username || u.Email == request.Email);
        if (existingUser != null)
            return Results.BadRequest("User already exists");

        if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
            return Results.BadRequest("Invalid role");

        var user = new User
        {
            Id = Guid.NewGuid(),
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
        HttpContext httpContext,
        LoginRequest request,
        IAuthenticationService authenticationService,
        ApplicationDbContext context)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            return Results.BadRequest("Username and password are required");

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null)
            return Results.Unauthorized();

        if (!authenticationService.VerifyPassword(request.Password, user.PasswordHash))
            return Results.Unauthorized();

        if (!user.IsActive)
            return Results.BadRequest("User account is inactive");

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
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Results.NotFound();

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
