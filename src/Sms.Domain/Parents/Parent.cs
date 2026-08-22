using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Parents
{
    /// <summary>
    /// ppl.Parent (doc/Modules/11 §7, BR-PAR-001): a person entity with a
    /// permanent Parent File No. (doc 08), deduplicated — never duplicated
    /// per child or per year. BR-PAR-009: identity fields T1-audited.
    /// Deduplication matching (BR-PAR-002) and the merge tool (BR-PAR-003)
    /// are deferred — this slice covers the Registrar-direct creation path
    /// only, not the admission-portal dedup pipeline.
    /// </summary>
    [Audited(AuditTier.T1)]
    public class Parent : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        /// <summary>doc 08 PAR series — permanent.</summary>
        public string ParentFileNo { get; set; } = string.Empty;

        /// <summary>BR-SEC-010/011's portal identity bridge — nullable since portal account provisioning (Module 36) isn't wired by any admin service yet. Mirrors Employee.UserAccountId's E-203 precedent.</summary>
        public int? UserAccountId { get; set; }

        [RequiresAuditReason]
        public string NameAr { get; set; } = string.Empty;

        [RequiresAuditReason]
        public string NameEn { get; set; } = string.Empty;

        /// <summary>BR-PAR-007: mandatory, verified on portal activation (verification not modeled in this slice).</summary>
        public string PrimaryMobile { get; set; } = string.Empty;

        public string? Email { get; set; }

        /// <summary>The line that says which house. The residence hierarchy below says which place.</summary>
        public string? Address { get; set; }

        /// <summary>
        /// منطقة — the locality level of the residence hierarchy (governorate → area → neighbourhood),
        /// with the governorate reached by walking up from it so the two can never disagree.
        /// <para>
        /// Two levels are kept here where <c>Student</c> keeps only the neighbourhood, because most
        /// localities have no quarters recorded at all: pointing solely at a neighbourhood would leave
        /// every family outside Gaza City with no residence that could be recorded.
        /// </para>
        /// </summary>
        public int? ResidenceAreaId { get; set; }

        /// <summary>حي — the quarter inside <see cref="ResidenceAreaId"/>, where that locality has any.</summary>
        public int? NeighbourhoodId { get; set; }

        public string? OccupationEmployer { get; set; }

        /// <summary>"ar" or "en" — BR-NOT-001's recipient-language input.</summary>
        public string PreferredLanguage { get; set; } = "ar";

        public bool IsActive { get; set; } = true;
    }
}
