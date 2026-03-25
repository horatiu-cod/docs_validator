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
            .Include(u => u.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Administrator has all permissions
        if (user.Role == UserRole.Administrator)
            return true;

        // Check role-based permissions
        var hasPermission = user.Role switch
        {
            UserRole.Validator => ValidatorPermissions(user.RolePermissions, scope, permission),
            UserRole.Expert => ExpertPermissions(user.RolePermissions, scope, permission),
            _ => false
        };

        return hasPermission;
    }

    public async Task<bool> CanAccessDocumentAsync(Guid userId, Guid documentId)
    {
        var user = await _context.Users
            .Include(u => u.RolePermissions)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return false;

        // Administrators can access all documents
        if (user.Role == UserRole.Administrator)
            return true;

        // Check if user is the owner or assigned to approve
        var document = await _context.Documents
            .FirstOrDefaultAsync(d => d.Id == documentId);

        if (document?.UploadedById == userId)
            return true;

        var isApprover = await _context.WorkflowApprovals
            .AnyAsync(wa => wa.Workflow.DocumentId == documentId && wa.AssignedToId == userId);

        return isApprover;
    }

    public async Task<bool> CanApproveWorkflowAsync(Guid userId, Guid workflowId)
    {
        var approval = await _context.WorkflowApprovals
            .FirstOrDefaultAsync(wa => wa.WorkflowId == workflowId && wa.AssignedToId == userId);

        return approval != null;
    }

    private bool ValidatorPermissions(ICollection<RolePermission> permissions, Scope scope, Permission permission)
    {
        // Validator: Assigned:CanRead, Assigned:CanUpdate, Assigned:CanValidate
        var validScopes = new[] { Scope.CanRead, Scope.CanUpdate, Scope.CanValidate };
        return validScopes.Contains(scope) && permission == Permission.Assigned;
    }

    private bool ExpertPermissions(ICollection<RolePermission> permissions, Scope scope, Permission permission)
    {
        // Expert: OnlyHis:CanRead, OnlyHis:CanUpdate, OnlyHis:CanDelete, OnlyHis:CanWrite, OnlyHis:CanValidate
        return permission == Permission.OnlyHis;
    }
}
