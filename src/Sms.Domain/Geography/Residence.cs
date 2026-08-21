using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Geography
{
    /// <summary>
    /// محافظة — the outermost level of the residence hierarchy
    /// (governorate → area → neighbourhood).
    /// <para>
    /// A three-level table of its own rather than three lookup categories. The
    /// generic <c>LookupValue</c> framework is a flat list per category, and
    /// this is not a list: a neighbourhood only means something inside its area,
    /// and an area inside its governorate. Flattening it would let a student be
    /// recorded in a neighbourhood that does not exist in the governorate beside
    /// it, and would make "every student in this governorate" a text match
    /// rather than a join.
    /// </para>
    /// <para>
    /// T2: reference data an administrator maintains — changes are rare and
    /// worth an audit line, but they are not the identity fields T1 protects.
    /// </para>
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Governorate : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public List<ResidenceArea> Areas { get; set; } = new();
    }

    /// <summary>منطقة — an area within a governorate.</summary>
    [Audited(AuditTier.T2)]
    public class ResidenceArea : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int GovernorateId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public List<Neighbourhood> Neighbourhoods { get; set; } = new();
    }

    /// <summary>
    /// حي — the level a student's address actually names, and the only one the
    /// student record points at: the area and governorate are reached by walking
    /// up, so they can never disagree with it.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Neighbourhood : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public int ResidenceAreaId { get; set; }

        public string Code { get; set; } = string.Empty;

        public LocalizedName Name { get; set; } = new();

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
