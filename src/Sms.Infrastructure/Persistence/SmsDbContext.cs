using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Common;

namespace Sms.Infrastructure.Persistence
{
    /// <summary>
    /// Base context enforcing the cross-cutting persistence rules centrally
    /// (docs/Database/01 §5 — never per screen or module):
    ///  - tenant query filter on every ISchoolScoped entity (ADR-2, BR-GLB-010),
    ///  - opt-in soft-active filter on ISoftActiveFiltered entities (DB-6),
    ///  - audit stamping via IClock/ICurrentUser (BR-GLB-007),
    ///  - cross-school write guard (BR-GLB-010),
    ///  - hard-delete guard on master data (ADR-7, BR-GLB-005).
    /// </summary>
    public class SmsDbContext : DbContext
    {
        private readonly ITenantContext _tenant;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;

        public SmsDbContext(DbContextOptions options, ITenantContext tenant, ICurrentUser currentUser, IClock clock)
            : base(options)
        {
            _tenant = tenant;
            _currentUser = currentUser;
            _clock = clock;
        }

        /// <summary>Referenced inside query filters so EF parameterizes it per context instance.</summary>
        public int CurrentSchoolId => _tenant.SchoolId;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            ApplyPersistenceConventions();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
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
