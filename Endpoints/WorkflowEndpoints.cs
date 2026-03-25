using System.Security.Claims;
using DocsValidator.Models;
using DocsValidator.Services;

namespace DocsValidator.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workflows").WithName("Workflows");

        // Assign validator to workflow
        group.MapPost("/{workflowId}/assign-validator", AssignValidator)
            .WithName("AssignValidator")
            .RequireAuthorization();

        // Get workflow details
        group.MapGet("/{workflowId}", GetWorkflow)
            .WithName("GetWorkflow")
            .RequireAuthorization();

        // List user workflows
        group.MapGet("/", ListUserWorkflows)
            .WithName("ListUserWorkflows")
            .RequireAuthorization();

        // Approve workflow
        group.MapPost("/approvals/{approvalId}/approve", ApproveWorkflow)
            .WithName("ApproveWorkflow")
            .RequireAuthorization();

        // Reject workflow
        group.MapPost("/{workflowId}/reject", RejectWorkflow)
            .WithName("RejectWorkflow")
            .RequireAuthorization();

        // Get workflow status
        group.MapGet("/{workflowId}/status", GetWorkflowStatus)
            .WithName("GetWorkflowStatus")
            .RequireAuthorization();
    }

    private static async Task<IResult> AssignValidator(
        Guid workflowId,
        HttpContext httpContext,
        AssignValidatorRequest request,
        IWorkflowService workflowService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;

        // Only administrators and experts can assign validators
        if (userRole != UserRole.Administrator.ToString() && userRole != UserRole.Expert.ToString())
            return Results.Forbid();

        try
        {
            var approval = await workflowService.AssignValidatorAsync(workflowId, request.ValidatorId);
            return Results.Ok(new
            {
                approval.Id,
                approval.WorkflowId,
                approval.AssignedToId,
                approval.AssignedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> GetWorkflow(
        Guid workflowId,
        HttpContext httpContext,
        IWorkflowService workflowService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var workflow = await workflowService.GetWorkflowAsync(workflowId);
        if (workflow == null)
            return Results.NotFound();

        // Check if user can access this workflow
        var canAccess = workflow.CreatedById == userId ||
                       workflow.Approvals.Any(a => a.AssignedToId == userId);
        if (!canAccess)
        {
            var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRole != UserRole.Administrator.ToString())
                return Results.Forbid();
        }

        return Results.Ok(new
        {
            workflow.Id,
            workflow.DocumentId,
            workflow.CreatedById,
            workflow.Status,
            workflow.CreatedAt,
            workflow.CompletedAt,
            workflow.RejectionReason,
            Steps = workflow.Steps.Select(s => new
            {
                s.Id,
                s.StepNumber,
                s.StepType,
                s.Status,
                s.Result
            }),
            Approvals = workflow.Approvals.Select(a => new
            {
                a.Id,
                a.AssignedToId,
                a.IsApproved,
                a.ApprovalComment,
                a.AssignedAt,
                a.ApprovedAt
            })
        });
    }

    private static async Task<IResult> ListUserWorkflows(
        HttpContext httpContext,
        IWorkflowService workflowService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var workflows = await workflowService.GetUserWorkflowsAsync(userId);
        return Results.Ok(workflows.Select(w => new
        {
            w.Id,
            w.DocumentId,
            w.Status,
            w.CreatedAt,
            w.CompletedAt
        }));
    }

    private static async Task<IResult> ApproveWorkflow(
        Guid approvalId,
        HttpContext httpContext,
        ApproveWorkflowRequest request,
        IWorkflowService workflowService,
        IAuthorizationService authorizationService)
    {
        var userId = Guid.Parse(httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty);

        var canApprove = await authorizationService.CanApproveWorkflowAsync(userId, approvalId);
        if (!canApprove)
            return Results.Forbid();

        var success = await workflowService.ApproveWorkflowAsync(approvalId, request.Comment ?? string.Empty);
        if (!success)
            return Results.NotFound();

        return Results.Ok(new { message = "Workflow approved successfully" });
    }

    private static async Task<IResult> RejectWorkflow(
        Guid workflowId,
        HttpContext httpContext,
        IWorkflowService workflowService)
    {
        var userRole = httpContext.User.FindFirst(ClaimTypes.Role)?.Value;

        // Only administrators can reject workflows
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var request = new RejectWorkflowRequest(Reason :"Rejected by administrator");
        var success = await workflowService.RejectWorkflowAsync(workflowId, request.Reason);
        if (!success)
            return Results.NotFound();

        return Results.Ok(new { message = "Workflow rejected successfully" });
    }

    private static async Task<IResult> GetWorkflowStatus(
        Guid workflowId,
        HttpContext httpContext,
        IWorkflowService workflowService)
    {
        var status = await workflowService.GetWorkflowStatusAsync(workflowId);
        return Results.Ok(new { status = status.ToString() });
    }
}

public record AssignValidatorRequest(Guid ValidatorId);
public record ApproveWorkflowRequest(string? Comment);
public record RejectWorkflowRequest(string Reason);
