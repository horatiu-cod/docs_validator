namespace DocsValidator.Models;

public enum UserRole
{
    Administrator,
    Validator,
    Expert
}

public enum Scope
{
    CanRead,
    CanWrite,
    CanDelete,
    CanUpdate,
    CanValidate
}

public enum Permission
{
    All,
    OnlyHis,
    Assigned
}

public enum WorkflowStatus
{
    Pending,
    Validating,
    AwaitingApproval,
    Approved,
    Rejected,
    Signed,
    Completed
}

public enum StepType
{
    Validation,
    Approval,
    Signing
}

public class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    public ICollection<RolePermission> RolePermissions { get; set; } = [];
    public ICollection<Workflow> CreatedWorkflows { get; set; } = [];
    public ICollection<WorkflowApproval> Approvals { get; set; } = [];
}

public class RolePermission
{
    public Guid Id { get; set; }
    public UserRole Role { get; set; }
    public Scope Scope { get; set; }
    public Permission Permission { get; set; }
    public Guid? UserId { get; set; }
    public User? User { get; set; }
}

public class Document
{
    public Guid Id { get; set; }
    public Guid UploadedById { get; set; }
    public User UploadedBy { get; set; } = null!;
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public bool HasDigitalSignature { get; set; }
    public string? SignatureOwner { get; set; }
    public bool IsCleanAccordingToClamAV { get; set; }
    public string? ClamAVScanResult { get; set; }
    public DateTime? ClamAVScanDate { get; set; }

    public ICollection<Workflow> Workflows { get; set; } = [];
}

public class Workflow
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Document Document { get; set; } = null!;
    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }

    public ICollection<WorkflowStep> Steps { get; set; } = [];
    public ICollection<WorkflowApproval> Approvals { get; set; } = [];
}

public class WorkflowStep
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public int StepNumber { get; set; }
    public StepType StepType { get; set; } = StepType.Validation;
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Pending;
    public string? Result { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}

public class WorkflowApproval
{
    public Guid Id { get; set; }
    public Guid WorkflowId { get; set; }
    public Workflow Workflow { get; set; } = null!;
    public Guid AssignedToId { get; set; }
    public User AssignedTo { get; set; } = null!;
    /// <summary>null = pending, true = approved, false = rejected</summary>
    public bool? IsApproved { get; set; }
    public string? ApprovalComment { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
}
