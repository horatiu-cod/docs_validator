using Microsoft.EntityFrameworkCore;
using DocsValidator.Models;

namespace DocsValidator.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Document> Documents { get; set; }
    public DbSet<Workflow> Workflows { get; set; }
    public DbSet<WorkflowStep> WorkflowSteps { get; set; }
    public DbSet<WorkflowApproval> WorkflowApprovals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);
            entity.HasIndex(u => u.Username).IsUnique();
            entity.HasIndex(u => u.Email).IsUnique();
            entity.Property(u => u.Username).HasMaxLength(100).IsRequired();
            entity.Property(u => u.Email).HasMaxLength(255).IsRequired();
            entity.Property(u => u.PasswordHash).HasMaxLength(100).IsRequired();
        });

        // RolePermission configuration
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(rp => rp.Id);
            entity.HasOne(rp => rp.User)
                  .WithMany(u => u.RolePermissions)
                  .HasForeignKey(rp => rp.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Document configuration
        modelBuilder.Entity<Document>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.OriginalFileName).HasMaxLength(255).IsRequired();
            entity.Property(d => d.StoredFileName).HasMaxLength(255).IsRequired();
            entity.Property(d => d.FilePath).HasMaxLength(1000).IsRequired();
            entity.Property(d => d.FileHash).HasMaxLength(100).IsRequired();
            entity.Property(d => d.SignatureOwner).HasMaxLength(255);
            entity.Property(d => d.ClamAVScanResult).HasMaxLength(500);
            entity.HasOne(d => d.UploadedBy)
                  .WithMany()
                  .HasForeignKey(d => d.UploadedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Workflow configuration
        modelBuilder.Entity<Workflow>(entity =>
        {
            entity.HasKey(w => w.Id);
            entity.Property(w => w.RejectionReason).HasMaxLength(1000);
            entity.HasOne(w => w.Document)
                  .WithMany(d => d.Workflows)
                  .HasForeignKey(w => w.DocumentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(w => w.CreatedBy)
                  .WithMany(u => u.CreatedWorkflows)
                  .HasForeignKey(w => w.CreatedById)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // WorkflowStep configuration
        modelBuilder.Entity<WorkflowStep>(entity =>
        {
            entity.HasKey(ws => ws.Id);
            entity.Property(ws => ws.Result).HasMaxLength(2000);
            entity.HasOne(ws => ws.Workflow)
                  .WithMany(w => w.Steps)
                  .HasForeignKey(ws => ws.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // WorkflowApproval configuration
        modelBuilder.Entity<WorkflowApproval>(entity =>
        {
            entity.HasKey(wa => wa.Id);
            entity.Property(wa => wa.ApprovalComment).HasMaxLength(1000);
            // Prevent assigning the same validator twice to the same workflow
            entity.HasIndex(wa => new { wa.WorkflowId, wa.AssignedToId }).IsUnique();
            entity.HasOne(wa => wa.Workflow)
                  .WithMany(w => w.Approvals)
                  .HasForeignKey(wa => wa.WorkflowId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(wa => wa.AssignedTo)
                  .WithMany(u => u.Approvals)
                  .HasForeignKey(wa => wa.AssignedToId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
