using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Setup
{
    /// <summary>
    /// core.SchoolSetting (doc/Modules/01 §7): key/value with a value type,
    /// optionally pinned to one academic year. BR-SET-005/BR-GLB-011: a row
    /// with AcademicYearId = null is the school-wide default; a row with a
    /// year is that year's effective value, so history is preserved by
    /// adding a year row, never by overwriting — a transaction dated in a
    /// past year resolves that year's row (see SettingResolver). BR-SET-007:
    /// settings are T1-audited; the first definition is an Added row (no
    /// reason), any later edit of the value demands one.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class SchoolSetting : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        /// <summary>One of <c>SettingKeys</c> (Sms.Application.Setup) — unknown keys are rejected by the admin service.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Null = school-wide default; set = override in force for that academic year (BR-SET-005).</summary>
        public int? AcademicYearId { get; set; }

        [RequiresAuditReason]
        public string Value { get; set; } = string.Empty;

        public SettingValueType ValueType { get; set; }
    }
}
