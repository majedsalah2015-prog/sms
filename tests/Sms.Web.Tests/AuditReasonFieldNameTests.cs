using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sms.Domain.Audit;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// A refusal must name the field in words, in both languages.
    /// <para>
    /// Every property carrying <c>[RequiresAuditReason]</c> can produce
    /// <c>MissingAuditReasonException</c>, and that is the only way to reach
    /// <see cref="UserMessage.FieldName"/>. The set is therefore closed and knowable, and it was
    /// nonetheless incomplete: editing a parent's ID number told the administrator that changing
    /// <c>«Parent.PrimaryIdNo»</c> needs a reason — a class name and a property name, printed
    /// identically in Arabic and English, on the one screen whose job at that moment is to explain
    /// what needs justifying.
    /// </para>
    /// <para>
    /// A hand-maintained list goes stale the first time somebody adds an audited field, so it is
    /// held by reflection over the domain rather than by anybody remembering. The same shape as
    /// <c>ScreenPermissionTests</c>: the build is what notices.
    /// </para>
    /// </summary>
    public class AuditReasonFieldNameTests
    {
        public static IEnumerable<object[]> AuditReasonFields() =>
            typeof(RequiresAuditReasonAttribute).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract)
                .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                    .Where(p => p.GetCustomAttribute<RequiresAuditReasonAttribute>() != null)
                    .Select(p => new object[] { t.Name, p.Name }))
                .OrderBy(pair => (string)pair[0], StringComparer.Ordinal)
                .ThenBy(pair => (string)pair[1], StringComparer.Ordinal)
                .ToList();

        [Theory]
        [MemberData(nameof(AuditReasonFields))]
        public void Every_audited_field_has_a_name_a_person_can_read(string entityType, string field)
        {
            foreach (var arabic in new[] { true, false })
            {
                var name = UserMessage.FieldName(entityType, field, arabic);

                Assert.False(
                    string.IsNullOrWhiteSpace(name),
                    $"{entityType}.{field} has no {(arabic ? "Arabic" : "English")} name.");

                Assert.False(
                    name.Contains(field, StringComparison.Ordinal) && name.Contains(entityType, StringComparison.Ordinal),
                    $"{entityType}.{field} falls through to the CLR name in {(arabic ? "Arabic" : "English")}. " +
                    "Add it to UserMessage.FieldName — an administrator cannot act on a property name.");

                Assert.False(
                    name == (arabic ? "هذا الحقل" : field.ToLowerInvariant()),
                    $"{entityType}.{field} reaches the unnamed-field fallback in {(arabic ? "Arabic" : "English")}.");
            }
        }

        /// <summary>The two languages must actually differ; a copied English string is not a translation.</summary>
        [Theory]
        [MemberData(nameof(AuditReasonFields))]
        public void The_two_languages_are_not_the_same_string(string entityType, string field)
        {
            Assert.NotEqual(
                UserMessage.FieldName(entityType, field, arabic: false),
                UserMessage.FieldName(entityType, field, arabic: true));
        }

        /// <summary>Arabic must be Arabic. A Latin-script "translation" is the defect wearing a hat.</summary>
        [Theory]
        [MemberData(nameof(AuditReasonFields))]
        public void The_arabic_name_is_written_in_arabic(string entityType, string field)
        {
            var arabic = UserMessage.FieldName(entityType, field, arabic: true);

            Assert.True(
                arabic.Any(c => c >= '؀' && c <= 'ۿ'),
                $"{entityType}.{field}'s Arabic name has no Arabic letters in it: \"{arabic}\".");
        }
    }
}
