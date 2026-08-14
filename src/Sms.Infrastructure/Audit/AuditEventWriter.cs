using System;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Audit;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Audit
{
    /// <summary>
    /// Writes record-level events (T0 view/print/export, security, workflow,
    /// job runs) into the ambient unit of work; they persist with the caller's
    /// SaveChanges, atomic with the business transaction (BR-AUD-003).
    /// </summary>
    public class AuditEventWriter : IAuditEventWriter
    {
        private readonly SmsDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IWorkingYearContext _workingYear;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly IAuditContext _auditContext;

        public AuditEventWriter(
            SmsDbContext db,
            ITenantContext tenant,
            IWorkingYearContext workingYear,
            ICurrentUser currentUser,
            IClock clock,
            IAuditContext auditContext)
        {
            _db = db;
            _tenant = tenant;
            _workingYear = workingYear;
            _currentUser = currentUser;
            _clock = clock;
            _auditContext = auditContext;
        }

        public AuditEntry Log(AuditAction action, string entityType, long? entityId = null, string? businessKey = null, string? reason = null)
        {
            var entry = new AuditEntry
            {
                SchoolId = _tenant.SchoolId,
                AcademicYearId = _workingYear.AcademicYearId,
                EntityType = entityType,
                EntityId = entityId,
                BusinessKey = businessKey,
                ActorUserId = _currentUser.UserId,
                Action = action,
                Reason = reason ?? _auditContext.Reason,
                CorrelationId = Guid.NewGuid(),
                SourceScreen = _auditContext.SourceScreen,
                ClientIp = _auditContext.ClientIp,
                OccurredAtUtc = _clock.UtcNow,
            };

            _db.AuditEntries.Add(entry);
            return entry;
        }
    }
}
