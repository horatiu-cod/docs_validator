using System.Security.Claims;
using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Endpoints;

public static class AdminEndpoints
{
    public static void MapAdminEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/admin").WithName("Administration");

        // Get all users
        group.MapGet("/users", GetAllUsers)
            .WithName("GetAllUsers")
            .RequireAuthorization();

        // Get user details
        group.MapGet("/users/{userId}", GetUserDetails)
            .WithName("GetUserDetails")
            .RequireAuthorization();

        // Deactivate user
        group.MapPost("/users/{userId}/deactivate", DeactivateUser)
            .WithName("DeactivateUser")
            .RequireAuthorization();

        // Activate user
        group.MapPost("/users/{userId}/activate", ActivateUser)
            .WithName("ActivateUser")
            .RequireAuthorization();

        // Get all workflows
        group.MapGet("/workflows", GetAllWorkflows)
            .WithName("GetAllWorkflows")
            .RequireAuthorization();

        // Get all documents
        group.MapGet("/documents", GetAllDocuments)
            .WithName("GetAllDocuments")
            .RequireAuthorization();
    }

    private static async Task<IResult> GetAllUsers(
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var users = await context.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.Email,
                u.Role,
                u.IsActive,
                u.CreatedAt
            })
            .ToListAsync();

        return Results.Ok(users);
    }

    private static async Task<IResult> GetUserDetails(
        Guid userId,
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var user = await context.Users
            .Include(u => u.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
            return Results.NotFound();

        return Results.Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            Permissions = user.RolePermissions.Select(p => new
            {
                p.Scope,
                p.Permission
            })
        });
    }

    private static async Task<IResult> DeactivateUser(
        Guid userId,
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Results.NotFound();

        user.IsActive = false;
        context.Users.Update(user);
        await context.SaveChangesAsync();

        return Results.Ok(new { message = "User deactivated successfully" });
    }

    private static async Task<IResult> ActivateUser(
        Guid userId,
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null)
            return Results.NotFound();

        user.IsActive = true;
        context.Users.Update(user);
        await context.SaveChangesAsync();

        return Results.Ok(new { message = "User activated successfully" });
    }

    private static async Task<IResult> GetAllWorkflows(
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var workflows = await context.Workflows
            .Include(w => w.Document)
            .Include(w => w.Approvals)
            .Select(w => new
            {
                w.Id,
                w.DocumentId,
                DocumentName = w.Document.OriginalFileName,
                w.Status,
                w.CreatedAt,
                w.CompletedAt,
                ApprovalCount = w.Approvals.Count
            })
            .ToListAsync();

        return Results.Ok(workflows);
    }

    private static async Task<IResult> GetAllDocuments(
        HttpContext httpContext,
        ApplicationDbContext context)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var documents = await context.Documents
            .Include(d => d.UploadedBy)
            .Select(d => new
            {
                d.Id,
                d.OriginalFileName,
                d.StoredFileName,
                d.FileSize,
                d.UploadedAt,
                UploadedBy = d.UploadedBy.Username,
                d.IsCleanAccordingToClamAV,
                d.ClamAVScanDate
            })
            .ToListAsync();

        return Results.Ok(documents);
    }
}
