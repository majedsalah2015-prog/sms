using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Security;
using Sms.Domain.Workflow;

namespace Sms.Infrastructure.Persistence
{
    /// <summary>
    /// The product's concrete context. Module entity sets accumulate here;
    /// mapping details live in configuration classes (docs/Database/01 §5).
    /// </summary>
    public class AppDbContext : SmsDbContext
    {
        public AppDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser currentUser, IClock clock, IAuditContext? auditContext = null)
            : base(options, tenant, currentUser, clock, auditContext)
        {
        }

        public DbSet<UserAccount> UserAccounts => Set<UserAccount>();

        public DbSet<Role> Roles => Set<Role>();

        public DbSet<Permission> Permissions => Set<Permission>();

        public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

        public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

        public DbSet<ScopeGrant> ScopeGrants => Set<ScopeGrant>();

        public DbSet<WorkflowDefinition> WorkflowDefinitions => Set<WorkflowDefinition>();

        public DbSet<WorkflowState> WorkflowStates => Set<WorkflowState>();

        public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

        public DbSet<WorkflowInstance> WorkflowInstances => Set<WorkflowInstance>();

        public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

            // Base runs last so tenant/soft-active filters cover every entity
            // the configurations added.
            base.OnModelCreating(modelBuilder);
        }
    }
}
