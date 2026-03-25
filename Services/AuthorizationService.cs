using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Services;

public interface IAuthorizationService
{
    Task<bool> UserHasPermissionAsync(Guid userId, Scope scope, Permission permission);
    Task<bool> CanAccessDocumentAsync(Guid userId, Guid documentId);
    Task<bool> CanApproveWorkflowAsync(Guid userId, Guid workflowId);
}

public class AuthorizationService : IAuthorizationService
{
    private readonly ApplicationDbContext _context;

    public AuthorizationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<bool> UserHasPermissionAsync(Guid userId, Scope scope, Permission permission)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Administrator has all permissions
        if (user.Role == UserRole.Administrator)
            return true;

        return user.Role switch
        {
            UserRole.Validator => ValidatorPermissions(scope, permission),
            UserRole.Expert    => ExpertPermissions(permission),
            _                  => false
        };
    }

    public async Task<bool> CanAccessDocumentAsync(Guid userId, Guid documentId)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Administrators can access all documents
        if (user.Role == UserRole.Administrator)
            return true;

        // Owner check
        var isOwner = await _context.Documents
            .AnyAsync(d => d.Id == documentId && d.UploadedById == userId);

        if (isOwner) return true;

        // Assigned approver check
        return await _context.WorkflowApprovals
            .AnyAsync(wa => wa.Workflow.DocumentId == documentId && wa.AssignedToId == userId);
    }

    public async Task<bool> CanApproveWorkflowAsync(Guid userId, Guid workflowId)
    {
        return await _context.WorkflowApprovals
            .AnyAsync(wa => wa.WorkflowId == workflowId && wa.AssignedToId == userId);
    }

    // Validator: Read, Update, Validate – but only on Assigned resources
    private static bool ValidatorPermissions(Scope scope, Permission permission)
    {
        var validScopes = new[] { Scope.CanRead, Scope.CanUpdate, Scope.CanValidate };
        return validScopes.Contains(scope) && permission == Permission.Assigned;
    }

    // Expert: all scopes – but only on their own resources
    private static bool ExpertPermissions(Permission permission)
        => permission == Permission.OnlyHis;
}
