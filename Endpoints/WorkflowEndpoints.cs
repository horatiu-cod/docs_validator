using DocsValidator.Models;
using DocsValidator.Services;

namespace DocsValidator.Endpoints;

public static class WorkflowEndpoints
{
    public static void MapWorkflowEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/workflows").WithName("Workflows");

        group.MapPost("/{workflowId}/assign-validator", AssignValidator)
            .WithName("AssignValidator")
            .RequireAuthorization();

        group.MapGet("/{workflowId}", GetWorkflow)
            .WithName("GetWorkflow")
            .RequireAuthorization();

        group.MapGet("/", ListUserWorkflows)
            .WithName("ListUserWorkflows")
            .RequireAuthorization();

        group.MapPost("/approvals/{approvalId}/approve", ApproveWorkflow)
            .WithName("ApproveWorkflow")
            .RequireAuthorization();

        group.MapPost("/{workflowId}/reject", RejectWorkflow)
            .WithName("RejectWorkflow")
            .RequireAuthorization();

        group.MapGet("/{workflowId}/status", GetWorkflowStatus)
            .WithName("GetWorkflowStatus")
            .RequireAuthorization();
    }

    private static async Task<IResult> AssignValidator(
        Guid workflowId,
        HttpContext httpContext,
        AssignValidatorRequest request,
        IWorkflowService workflowService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var userRole = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

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
        IWorkflowService workflowService)
    {
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var workflow = await workflowService.GetWorkflowAsync(workflowId);
        if (workflow == null) return Results.NotFound();

        // Allow access to the creator, any assigned approver, or an administrator
        var userRole = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
        var isAdmin = userRole == UserRole.Administrator.ToString();
        var canAccess = isAdmin ||
                        workflow.CreatedById == userId ||
                        workflow.Approvals.Any(a => a.AssignedToId == userId);

        if (!canAccess) return Results.Forbid();

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
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        var workflows = await workflowService.GetUserWorkflowsAsync(userId.Value);
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
        var userId = EndpointHelpers.GetUserId(httpContext);
        if (userId is null) return Results.Unauthorized();

        if (!await authorizationService.CanApproveWorkflowAsync(userId.Value, approvalId))
            return Results.Forbid();

        var success = await workflowService.ApproveWorkflowAsync(approvalId, request.Comment ?? string.Empty);
        if (!success) return Results.NotFound();

        return Results.Ok(new { message = "Workflow approved successfully" });
    }

    private static async Task<IResult> RejectWorkflow(
        Guid workflowId,
        HttpContext httpContext,
        RejectWorkflowRequest request,
        IWorkflowService workflowService)
    {
        var userRole = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        // Only administrators can reject workflows
        if (userRole != UserRole.Administrator.ToString())
            return Results.Forbid();

        var success = await workflowService.RejectWorkflowAsync(workflowId, request.Reason);
        if (!success) return Results.NotFound();

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
