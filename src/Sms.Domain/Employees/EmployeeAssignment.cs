using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.EmployeeAssignment (doc/Modules/12 §7, BR-EMP-002): position +
    /// org unit + reporting line, effective-dated — same pattern as
    /// E-102's Signatory / E-103's HomeroomAssignment (closes out the
    /// prior current row rather than erroring on reassignment).
    /// PositionLookupId reuses the LookupValue mechanism ("JobTitle"
    /// category) rather than a dedicated Position entity — same reuse
    /// call as RelationshipType/IdType, since a job title is just a
    /// bilingual code, not a structure that needs its own table.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class EmployeeAssignment : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        public int OrgUnitId { get; set; }

        public int PositionLookupId { get; set; }

        /// <summary>Drives WF-10 leave approvals (BR-EMP-002) — the workflow itself is deferred.</summary>
        public int? ManagerEmployeeId { get; set; }

        public DateTime EffectiveFromUtc { get; set; }

        /// <summary>Null = the current assignment for this employee.</summary>
        public DateTime? EffectiveToUtc { get; set; }
    }
}
