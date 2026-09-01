using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.TestSupport;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The refusals the Installment Plans screens can actually produce, each asserted to come back
    /// in the reader's language — the same guarantee <see cref="StaffRefusalTranslationTests"/>
    /// holds for Modules 12 and 13, now for Module 20 (doc/Modules/20 §8.2, §9).
    /// <para>
    /// Every one of these fell through to <c>_ =&gt; exception.Message</c> before this build, so a
    /// collection officer working the Arabic console was told "Student 1 already has a plan
    /// assignment for this year and category" — a sentence written for a log file, in a language
    /// they may not read, about an id that is not on their screen.
    /// </para>
    /// <para>
    /// <see cref="ExceptionAssignmentReasonRequiredException"/> is in this list for a second reason.
    /// The assignment console offers exactly the gesture that raises it — tick "Exception
    /// assignment", leave the reason blank — and the controller's catch filter did not name it, so
    /// the screen answered with HTTP 500 instead of the rule. Translating it is only half the fix;
    /// <see cref="TranslatedRefusalTests"/> cannot see a refusal that was never caught.
    /// </para>
    /// </summary>
    public class InstallmentRefusalTranslationTests
    {
        /// <summary>
        /// One instance of every refusal reachable from Module 20's screens. Explicit, not reflected
        /// over the assembly, for the reason the staff list gives: the product defines 224 exception
        /// types and most are not translated yet, so a reflected list would fail for work this build
        /// never claimed to do.
        /// </summary>
        public static IEnumerable<object[]> Refusals() => new[]
        {
            new object[] { "BR-INS-001", new PlanTemplateNotApprovedException(3) },
            new object[] { "BR-INS-002", new NoChargesToScheduleException(19) },
            new object[] { "BR-INS-002", new PlanAssignmentExistsException(19) },
            new object[] { "BR-INS-002", new ExceptionAssignmentReasonRequiredException() },
        };

        [Theory]
        [MemberData(nameof(Refusals))]
        [BusinessRule("BR-GLB-001")]
        public void Every_installment_refusal_is_translated_in_both_languages(string rule, Exception refusal)
        {
            foreach (var arabic in new[] { true, false })
            {
                var message = UserMessage.For(refusal, arabic);

                Assert.False(
                    string.Equals(message, refusal.Message, StringComparison.Ordinal),
                    $"{refusal.GetType().Name} ({rule}) falls through to the engine's own English sentence in " +
                    $"{(arabic ? "Arabic" : "English")} — add a case to UserMessage.For.");
            }
        }

        [Theory]
        [MemberData(nameof(Refusals))]
        [BusinessRule("BR-GLB-001")]
        public void Arabic_and_English_are_not_the_same_sentence(string rule, Exception refusal)
        {
            var ar = UserMessage.For(refusal, arabic: true);
            var en = UserMessage.For(refusal, arabic: false);

            Assert.False(
                string.Equals(ar, en, StringComparison.Ordinal),
                $"{refusal.GetType().Name} ({rule}) returns one sentence for both languages.");
            Assert.Contains(ar, c => c >= '؀' && c <= 'ۿ');
        }
    }
}
