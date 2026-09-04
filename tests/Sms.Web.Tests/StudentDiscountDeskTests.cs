using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sms.Application.Security;
using Sms.Domain.Discounts;
using Sms.Domain.Security;
using Sms.Web.Controllers;
using Sms.Web.Models;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The grant desk rendered on the student's fee file (doc/Modules/22 §8.3, built on
    /// <c>FeesController.StudentDiscounts.cs</c>).
    /// <para>
    /// Two things are worth pinning. The first is <b>which permission the actions carry</b>: they are
    /// Discounts operations drawn on a Fees screen, and the obvious-looking tidy-up — giving them the
    /// Fees permission the rest of the page uses — would turn this screen into a way to grant a
    /// discount without holding the discount right. That is the exact mistake BR-SEC-010 and
    /// <c>ScreenPermissionTests</c> exist over, and the architecture test cannot catch it: an action
    /// with the <em>wrong</em> permission is still an action with a permission.
    /// </para>
    /// <para>
    /// The second is the panel's own gating, which decides whether any of it is drawn at all.
    /// </para>
    /// </summary>
    public class StudentDiscountDeskTests
    {
        private static IEnumerable<(string Module, string Screen, ActionVerb Verb)> PermissionsOf(string actionName)
            => typeof(FeesController).GetMethod(actionName, BindingFlags.Public | BindingFlags.Instance)!
                .GetCustomAttributes<RequirePermissionAttribute>(inherit: true)
                .Select(a => ((string)a.Arguments[0], (string)a.Arguments[1], (ActionVerb)a.Arguments[2]));

        /// <summary>
        /// BR-DIS-003 hands the three acts to three different holders — finance staff propose, the
        /// routed tier decides, FM+Principal revoke — so the five actions carry three distinct verbs
        /// rather than one blanket right.
        /// </summary>
        [Theory]
        [InlineData(nameof(FeesController.GrantStudentDiscount), ActionVerb.Submit)]
        [InlineData(nameof(FeesController.EditStudentDiscount), ActionVerb.Submit)]
        [InlineData(nameof(FeesController.ApproveStudentDiscount), ActionVerb.Approve)]
        [InlineData(nameof(FeesController.RejectStudentDiscount), ActionVerb.Approve)]
        [InlineData(nameof(FeesController.RevokeStudentDiscount), ActionVerb.Deactivate)]
        public void Acting_on_a_grant_from_the_student_file_needs_the_discount_right_not_the_fees_one(string action, ActionVerb verb)
        {
            var permission = Assert.Single(PermissionsOf(action));

            Assert.Equal(ScreenCatalog.Modules.Discounts, permission.Module);
            Assert.Equal(ScreenCatalog.Discounts.Grants, permission.Screen);
            Assert.Equal(verb, permission.Verb);
        }

        /// <summary>
        /// The three verbs are all catalogued against that screen already — the panel reuses the grant
        /// desk's own permissions rather than minting new ones, which is what keeps it working in a
        /// deployment whose <c>sec.Permission</c> rows were seeded before this screen existed.
        /// </summary>
        [Fact]
        public void The_verbs_the_panel_uses_are_ones_the_catalogue_already_carries()
        {
            var screen = Assert.Single(
                ScreenCatalog.Screens,
                s => s.ModuleCode == ScreenCatalog.Modules.Discounts && s.ScreenCode == ScreenCatalog.Discounts.Grants);

            Assert.Contains(ActionVerb.Submit, screen.Verbs);
            Assert.Contains(ActionVerb.Approve, screen.Verbs);
            Assert.Contains(ActionVerb.Deactivate, screen.Verbs);
        }

        /// <summary>BR-SEC-010: a reader holding none of the three sees the register, not a row of disabled buttons.</summary>
        [Fact]
        public void A_reader_with_none_of_the_three_rights_gets_no_controls()
        {
            var desk = new StudentDiscountDesk { IsWorkingYear = true };

            Assert.False(desk.CanAct);
            Assert.False(desk.CanAddNow);
        }

        /// <summary>
        /// A grant is filed against the working year whatever the screen is showing (BR-DIS-007: nothing
        /// carries between years silently), so the form is not offered while an older year is on screen —
        /// the alternative is a grant landing in a year the operator is not looking at.
        /// </summary>
        [Fact]
        public void The_add_form_is_not_offered_while_reading_a_year_other_than_the_working_one()
        {
            var browsing = new StudentDiscountDesk
            {
                CanPropose = true,
                IsWorkingYear = false,
                Types = new[] { new DiscountType { NameEn = "Negotiated", NameAr = "تفاوضي" } },
            };

            Assert.True(browsing.CanAct, "the row controls still apply — they act on a grant's own year");
            Assert.False(browsing.CanAddNow);
        }

        /// <summary>An empty catalogue is a reason to say so, not to draw a picker with nothing in it (BR-DIS-001).</summary>
        [Fact]
        public void The_add_form_is_not_offered_when_no_manual_type_is_defined()
        {
            var desk = new StudentDiscountDesk { CanPropose = true, IsWorkingYear = true };

            Assert.True(desk.CanAct);
            Assert.False(desk.CanAddNow);
        }

        /// <summary>The whole of it: the right, the working year and something to grant.</summary>
        [Fact]
        public void A_proposer_on_the_working_year_with_a_type_gets_the_form()
        {
            var desk = new StudentDiscountDesk
            {
                CanPropose = true,
                IsWorkingYear = true,
                Types = new[] { new DiscountType { NameEn = "Negotiated", NameAr = "تفاوضي" } },
            };

            Assert.True(desk.CanAddNow);
        }
    }
}
