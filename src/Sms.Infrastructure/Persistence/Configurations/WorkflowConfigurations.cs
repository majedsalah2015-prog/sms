using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sms.Domain.Workflow;

namespace Sms.Infrastructure.Persistence.Configurations
{
    // wf schema per docs/Database/03 §B ("Workflow engine — own schema wf, 5
    // tables"). State FKs are Restrict: definitions are deactivated, never
    // deleted (BR-WF-008), so cascades would only create SQL Server
    // multiple-cascade-path problems.

    public class WorkflowDefinitionConfiguration : IEntityTypeConfiguration<WorkflowDefinition>
    {
        public void Configure(EntityTypeBuilder<WorkflowDefinition> builder)
        {
            builder.ToTable("WorkflowDefinition", "wf");
            builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
            builder.Property(x => x.EntityTypeName).HasMaxLength(100).IsRequired();
            builder.HasIndex(x => new { x.SchoolId, x.Code, x.Version }).IsUnique();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
            builder.HasMany(x => x.States).WithOne().HasForeignKey(s => s.WorkflowDefinitionId);
            builder.HasMany(x => x.Transitions).WithOne().HasForeignKey(t => t.WorkflowDefinitionId);
        }
    }

    public class WorkflowStateConfiguration : IEntityTypeConfiguration<WorkflowState>
    {
        public void Configure(EntityTypeBuilder<WorkflowState> builder)
        {
            builder.ToTable("WorkflowState", "wf");
            builder.Property(x => x.Code).HasMaxLength(40).IsRequired();
            builder.OwnsOne(x => x.Name, name =>
            {
                name.Property(n => n.NameAr).HasColumnName("NameAr").HasMaxLength(200).IsRequired();
                name.Property(n => n.NameEn).HasColumnName("NameEn").HasMaxLength(200).IsRequired();
            });
        }
    }

    public class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
    {
        public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
        {
            builder.ToTable("WorkflowTransition", "wf");
            builder.Property(x => x.MinRoutingValue).HasPrecision(18, 4);
            builder.Property(x => x.MaxRoutingValue).HasPrecision(18, 4);
            builder.Property(x => x.PermissionModuleCode).HasMaxLength(30);
            builder.Property(x => x.PermissionScreenCode).HasMaxLength(60);
            builder.HasOne<WorkflowState>().WithMany().HasForeignKey(x => x.FromStateId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkflowState>().WithMany().HasForeignKey(x => x.ToStateId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class WorkflowInstanceConfiguration : IEntityTypeConfiguration<WorkflowInstance>
    {
        public void Configure(EntityTypeBuilder<WorkflowInstance> builder)
        {
            builder.ToTable("WorkflowInstance", "wf");
            builder.Property(x => x.EntityTypeName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.BusinessKey).HasMaxLength(60);
            builder.Property(x => x.RoutingValue).HasPrecision(18, 4);
            builder.HasIndex(x => new { x.EntityTypeName, x.EntityId });
            builder.HasOne<WorkflowDefinition>().WithMany().HasForeignKey(x => x.WorkflowDefinitionId);
            builder.HasOne<WorkflowState>().WithMany().HasForeignKey(x => x.CurrentStateId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
    {
        public void Configure(EntityTypeBuilder<WorkflowStep> builder)
        {
            builder.ToTable("WorkflowStep", "wf");
            builder.Property(x => x.Reason).HasMaxLength(500);
            builder.HasIndex(x => new { x.WorkflowInstanceId, x.OccurredAtUtc });
            builder.HasOne<WorkflowInstance>().WithMany().HasForeignKey(x => x.WorkflowInstanceId);
            builder.HasOne<WorkflowState>().WithMany().HasForeignKey(x => x.FromStateId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne<WorkflowState>().WithMany().HasForeignKey(x => x.ToStateId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
