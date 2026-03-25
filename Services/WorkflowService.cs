using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;

namespace DocsValidator.Services;

public interface IWorkflowService
{
    Task<Workflow> InitiateWorkflowAsync(Guid documentId, Guid createdById);
    Task<WorkflowStep> AddValidationStepAsync(Guid workflowId);
    Task<WorkflowApproval> AssignValidatorAsync(Guid workflowId, Guid validatorId);
    Task<bool> ApproveWorkflowAsync(Guid approvalId, string comment);
    Task<bool> RejectWorkflowAsync(Guid workflowId, string reason);
    Task<Workflow?> GetWorkflowAsync(Guid workflowId);
    Task<List<Workflow>> GetUserWorkflowsAsync(Guid userId);
    Task<WorkflowStatus> GetWorkflowStatusAsync(Guid workflowId);
}

public class WorkflowService : IWorkflowService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<WorkflowService> _logger;

    public WorkflowService(ApplicationDbContext context, ILogger<WorkflowService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<Workflow> InitiateWorkflowAsync(Guid documentId, Guid createdById)
    {
        var document = await _context.Documents.FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
            throw new InvalidOperationException("Document not found");

        var workflow = new Workflow
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            CreatedById = createdById,
            Status = WorkflowStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Workflow {workflow.Id} initiated for document {documentId}");
        return workflow;
    }

    public async Task<WorkflowStep> AddValidationStepAsync(Guid workflowId)
    {
        var workflow = await _context.Workflows
            .Include(w => w.Steps)
            .FirstOrDefaultAsync(w => w.Id == workflowId);

        if (workflow == null)
            throw new InvalidOperationException("Workflow not found");

        var stepNumber = workflow.Steps.Count + 1;
        var step = new WorkflowStep
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            StepNumber = stepNumber,
            StepType = "Validation",
            Status = WorkflowStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _context.WorkflowSteps.Add(step);

        // Update workflow status
        workflow.Status = WorkflowStatus.Validating;
        _context.Workflows.Update(workflow);

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Validation step {stepNumber} added to workflow {workflowId}");
        return step;
    }

    public async Task<WorkflowApproval> AssignValidatorAsync(Guid workflowId, Guid validatorId)
    {
        var workflow = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow == null)
            throw new InvalidOperationException("Workflow not found");

        var validator = await _context.Users.FirstOrDefaultAsync(u => u.Id == validatorId);
        if (validator == null)
            throw new InvalidOperationException("Validator not found");

        if (validator.Role != UserRole.Validator && validator.Role != UserRole.Administrator)
            throw new InvalidOperationException("User is not a validator");

        var approval = new WorkflowApproval
        {
            Id = Guid.NewGuid(),
            WorkflowId = workflowId,
            AssignedToId = validatorId,
            AssignedAt = DateTime.UtcNow,
            IsApproved = false
        };

        _context.WorkflowApprovals.Add(approval);

        // Update workflow status
        workflow.Status = WorkflowStatus.AwaitingApproval;
        _context.Workflows.Update(workflow);

        await _context.SaveChangesAsync();

        _logger.LogInformation($"Validator {validatorId} assigned to workflow {workflowId}");
        return approval;
    }

    public async Task<bool> ApproveWorkflowAsync(Guid approvalId, string comment)
    {
        var approval = await _context.WorkflowApprovals
            .Include(a => a.Workflow)
            .FirstOrDefaultAsync(a => a.Id == approvalId);

        if (approval == null)
            return false;

        approval.IsApproved = true;
        approval.ApprovalComment = comment;
        approval.ApprovedAt = DateTime.UtcNow;

        var workflow = approval.Workflow;

        // Check if all approvals are complete
        var pendingApprovals = await _context.WorkflowApprovals
            .Where(a => a.WorkflowId == workflow.Id && !a.IsApproved)
            .CountAsync();

        if (pendingApprovals == 0)
        {
            workflow.Status = WorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
        }

        _context.WorkflowApprovals.Update(approval);
        _context.Workflows.Update(workflow);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Workflow {workflow.Id} approved");
        return true;
    }

    public async Task<bool> RejectWorkflowAsync(Guid workflowId, string reason)
    {
        var workflow = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        if (workflow == null)
            return false;

        workflow.Status = WorkflowStatus.Rejected;
        workflow.RejectionReason = reason;
        workflow.CompletedAt = DateTime.UtcNow;

        _context.Workflows.Update(workflow);
        await _context.SaveChangesAsync();

        _logger.LogInformation($"Workflow {workflowId} rejected: {reason}");
        return true;
    }

    public async Task<Workflow?> GetWorkflowAsync(Guid workflowId)
    {
        return await _context.Workflows
            .Include(w => w.Steps)
            .Include(w => w.Approvals)
            .FirstOrDefaultAsync(w => w.Id == workflowId);
    }

    public async Task<List<Workflow>> GetUserWorkflowsAsync(Guid userId)
    {
        return await _context.Workflows
            .Where(w => w.CreatedById == userId || w.Approvals.Any(a => a.AssignedToId == userId))
            .Include(w => w.Steps)
            .Include(w => w.Approvals)
            .ToListAsync();
    }

    public async Task<WorkflowStatus> GetWorkflowStatusAsync(Guid workflowId)
    {
        var workflow = await _context.Workflows.FirstOrDefaultAsync(w => w.Id == workflowId);
        return workflow?.Status ?? WorkflowStatus.Pending;
    }
}
