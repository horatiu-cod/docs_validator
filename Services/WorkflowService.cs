using DocsValidator.Data;
using DocsValidator.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.IO;

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
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _configuration;

    public WorkflowService(ApplicationDbContext context, ILogger<WorkflowService> logger, INotificationService notificationService, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _notificationService = notificationService;
        _configuration = configuration;
    }

    public async Task<Workflow> InitiateWorkflowAsync(Guid documentId, Guid createdById)
    {
        var document = await _context.Documents.Include(d => d.UploadedBy).FirstOrDefaultAsync(d => d.Id == documentId);
        if (document == null)
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

        // Notify document owner that a workflow was initiated
        try
        {
            var notification = new Notification
            {
                Email = document.UploadedBy.Email,
                Subject = "Workflow started for your document",
                Body = $"A workflow (ID: {workflow.Id}) was started for your document '{document.OriginalFileName}'.",
                Type = NotificationType.Generic
            };

            await _notificationService.SendNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send workflow-start notification for workflow {WorkflowId}", workflow.Id);
        }

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

        // Notify the validator that they have been assigned
        try
        {
            var notification = new Notification
            {
                Email = validator.Email,
                Subject = "You have been assigned a document to validate",
                Body = $"You have been assigned to validate workflow {workflowId} for document ID {workflow.DocumentId}.",
                Type = NotificationType.Generic
            };

            await _notificationService.SendNotificationAsync(notification);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send validator assignment notification for workflow {WorkflowId}", workflowId);
        }

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

        // If workflow completed, notify document owner
        if (workflow.Status == WorkflowStatus.Completed)
        {
            try
            {
                var document = await _context.Documents.Include(d => d.UploadedBy).FirstOrDefaultAsync(d => d.Id == workflow.DocumentId);
                if (document != null)
                {
                    // Determine approved recipient (optional override)
                    var approvedRecipient = _configuration["Notifications:ApprovedRecipient"];

                    // Resolve attachment path: if stored path is relative, combine with FileStorage:Path
                    var attachmentPath = document.FilePath ?? string.Empty;
                    if (!Path.IsPathRooted(attachmentPath))
                    {
                        var storagePath = _configuration["FileStorage:Path"] ?? string.Empty;
                        if (!string.IsNullOrEmpty(storagePath))
                        {
                            attachmentPath = Path.Combine(storagePath, attachmentPath);
                        }
                    }

                    await _notificationService.SendDocumentApprovedEmailWithAttachmentAsync(document.UploadedBy.Email, document.OriginalFileName, attachmentPath, approvedRecipient);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send document approved notification for workflow {WorkflowId}", workflow.Id);
            }
        }

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
