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
        modelBuilder.Entity<User>()
            .HasKey(u => u.Id);
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email).IsUnique();

        // RolePermission configuration
        modelBuilder.Entity<RolePermission>()
            .HasKey(rp => rp.Id);
        modelBuilder.Entity<RolePermission>()
            .HasOne(rp => rp.User)
            .WithMany(u => u.RolePermissions)
            .HasForeignKey(rp => rp.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Document configuration
        modelBuilder.Entity<Document>()
            .HasKey(d => d.Id);
        modelBuilder.Entity<Document>()
            .HasOne(d => d.UploadedBy)
            .WithMany()
            .HasForeignKey(d => d.UploadedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Workflow configuration
        modelBuilder.Entity<Workflow>()
            .HasKey(w => w.Id);
        modelBuilder.Entity<Workflow>()
            .HasOne(w => w.Document)
            .WithMany(d => d.Workflows)
            .HasForeignKey(w => w.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Workflow>()
            .HasOne(w => w.CreatedBy)
            .WithMany(u => u.CreatedWorkflows)
            .HasForeignKey(w => w.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkflowStep configuration
        modelBuilder.Entity<WorkflowStep>()
            .HasKey(ws => ws.Id);
        modelBuilder.Entity<WorkflowStep>()
            .HasOne(ws => ws.Workflow)
            .WithMany(w => w.Steps)
            .HasForeignKey(ws => ws.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);

        // WorkflowApproval configuration
        modelBuilder.Entity<WorkflowApproval>()
            .HasKey(wa => wa.Id);
        modelBuilder.Entity<WorkflowApproval>()
            .HasOne(wa => wa.Workflow)
            .WithMany(w => w.Approvals)
            .HasForeignKey(wa => wa.WorkflowId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<WorkflowApproval>()
            .HasOne(wa => wa.AssignedTo)
            .WithMany(u => u.Approvals)
            .HasForeignKey(wa => wa.AssignedToId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
