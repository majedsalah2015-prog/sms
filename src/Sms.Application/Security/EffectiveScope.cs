using System.Collections.Generic;

namespace Sms.Application.Security
{
    /// <summary>
    /// The union of the scope dimensions of every assignment granting a
    /// permission (doc 06 §4.2/4.3). A null id set = unrestricted in that
    /// dimension ("empty dimension = all within the lower bound granted").
    /// Data scoping is part of authorization, not report filtering (doc 06 §1):
    /// repositories apply this scope to every query path.
    /// </summary>
    public sealed class EffectiveScope
    {
        public IReadOnlyCollection<int>? SchoolIds { get; init; }

        public IReadOnlyCollection<int>? AcademicYearIds { get; init; }

        /// <summary>True when a granting assignment scopes the year dynamically
        /// to the Active(+Preparation) year (ScopeValueId null).</summary>
        public bool IncludesDynamicActiveYear { get; init; }

        public IReadOnlyCollection<int>? GradeIds { get; init; }

        public IReadOnlyCollection<int>? SectionIds { get; init; }

        /// <summary>True when a granting assignment carries the dynamic
        /// "own sections" grant, resolved from Teacher Assignments each year.</summary>
        public bool IncludesDynamicOwnSections { get; init; }

        /// <summary>True only when every granting assignment is own-records-only.</summary>
        public bool OwnRecordsOnly { get; init; }
    }
}
