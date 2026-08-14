using System.Linq;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    public class PermissionEvaluatorTests
    {
        private static AssignmentSnapshot Assignment(
            PermissionTriple[] permissions,
            params ScopeGrantValue[] grants)
        {
            return new AssignmentSnapshot(permissions, grants);
        }

        private static readonly PermissionTriple ViewStudents = new("STU", "StudentList", ActionVerb.View);
        private static readonly PermissionTriple EditStudents = new("STU", "StudentList", ActionVerb.Edit);

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void No_grant_means_no_permission_and_no_scope()
        {
            var assignments = new[] { Assignment(new[] { ViewStudents }) };

            Assert.False(PermissionEvaluator.HasPermission(assignments, "STU", "StudentList", ActionVerb.Edit));
            Assert.False(PermissionEvaluator.HasPermission(assignments, "FEE", "InvoiceList", ActionVerb.View));
            Assert.Null(PermissionEvaluator.GetEffectiveScope(assignments, "STU", "StudentList", ActionVerb.Edit));
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void Empty_assignment_set_denies_everything()
        {
            var assignments = System.Array.Empty<AssignmentSnapshot>();

            Assert.False(PermissionEvaluator.HasPermission(assignments, "STU", "StudentList", ActionVerb.View));
        }

        [Fact]
        [BusinessRule("BR-GLB-070")]
        public void Effective_permission_is_the_union_of_role_grants()
        {
            // doc 06 §4.3: users may hold multiple roles; union, no explicit deny.
            var assignments = new[]
            {
                Assignment(new[] { ViewStudents }),
                Assignment(new[] { EditStudents }),
            };

            Assert.True(PermissionEvaluator.HasPermission(assignments, "STU", "StudentList", ActionVerb.View));
            Assert.True(PermissionEvaluator.HasPermission(assignments, "STU", "StudentList", ActionVerb.Edit));
        }

        [Fact]
        [BusinessRule("BR-GLB-071")]
        public void Scope_dimensions_compound_within_an_assignment()
        {
            var assignments = new[]
            {
                Assignment(
                    new[] { ViewStudents },
                    new ScopeGrantValue(ScopeDimension.Grade, 5),
                    new ScopeGrantValue(ScopeDimension.Grade, 6),
                    new ScopeGrantValue(ScopeDimension.Section, 101)),
            };

            var scope = PermissionEvaluator.GetEffectiveScope(assignments, "STU", "StudentList", ActionVerb.View)!;

            Assert.Null(scope.SchoolIds); // empty dimension = all within lower bound
            Assert.Equal(new[] { 5, 6 }, scope.GradeIds!.OrderBy(x => x));
            Assert.Equal(new[] { 101 }, scope.SectionIds!);
            Assert.False(scope.OwnRecordsOnly);
        }

        [Fact]
        [BusinessRule("BR-GLB-071")]
        public void Unrestricted_assignment_wins_the_union_per_dimension()
        {
            var assignments = new[]
            {
                Assignment(new[] { ViewStudents }, new ScopeGrantValue(ScopeDimension.Grade, 5)),
                Assignment(new[] { ViewStudents }), // no grade restriction
            };

            var scope = PermissionEvaluator.GetEffectiveScope(assignments, "STU", "StudentList", ActionVerb.View)!;

            Assert.Null(scope.GradeIds);
        }

        [Fact]
        [BusinessRule("BR-GLB-071")]
        public void Dynamic_own_sections_grant_surfaces_as_flag()
        {
            // doc 06 §4.2: ScopeValueId null = "own sections", resolved from
            // Teacher Assignments each year — no manual re-scoping at rollover.
            var assignments = new[]
            {
                Assignment(new[] { ViewStudents }, new ScopeGrantValue(ScopeDimension.Section, null)),
            };

            var scope = PermissionEvaluator.GetEffectiveScope(assignments, "STU", "StudentList", ActionVerb.View)!;

            Assert.True(scope.IncludesDynamicOwnSections);
            Assert.Empty(scope.SectionIds!);
        }

        [Fact]
        [BusinessRule("BR-GLB-071")]
        public void Own_records_only_applies_only_when_every_granting_assignment_has_it()
        {
            var ownOnly = Assignment(new[] { ViewStudents }, new ScopeGrantValue(ScopeDimension.OwnRecordsOnly, null));
            var broad = Assignment(new[] { ViewStudents });

            var restrictive = PermissionEvaluator.GetEffectiveScope(new[] { ownOnly }, "STU", "StudentList", ActionVerb.View)!;
            var widened = PermissionEvaluator.GetEffectiveScope(new[] { ownOnly, broad }, "STU", "StudentList", ActionVerb.View)!;

            Assert.True(restrictive.OwnRecordsOnly);
            Assert.False(widened.OwnRecordsOnly);
        }

        [Fact]
        [BusinessRule("BR-GLB-005")]
        public void Action_taxonomy_has_no_delete_verb()
        {
            Assert.DoesNotContain("Delete", System.Enum.GetNames(typeof(ActionVerb)));
        }
    }
}
