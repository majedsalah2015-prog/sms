using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence.Configurations;

namespace Sms.Infrastructure.Persistence
{
    /// <summary>
    /// Base context enforcing the cross-cutting persistence rules centrally
    /// (docs/Database/01 §5 — never per screen or module):
    ///  - tenant query filter on every ISchoolScoped entity (ADR-2, BR-GLB-010),
    ///  - opt-in soft-active filter on ISoftActiveFiltered entities (DB-6),
    ///  - audit stamping via IClock/ICurrentUser (BR-GLB-007),
    ///  - cross-school write guard (BR-GLB-010),
    ///  - hard-delete guard on master data (ADR-7, BR-GLB-005),
    ///  - E-004: T1–T3 audit capture in the same transaction (doc 07, BR-AUD-003)
    ///    and the append-only guard on audit storage (BR-AUD-001).
    /// </summary>
    public class SmsDbContext : DbContext
    {
        private readonly ITenantContext _tenant;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly IAuditContext? _auditContext;

        public SmsDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser currentUser, IClock clock, IAuditContext? auditContext = null)
            : base(options)
        {
            _tenant = tenant;
            _currentUser = currentUser;
            _clock = clock;
            _auditContext = auditContext;
        }

        /// <summary>Referenced inside query filters so EF parameterizes it per context instance.</summary>
        public int CurrentSchoolId => _tenant.SchoolId;

        // The audit store lives on the base context so every derived context
        // (product and tests alike) captures under the same rules.
        public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

        public DbSet<IntegrityCheckpoint> IntegrityCheckpoints => Set<IntegrityCheckpoint>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            new AuditEntryConfiguration().Configure(modelBuilder.Entity<AuditEntry>());
            new IntegrityCheckpointConfiguration().Configure(modelBuilder.Entity<IntegrityCheckpoint>());

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var filter = BuildQueryFilter(entityType.ClrType);
                if (filter != null)
                {
                    modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
                }
            }
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            ApplyPersistenceConventions();

            var pending = AuditCaptor.Collect(this, _auditContext, _currentUser, _clock);
            if (pending.Count == 0)
            {
                return base.SaveChanges(acceptAllChangesOnSuccess);
            }

            // BR-AUD-003: business rows and their audit entries commit or roll
            // back together. Two saves are needed because Added rows only get
            // their identity from the first one.
            var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? Database.BeginTransaction() : null;
            try
            {
                var result = base.SaveChanges(acceptAllChangesOnSuccess);

                AuditCaptor.ResolveDeferredIds(pending);
                AuditEntries.AddRange(pending.Select(p => p.Entry));
                base.SaveChanges(acceptAllChangesOnSuccess);

                transaction?.Commit();
                return result;
            }
            finally
            {
                transaction?.Dispose();
            }
        }

        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyPersistenceConventions();

            var pending = AuditCaptor.Collect(this, _auditContext, _currentUser, _clock);
            if (pending.Count == 0)
            {
                return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            }

            var ownsTransaction = Database.IsRelational() && Database.CurrentTransaction == null;
            var transaction = ownsTransaction ? await Database.BeginTransactionAsync(cancellationToken) : null;
            try
            {
                var result = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                AuditCaptor.ResolveDeferredIds(pending);
                AuditEntries.AddRange(pending.Select(p => p.Entry));
                await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

                if (transaction != null)
                {
                    await transaction.CommitAsync(cancellationToken);
                }

                return result;
            }
            finally
            {
                if (transaction != null)
                {
                    await transaction.DisposeAsync();
                }
            }
        }

        private LambdaExpression? BuildQueryFilter(Type clrType)
        {
            var parameter = Expression.Parameter(clrType, "e");
            Expression? body = null;

            if (typeof(ISchoolScoped).IsAssignableFrom(clrType))
            {
                var currentSchoolId = Expression.Property(Expression.Constant(this), nameof(CurrentSchoolId));
                body = Expression.Equal(
                    Expression.Property(parameter, nameof(ISchoolScoped.SchoolId)),
                    currentSchoolId);
            }

            if (typeof(ISoftActiveFiltered).IsAssignableFrom(clrType))
            {
                var isActive = Expression.Property(parameter, nameof(IActivatable.IsActive));
                body = body == null ? isActive : Expression.AndAlso(body, isActive);
            }

            return body == null ? null : Expression.Lambda(body, parameter);
        }

        private void ApplyPersistenceConventions()
        {
            var now = _clock.UtcNow;

            foreach (var entry in ChangeTracker.Entries().ToList())
            {
                if ((entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                    && (entry.Entity is AuditEntry || entry.Entity is IntegrityCheckpoint))
                {
                    throw new AuditImmutableException(entry.Entity.GetType().Name);
                }

                if (entry.State == EntityState.Deleted && entry.Entity is IActivatable)
                {
                    throw new HardDeleteForbiddenException(entry.Entity.GetType().Name);
                }

                if (entry.Entity is ISchoolScoped scoped && entry.State == EntityState.Added)
                {
                    if (scoped.SchoolId == 0)
                    {
                        scoped.SchoolId = _tenant.SchoolId;
                    }
                    else if (scoped.SchoolId != _tenant.SchoolId)
                    {
                        throw new CrossSchoolWriteException(entry.Entity.GetType().Name, scoped.SchoolId, _tenant.SchoolId);
                    }
                }

                if (entry.Entity is AuditableEntity auditable)
                {
                    if (entry.State == EntityState.Added)
                    {
                        auditable.CreatedAtUtc = now;
                        auditable.CreatedByUserId = _currentUser.UserId;
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        auditable.ModifiedAtUtc = now;
                        auditable.ModifiedByUserId = _currentUser.UserId;
                    }
                }
            }
        }
    }
}
