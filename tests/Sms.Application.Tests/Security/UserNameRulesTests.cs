using Sms.Application.Security;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Security
{
    /// <summary>
    /// What a user name may be, and what one is proposed to be (doc 06 §2, BR-SYS-001). The rules are
    /// about typing rather than about storage, so the tests are too: a name that has to be spelled
    /// out, argued about, or distinguished from a nearly identical one is a name this product refuses.
    /// </summary>
    public class UserNameRulesTests
    {
        [Theory]
        [InlineData("  Ahmed.Salem  ", "ahmed.salem")]
        [InlineData("Ahmed Salem", "ahmed.salem")]
        [InlineData("ahmed   salem", "ahmed.salem")]
        [InlineData("a.salem@school.sa", "a.salem@school.sa")]
        public void Normalize_folds_a_typed_name_to_the_one_form_it_is_stored_in(string typed, string expected)
        {
            Assert.Equal(expected, UserNameRules.Normalize(typed));
        }

        [Fact]
        public void Normalize_drops_what_a_name_may_not_contain_rather_than_refusing_it_here()
        {
            // The Arabic does not survive the fold at all, which is the point: IsWellFormed refuses
            // what is left, so "أحمد" cannot become a silently truncated login.
            Assert.Equal("", UserNameRules.Normalize("أحمد"));
            Assert.Equal("ahmed", UserNameRules.Normalize("Ahmed!#()"));
        }

        [Theory]
        [InlineData("emp-1042")]
        [InlineData("stu-2311")]
        [InlineData("a.salem@school.sa")]
        public void A_typeable_name_is_well_formed(string userName)
        {
            Assert.True(UserNameRules.IsWellFormed(userName));
        }

        [Theory]
        [InlineData("")]
        [InlineData("ab")]
        [InlineData(".ahmed")]
        [InlineData("-ahmed")]
        [InlineData("@ahmed")]
        [InlineData("Ahmed")]
        [InlineData("ahmed salem")]
        public void A_name_that_reads_as_an_accident_is_not(string userName)
        {
            Assert.False(UserNameRules.IsWellFormed(userName));
        }

        [Theory]
        [BusinessRule("BR-SYS-001")]
        [InlineData(ProvisionableAccountType.Staff, "1042", "emp-1042")]
        [InlineData(ProvisionableAccountType.Parent, "PAR-77", "par-77")]
        [InlineData(ProvisionableAccountType.Student, "STU/2311", "stu-2311")]
        public void The_proposal_is_built_from_the_reference_number_not_the_name(
            ProvisionableAccountType accountType, string reference, string expected)
        {
            Assert.Equal(expected, UserNameRules.Propose(accountType, reference));
        }

        [Theory]
        [InlineData("EMP-00007", "emp-00007")]
        [InlineData("emp00007", "emp-00007")]
        [InlineData("empire7", "emp-empire7")]
        public void A_reference_that_already_carries_the_prefix_does_not_get_it_twice(string reference, string expected)
        {
            // The seeded registers number staff "EMP-00007", so the unguarded proposal read
            // "emp-emp-00007" on the provisioning screen. The last case is the guard on the guard: a
            // reference that merely begins with those letters keeps them.
            Assert.Equal(expected, UserNameRules.Propose(ProvisionableAccountType.Staff, reference));
        }

        [Fact]
        [BusinessRule("BR-SYS-001")]
        public void A_reference_that_yields_nothing_typeable_proposes_nothing()
        {
            // Rather than a bare prefix that every such person would collide on. The screen then asks
            // for a name instead of offering one.
            Assert.Equal(string.Empty, UserNameRules.Propose(ProvisionableAccountType.Staff, "  "));
            Assert.Equal(string.Empty, UserNameRules.Propose(ProvisionableAccountType.Student, null));
        }
    }
}
