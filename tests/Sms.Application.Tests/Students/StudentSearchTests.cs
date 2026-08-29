using System;
using System.Linq;
using Sms.Application.Students;
using Sms.Domain.Students;
using Xunit;

namespace Sms.Application.Tests.Students
{
    /// <summary>
    /// doc/Modules/23 §8.3 asks the subscription desk for a student search, and the assignment console
    /// and discount desk ask the same question of the same register. What a school actually types into
    /// such a box is a number off a bus sheet, or two words of a name — the father's among them, which
    /// is the half a naive search misses.
    /// <para>
    /// These run the engine over a plain in-memory queryable. The provider's own behaviour — that the
    /// same expression survives translation to SQL rather than falling back to the client — is the
    /// other half, and belongs to <c>StudentSearchQueryTests</c> against a real context.
    /// </para>
    /// </summary>
    public class StudentSearchTests
    {
        private static Student Child(
            string no, string firstAr, string fatherAr, string grandAr, string familyAr,
            string firstEn, string fatherEn, string grandEn, string familyEn) => new()
        {
            StudentNo = no,
            FirstNameAr = firstAr, FatherNameAr = fatherAr, GrandfatherNameAr = grandAr, FamilyNameAr = familyAr,
            FirstNameEn = firstEn, FatherNameEn = fatherEn, GrandfatherNameEn = grandEn, FamilyNameEn = familyEn,
        };

        private static readonly Student Mohammed = Child(
            "STU-0231", "محمد", "أحمد", "سعيد", "الغامدي", "Mohammed", "Ahmed", "Saeed", "Alghamdi");

        private static readonly Student Mohammed2 = Child(
            "STU-0232", "محمد", "خالد", "سعيد", "القحطاني", "Mohammed", "Khaled", "Saeed", "Alqahtani");

        private static readonly Student Sara = Child(
            "STU-0777", "سارة", "عمر", "فهد", "الحربي", "Sara", "Omar", "Fahd", "Alharbi");

        private static IQueryable<Student> Register => new[] { Mohammed, Mohammed2, Sara }.AsQueryable();

        private static string[] Match(string? term) =>
            StudentSearch.Matching(Register, term).Select(s => s.StudentNo).OrderBy(n => n).ToArray();

        // ---------------------------------------------------------------- what a clerk types

        [Fact]
        public void The_student_number_finds_the_one_child_it_names()
        {
            Assert.Equal(new[] { "STU-0231" }, Match("STU-0231"));
        }

        [Fact]
        public void A_fragment_of_the_number_is_enough()
        {
            // Bus sheets and receipts print the tail of the number more often than the whole of it.
            Assert.Equal(new[] { "STU-0777" }, Match("0777"));
        }

        [Theory]
        [InlineData("stu-0231")]
        [InlineData("STU-0231")]
        [InlineData("Stu-0231")]
        public void Case_does_not_decide_whether_a_child_is_found(string typed)
        {
            // Sqlite's Contains is case-sensitive and SQL Server's default collation is not. Left to the
            // provider, this test would pass in CI and the screen would fail in the school, or the
            // reverse. The engine folds case itself so both answer the same way.
            Assert.Equal(new[] { "STU-0231" }, Match(typed));
        }

        [Theory]
        [InlineData("mohammed")]
        [InlineData("MOHAMMED")]
        public void An_English_name_is_found_whatever_case_it_is_typed_in(string typed)
        {
            Assert.Equal(new[] { "STU-0231", "STU-0232" }, Match(typed));
        }

        // ---------------------------------------------------------------- every name part, both languages

        [Theory]
        [InlineData("محمد", "STU-0231")]      // first
        [InlineData("أحمد", "STU-0231")]      // father — the part a naive search drops
        [InlineData("سعيد", "STU-0231")]      // grandfather — printed nowhere, still searchable
        [InlineData("الغامدي", "STU-0231")]   // family
        [InlineData("Mohammed", "STU-0231")]
        [InlineData("Ahmed", "STU-0231")]
        [InlineData("Saeed", "STU-0231")]
        [InlineData("Alghamdi", "STU-0231")]
        public void Every_part_of_the_name_is_searchable_in_both_languages(string typed, string expected)
        {
            // BR-STU-001 makes the identity name four parts in each language while the picker prints
            // three. Searching only the printed ones answers "no results" for a name the clerk can read
            // on screen, which reads as a broken screen rather than a narrow search.
            Assert.Contains(expected, Match(typed));
        }

        // ---------------------------------------------------------------- more words means fewer children

        [Fact]
        public void A_second_word_narrows_rather_than_widens()
        {
            // "محمد أحمد" is the محمد whose father is أحمد — not every محمد plus every child of an أحمد.
            // An OR here would hand back a longer list the more precisely the clerk described the child.
            Assert.Equal(new[] { "STU-0231", "STU-0232" }, Match("محمد"));
            Assert.Equal(new[] { "STU-0231" }, Match("محمد أحمد"));
        }

        [Fact]
        public void Words_may_be_given_in_any_order()
        {
            Assert.Equal(new[] { "STU-0231" }, Match("الغامدي محمد"));
        }

        [Fact]
        public void A_word_matching_nobody_empties_the_result_even_beside_a_word_that_matches()
        {
            Assert.Empty(Match("محمد زيد"));
        }

        [Fact]
        public void The_number_and_a_name_may_be_mixed_in_one_search()
        {
            Assert.Equal(new[] { "STU-0231" }, Match("0231 محمد"));
            Assert.Empty(Match("0231 سارة"));
        }

        // ---------------------------------------------------------------- a blank box is not a filter

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void A_blank_term_leaves_the_register_alone(string? term)
        {
            // The caller decides whether an unfiltered list is worth showing; the engine does not
            // silently return nothing for an empty box, which would read as "this school has no students".
            Assert.Equal(3, StudentSearch.Matching(Register, term).Count());
        }

        [Fact]
        public void Extra_whitespace_between_words_is_not_a_word()
        {
            Assert.Equal(new[] { "STU-0231" }, Match("  محمد    أحمد  "));
        }

        [Fact]
        public void A_null_query_is_a_caller_mistake_and_says_so()
        {
            Assert.Throws<ArgumentNullException>(() => StudentSearch.Matching(null!, "محمد"));
        }

        // ---------------------------------------------------------------- the split the callers share

        [Fact]
        public void Words_lowers_and_splits_so_a_caller_filtering_in_memory_agrees_with_the_query()
        {
            Assert.Equal(new[] { "stu-0231", "محمد" }, StudentSearch.Words(" STU-0231  محمد "));
            Assert.Empty(StudentSearch.Words("   "));
            Assert.Empty(StudentSearch.Words(null));
        }
    }
}
