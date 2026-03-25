using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Endpoints;

public static class AdminEndpoints
{
    /// <summary>
    /// Policy name for the "Administrator-only" authorization requirement.
    /// Declared here and registered in Program.cs so all admin routes use it
    /// instead of repeating ad-hoc role string comparisons in every handler.
    /// </summary>
    public const string AdminPolicy = "AdminOnly";

    public static void MapAdminEndpoints(this WebApplication app)
    {
        // Apply the AdminOnly policy to the entire group – no per-handler checks needed
        var group = app.MapGroup("/api/admin")
                       .WithName("Administration")
                       .RequireAuthorization(AdminPolicy);

        group.MapGet("/users", GetAllUsers).WithName("GetAllUsers");
        group.MapGet("/users/{userId}", GetUserDetails).WithName("GetUserDetails");
        group.MapPost("/users/{userId}/deactivate", DeactivateUser).WithName("DeactivateUser");
        group.MapPost("/users/{userId}/activate", ActivateUser).WithName("ActivateUser");
        group.MapGet("/workflows", GetAllWorkflows).WithName("GetAllWorkflows");
        group.MapGet("/documents", GetAllDocuments).WithName("GetAllDocuments");
    }

    private static async Task<IResult> GetAllUsers(ApplicationDbContext context)
    {
        var users = await context.Users
            .AsNoTracking()
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

    private static async Task<IResult> GetUserDetails(Guid userId, ApplicationDbContext context)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(u => u.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return Results.NotFound();

        return Results.Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            Permissions = user.RolePermissions.Select(p => new { p.Scope, p.Permission })
        });
    }

    private static async Task<IResult> DeactivateUser(Guid userId, ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Results.NotFound();

        user.IsActive = false;
        await context.SaveChangesAsync();

        return Results.Ok(new { message = "User deactivated successfully" });
    }

    private static async Task<IResult> ActivateUser(Guid userId, ApplicationDbContext context)
    {
        var user = await context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null) return Results.NotFound();

        user.IsActive = true;
        await context.SaveChangesAsync();

        return Results.Ok(new { message = "User activated successfully" });
    }

    private static async Task<IResult> GetAllWorkflows(ApplicationDbContext context)
    {
        var workflows = await context.Workflows
            .AsNoTracking()
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

    private static async Task<IResult> GetAllDocuments(ApplicationDbContext context)
    {
        var documents = await context.Documents
            .AsNoTracking()
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
