using Sms.Application.Lookups;
using Sms.Domain.Lookups;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Lookups
{
    /// <summary>
    /// Which lookup lists a school may author (BR-SET-001). The rule this pins down is not
    /// "product tier means read only" — it is that the tier says who ships the values, and a
    /// handful of product-tier lists ship none because only the school can write them.
    /// </summary>
    public class SchoolAuthoredLookupsTests
    {
        private static LookupCategory Category(string code, LookupCategoryTier tier)
            => new LookupCategory { Code = code, Tier = tier };

        [Theory]
        [InlineData("HousingType")]
        [InlineData("ReferralSource")]
        [InlineData("AnythingTheSchoolInvented")]
        [BusinessRule("BR-SET-001")]
        public void A_school_managed_list_is_always_editable(string code)
            => Assert.True(SchoolAuthoredLookups.IsEditableBySchool(Category(code, LookupCategoryTier.SchoolManaged)));

        [Theory]
        [InlineData("Specialization")]
        [InlineData("University")]
        [InlineData("Bank")]
        [InlineData("Nationality")]
        [InlineData("JobTitle")]
        [InlineData("EducationLevel")]
        [BusinessRule("BR-SET-001")]
        public void A_product_list_the_product_ships_no_values_for_is_editable(string code)
            => Assert.True(SchoolAuthoredLookups.IsEditableBySchool(Category(code, LookupCategoryTier.ProductSeeded)));

        /// <summary>
        /// The half that matters more: BR-SET-001 names these as product-owned, and the wizard and
        /// <c>School.CurrencyCode</c> validate against the currency list (BR-GLB-112). A school
        /// editing them is the failure this whole allowlist exists to keep narrow.
        /// </summary>
        [Theory]
        [InlineData("Currency")]
        [InlineData("BloodType")]
        [InlineData("IdType")]
        [InlineData("RelationshipType")]
        [InlineData("AcademicGrade")]
        [InlineData("RoomType")]
        [InlineData("RoomFeature")]
        [InlineData("Curriculum")]
        [BusinessRule("BR-SET-001")]
        public void A_product_owned_list_stays_read_only(string code)
            => Assert.False(SchoolAuthoredLookups.IsEditableBySchool(Category(code, LookupCategoryTier.ProductSeeded)));

        /// <summary>The code arrives from a query string as often as from the database.</summary>
        [Theory]
        [InlineData("specialization")]
        [InlineData("SPECIALIZATION")]
        [InlineData(" Specialization ")]
        public void The_code_match_ignores_case_and_surrounding_space(string code)
            => Assert.True(SchoolAuthoredLookups.Includes(code));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("NoSuchList")]
        public void An_unknown_or_blank_code_is_not_authored(string? code)
            => Assert.False(SchoolAuthoredLookups.Includes(code));

        /// <summary>
        /// A null category is the "no such list" case. It answers false rather than throwing so the
        /// caller can say so in a sentence, in the operator's own language.
        /// </summary>
        [Fact]
        public void A_missing_category_is_not_editable()
            => Assert.False(SchoolAuthoredLookups.IsEditableBySchool(null));
    }
}
