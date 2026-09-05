using System;
using Sms.Application.Learning;
using Sms.Domain.Learning;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Learning
{
    /// <summary>
    /// BR-LRN-011: which question types mark themselves, what each one must look
    /// like to be answerable, and how an objective answer is judged.
    /// </summary>
    public class QuestionTypeRulesTests
    {
        private static readonly bool[] NoOptions = Array.Empty<bool>();
        private static readonly string[] NoAnswers = Array.Empty<string>();

        // ---------------------------------------------------------------- the auto-marking line

        [Theory]
        [BusinessRule("BR-LRN-011")]
        [InlineData(QuestionType.SingleChoice)]
        [InlineData(QuestionType.MultipleChoice)]
        [InlineData(QuestionType.TrueFalse)]
        [InlineData(QuestionType.Numeric)]
        [InlineData(QuestionType.ShortText)]
        public void The_five_objective_types_mark_themselves(QuestionType type)
            => Assert.True(QuestionTypeRules.IsAutoMarkable(type));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_constructed_response_never_marks_itself()
            => Assert.False(QuestionTypeRules.IsAutoMarkable(QuestionType.Essay));

        // ---------------------------------------------------------------- shape

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void One_option_is_an_instruction_not_a_choice()
            => Assert.Equal(
                QuestionShapeRefusal.TooFewOptions,
                QuestionTypeRules.Check(QuestionType.SingleChoice, 2m, new[] { true }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_choice_question_with_no_right_answer_cannot_be_marked()
            => Assert.Equal(
                QuestionShapeRefusal.NoCorrectOption,
                QuestionTypeRules.Check(QuestionType.SingleChoice, 2m, new[] { false, false }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Single_choice_means_one_right_answer()
            => Assert.Equal(
                QuestionShapeRefusal.TooManyCorrectOptions,
                QuestionTypeRules.Check(QuestionType.SingleChoice, 2m, new[] { true, true, false }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void True_false_is_single_choice_and_is_held_to_the_same_rule()
            => Assert.Equal(
                QuestionShapeRefusal.TooManyCorrectOptions,
                QuestionTypeRules.Check(QuestionType.TrueFalse, 1m, new[] { true, true }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Multiple_choice_accepts_more_than_one_right_answer()
            => Assert.Equal(
                QuestionShapeRefusal.None,
                QuestionTypeRules.Check(QuestionType.MultipleChoice, 3m, new[] { true, true, false }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_question_where_every_option_is_right_is_refused()
            => Assert.Equal(
                QuestionShapeRefusal.EveryOptionCorrect,
                QuestionTypeRules.Check(QuestionType.MultipleChoice, 3m, new[] { true, true, true }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void An_essay_carrying_options_is_a_type_chosen_by_mistake()
            => Assert.Equal(
                QuestionShapeRefusal.OptionsOnANonChoiceType,
                QuestionTypeRules.Check(QuestionType.Essay, 10m, new[] { true, false }, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Short_text_with_nothing_to_match_against_cannot_mark_itself()
            => Assert.Equal(
                QuestionShapeRefusal.NoAcceptedAnswer,
                QuestionTypeRules.Check(QuestionType.ShortText, 2m, NoOptions, new[] { "   " }, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_numeric_question_whose_answer_is_not_a_number_is_refused()
            => Assert.Equal(
                QuestionShapeRefusal.NonNumericAcceptedAnswer,
                QuestionTypeRules.Check(QuestionType.Numeric, 2m, NoOptions, new[] { "about ten" }, 0.5m));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Tolerance_belongs_to_numeric_questions_and_nothing_else()
            => Assert.Equal(
                QuestionShapeRefusal.ToleranceOnANonNumericType,
                QuestionTypeRules.Check(QuestionType.ShortText, 2m, NoOptions, new[] { "water" }, 0.5m));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_negative_tolerance_would_accept_nothing_at_all()
            => Assert.Equal(
                QuestionShapeRefusal.NegativeTolerance,
                QuestionTypeRules.Check(QuestionType.Numeric, 2m, NoOptions, new[] { "10" }, -1m));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_question_worth_nothing_cannot_be_asked()
            => Assert.Equal(
                QuestionShapeRefusal.MarksNotPositive,
                QuestionTypeRules.Check(QuestionType.Essay, 0m, NoOptions, NoAnswers, null));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_well_formed_numeric_question_passes()
            => Assert.Equal(
                QuestionShapeRefusal.None,
                QuestionTypeRules.Check(QuestionType.Numeric, 2m, NoOptions, new[] { "3.14" }, 0.01m));

        // ---------------------------------------------------------------- numeric marking

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_numeric_answer_inside_the_tolerance_is_right()
            => Assert.True(QuestionTypeRules.MatchesNumeric(3.14m, 0.01m, "3.15"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_numeric_answer_outside_the_tolerance_is_wrong()
            => Assert.False(QuestionTypeRules.MatchesNumeric(3.14m, 0.01m, "3.2"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void No_tolerance_means_exact()
        {
            Assert.True(QuestionTypeRules.MatchesNumeric(10m, null, "10"));
            Assert.False(QuestionTypeRules.MatchesNumeric(10m, null, "10.1"));
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void A_zero_tolerance_is_a_statement_the_author_is_allowed_to_make()
            => Assert.True(QuestionTypeRules.MatchesNumeric(10m, 0m, " 10 "));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Words_where_a_number_was_asked_for_are_wrong_rather_than_an_error()
            => Assert.False(QuestionTypeRules.MatchesNumeric(10m, 1m, "ten"));

        // ---------------------------------------------------------------- short text marking

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Any_of_the_accepted_spellings_is_right()
        {
            var accepted = new[] { "water", "H2O" };

            Assert.True(QuestionTypeRules.MatchesShortText(accepted, "H2O"));
            Assert.True(QuestionTypeRules.MatchesShortText(accepted, "water"));
            Assert.False(QuestionTypeRules.MatchesShortText(accepted, "air"));
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Case_and_stray_spacing_never_carried_meaning_in_an_answer_box()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "Water" }, "  wAtEr  "));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Repeated_spaces_inside_an_answer_collapse()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "sodium chloride" }, "sodium    chloride"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Arabic_diacritics_a_student_may_or_may_not_type_do_not_decide_a_mark()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "ماء" }, "مَاء"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void The_alef_forms_a_keyboard_makes_interchangeable_are_one_answer()
        {
            var accepted = new[] { "إجابة" };

            Assert.True(QuestionTypeRules.MatchesShortText(accepted, "اجابة"));
            Assert.True(QuestionTypeRules.MatchesShortText(accepted, "أجابة"));
            Assert.True(QuestionTypeRules.MatchesShortText(accepted, "آجابة"));
        }

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Yaa_and_alef_maqsura_are_one_answer()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "على" }, "علي"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Taa_marbuta_and_haa_are_one_answer()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "مدرسة" }, "مدرسه"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Tatweel_stretched_across_a_word_is_decoration_not_an_answer()
            => Assert.True(QuestionTypeRules.MatchesShortText(new[] { "مدرسة" }, "مدرســـة"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void Normalisation_does_not_make_different_words_equal()
            => Assert.False(QuestionTypeRules.MatchesShortText(new[] { "مدرسة" }, "مدينة"));

        [Fact]
        [BusinessRule("BR-LRN-011")]
        public void An_unanswered_question_is_not_right_by_default()
        {
            Assert.False(QuestionTypeRules.MatchesShortText(new[] { "water" }, null));
            Assert.False(QuestionTypeRules.MatchesShortText(new[] { "water" }, "   "));
        }
    }
}
