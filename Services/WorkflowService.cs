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
        var documentExists = await _context.Documents.AnyAsync(d => d.Id == documentId);
        if (!documentExists)
            throw new InvalidOperationException("Document not found");

        var workflow = new Workflow
        {
            DocumentId = documentId,
            CreatedById = createdById,
            Status = WorkflowStatus.Pending
        };

        _context.Workflows.Add(workflow);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Workflow {WorkflowId} initiated for document {DocumentId}", workflow.Id, documentId);
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
            WorkflowId = workflowId,
            StepNumber = stepNumber,
            StepType = StepType.Validation,
            Status = WorkflowStatus.Pending
        };

        _context.WorkflowSteps.Add(step);

        // Update workflow status – EF tracks the loaded entity; no explicit Update() needed
        workflow.Status = WorkflowStatus.Validating;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Validation step {StepNumber} added to workflow {WorkflowId}", stepNumber, workflowId);
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
            WorkflowId = workflowId,
            AssignedToId = validatorId,
            IsApproved = null // null = pending decision
        };

        _context.WorkflowApprovals.Add(approval);

        // EF tracks the loaded workflow; no explicit Update() needed
        workflow.Status = WorkflowStatus.AwaitingApproval;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Validator {ValidatorId} assigned to workflow {WorkflowId}", validatorId, workflowId);
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

        // Count pending approvals using local EF tracking state (IsApproved is already set above
        // in memory, so CountAsync on the DB would still see the old value – query the DB for
        // remaining pending approvals excluding the current one).
        var pendingApprovals = await _context.WorkflowApprovals
            .Where(a => a.WorkflowId == workflow.Id && a.Id != approvalId && a.IsApproved == null)
            .CountAsync();

        if (pendingApprovals == 0)
        {
            workflow.Status = WorkflowStatus.Completed;
            workflow.CompletedAt = DateTime.UtcNow;
        }

        // EF tracks both entities; no explicit Update() calls needed
        await _context.SaveChangesAsync();

        _logger.LogInformation("Approval {ApprovalId} for workflow {WorkflowId} accepted", approvalId, workflow.Id);
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

        // EF tracks the loaded entity; no explicit Update() needed
        await _context.SaveChangesAsync();

        _logger.LogInformation("Workflow {WorkflowId} rejected: {Reason}", workflowId, reason);
        return true;
    }

    public async Task<Workflow?> GetWorkflowAsync(Guid workflowId)
    {
        return await _context.Workflows
            .AsNoTracking()
            .Include(w => w.Steps)
            .Include(w => w.Approvals)
            .FirstOrDefaultAsync(w => w.Id == workflowId);
    }

    public async Task<List<Workflow>> GetUserWorkflowsAsync(Guid userId)
    {
        return await _context.Workflows
            .AsNoTracking()
            .Where(w => w.CreatedById == userId || w.Approvals.Any(a => a.AssignedToId == userId))
            .Include(w => w.Steps)
            .Include(w => w.Approvals)
            .ToListAsync();
    }

    public async Task<WorkflowStatus> GetWorkflowStatusAsync(Guid workflowId)
    {
        var status = await _context.Workflows
            .AsNoTracking()
            .Where(w => w.Id == workflowId)
            .Select(w => (WorkflowStatus?)w.Status)
            .FirstOrDefaultAsync();

        return status ?? WorkflowStatus.Pending;
    }
}
