using System.Linq;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Web.Security;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The permission grid posts its state as "MODULE/Screen/Verb" strings, one per ticked box. That
    /// encoding is the only place where a screen's checkbox and a <c>sec.Permission</c> row meet, and
    /// getting it wrong would grant something other than what was ticked.
    /// </summary>
    public class PermissionGridTests
    {
        [Fact]
        public void A_ticked_box_becomes_the_permission_it_names()
        {
            var keys = PermissionGrid.Parse(new[] { "STU/Directory/View", "FEE/Charges/Post" });

            Assert.Equal(2, keys.Count);
            Assert.Contains(keys, k => k == new PermissionKey("STU", "Directory", ActionVerb.View));
            Assert.Contains(keys, k => k == new PermissionKey("FEE", "Charges", ActionVerb.Post));
        }

        [Fact]
        public void An_empty_post_revokes_everything_rather_than_meaning_no_change()
        {
            // A grid with every box cleared submits no "granted" field at all. That must read as
            // "grant nothing", not as "leave the role alone" — the difference is a role a person
            // believes they emptied and did not.
            Assert.Empty(PermissionGrid.Parse(null));
            Assert.Empty(PermissionGrid.Parse(System.Array.Empty<string>()));
        }

        /// <summary>
        /// The values come from checkboxes this application rendered, so a malformed one is tampering.
        /// Dropping it is right — the service refuses anything the catalogue does not define anyway,
        /// and throwing would let a crafted post turn the save into a 500.
        /// </summary>
        [Theory]
        [InlineData("STU/Directory")]
        [InlineData("STU/Directory/Fly")]
        [InlineData("")]
        [InlineData("////")]
        public void A_malformed_value_is_dropped_rather_than_thrown_on(string value)
        {
            Assert.Empty(PermissionGrid.Parse(new[] { value }));
        }

        [Fact]
        public void A_malformed_value_does_not_take_the_valid_ones_with_it()
        {
            var keys = PermissionGrid.Parse(new[] { "nonsense", "STU/Directory/View" });

            Assert.Equal(new PermissionKey("STU", "Directory", ActionVerb.View), Assert.Single(keys));
        }

        /// <summary>
        /// Every verb in the taxonomy round-trips. A verb added to <see cref="ActionVerb"/> that did
        /// not parse would be a checkbox that silently never granted anything.
        /// </summary>
        [Fact]
        public void Every_verb_in_the_taxonomy_round_trips()
        {
            var verbs = System.Enum.GetValues(typeof(ActionVerb)).Cast<ActionVerb>().ToList();

            var keys = PermissionGrid.Parse(verbs.Select(v => $"STU/Directory/{v}").ToArray());

            Assert.Equal(verbs.Count, keys.Count);
            Assert.Equal(verbs, keys.Select(k => k.Action).ToList());
        }
    }
}
