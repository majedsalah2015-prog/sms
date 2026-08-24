using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Employees;
using Sms.TestSupport;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The refusals the Employees and Teachers screens can actually produce, each asserted to come
    /// back in the reader's language.
    /// <para>
    /// <see cref="TranslatedRefusalTests"/> checks the other half of this rule — that a controller
    /// routes a refusal through <see cref="UserMessage"/> rather than printing the exception. Both
    /// modules already did. What was missing was a case inside the translator, so every one of these
    /// fell through to <c>_ =&gt; exception.Message</c> and an Arabic administrator was told in
    /// English that their contract dates overlapped. Routing correctly to a translator that does not
    /// know the type looks identical, from the controller, to routing correctly to one that does.
    /// </para>
    /// <para>
    /// Asserting "not the engine's sentence" rather than a fixed string on purpose: the wording is
    /// allowed to improve, and a test that pins the sentence makes improving it a chore. What must
    /// not change is that something written for a person comes back, in both languages.
    /// </para>
    /// </summary>
    public class StaffRefusalTranslationTests
    {
        /// <summary>
        /// One instance of every refusal reachable from Module 12's and Module 13's screens. Adding a
        /// throw to those modules means adding it here — that is the point of the list being explicit
        /// rather than reflected over the assembly, which would sweep in the 180-odd exceptions of
        /// other modules that this build has not translated yet.
        /// </summary>
        public static IEnumerable<object[]> Refusals() => new[]
        {
            new object[] { "BR-EMP-001", new InvalidEmployeeStatusTransitionException(EmployeeStatus.Terminated, EmployeeStatus.Active) },
            new object[] { "BR-EMP-002", new OrgUnitInUseException(4, "3 child org unit(s) exist") },
            new object[] { "BR-EMP-003", new OverlappingContractException(7) },
            new object[] { "BR-EMP-003", new InvalidContractStatusTransitionException(ContractStatus.Terminated, ContractStatus.Active) },
            new object[] { "BR-EMP-003", new ContractNotEditableException(11, ContractStatus.Active) },
            new object[] { "BR-TCH-001", new EmployeeNotEligibleForTeachingException(10) },
            new object[] { "BR-TCH-004", new LoadExceededException(3, 24, 24) },
            new object[] { "BR-TCH-005", new DuplicatePrimaryTeacherException(5, 4) },
        };

        [Theory]
        [MemberData(nameof(Refusals))]
        [BusinessRule("BR-GLB-001")]
        public void Every_staff_refusal_is_translated_in_both_languages(string rule, Exception refusal)
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

        /// <summary>
        /// The half a fall-through would still pass: an untranslated type returns the same English
        /// string for both cultures, so "it said something" is not evidence on its own.
        /// </summary>
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
