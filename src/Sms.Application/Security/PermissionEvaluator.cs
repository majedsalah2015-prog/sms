using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Pure authorization core (doc 06 §4): deny by default (BR-GLB-070),
    /// effective permission = union of grants across roles — no explicit deny
    /// in v1 (doc 06 §4.3); scope dimensions compound (BR-GLB-071).
    /// </summary>
    public static class PermissionEvaluator
    {
        public static bool HasPermission(
            IReadOnlyCollection<AssignmentSnapshot> assignments,
            string moduleCode,
            string screenCode,
            ActionVerb action)
        {
            return Granting(assignments, moduleCode, screenCode, action).Any();
        }

        /// <summary>Returns null when the permission is not granted at all (deny by default).</summary>
        public static EffectiveScope? GetEffectiveScope(
            IReadOnlyCollection<AssignmentSnapshot> assignments,
            string moduleCode,
            string screenCode,
            ActionVerb action)
        {
            var granting = Granting(assignments, moduleCode, screenCode, action).ToList();
            if (granting.Count == 0)
            {
                return null;
            }

            var (schoolIds, _) = UnionDimension(granting, ScopeDimension.School);
            var (yearIds, dynamicYear) = UnionDimension(granting, ScopeDimension.AcademicYear);
            var (gradeIds, _) = UnionDimension(granting, ScopeDimension.Grade);
            var (sectionIds, dynamicOwnSections) = UnionDimension(granting, ScopeDimension.Section);

            return new EffectiveScope
            {
                SchoolIds = schoolIds,
                AcademicYearIds = yearIds,
                IncludesDynamicActiveYear = dynamicYear,
                GradeIds = gradeIds,
                SectionIds = sectionIds,
                IncludesDynamicOwnSections = dynamicOwnSections,
                OwnRecordsOnly = granting.All(a =>
                    a.ScopeGrants.Any(g => g.Dimension == ScopeDimension.OwnRecordsOnly)),
            };
        }

        private static IEnumerable<AssignmentSnapshot> Granting(
            IReadOnlyCollection<AssignmentSnapshot> assignments,
            string moduleCode,
            string screenCode,
            ActionVerb action)
        {
            return assignments.Where(a => a.Permissions.Any(p =>
                p.Action == action
                && string.Equals(p.ModuleCode, moduleCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(p.ScreenCode, screenCode, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>
        /// Union semantics per dimension: an assignment with no grant in the
        /// dimension is unrestricted, and unrestricted wins the union; otherwise
        /// the allowed id sets merge. Null ValueIds surface as the dynamic flag.
        /// </summary>
        private static (IReadOnlyCollection<int>? Ids, bool Dynamic) UnionDimension(
            IReadOnlyCollection<AssignmentSnapshot> granting,
            ScopeDimension dimension)
        {
            var ids = new HashSet<int>();
            var dynamic = false;
            var restricted = true;

            foreach (var assignment in granting)
            {
                var grants = assignment.ScopeGrants.Where(g => g.Dimension == dimension).ToList();
                if (grants.Count == 0)
                {
                    restricted = false;
                    continue;
                }

                foreach (var grant in grants)
                {
                    if (grant.ValueId is int value)
                    {
                        ids.Add(value);
                    }
                    else
                    {
                        dynamic = true;
                    }
                }
            }

            return (restricted ? ids : null, dynamic);
        }
    }
}
