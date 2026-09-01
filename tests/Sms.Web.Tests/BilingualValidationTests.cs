using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The other door an English refusal comes through.
    /// <para>
    /// <c>TranslatedRefusalTests</c> stops a controller printing an engine's English sentence at a
    /// user. But <c>[Required]</c> composes its own — "The Username field is required." — inside the
    /// framework, in English, whatever culture the request is in, and nothing in the source of the
    /// screen shows it happening. On the Arabic login page that was the first sentence a new
    /// administrator would ever be shown by this system.
    /// </para>
    /// <para>
    /// So the framework's single-language validation attributes are not used on this application's
    /// view models at all; <c>Sms.Web.Models.BilingualValidation</c> carries the pair the same way
    /// every other user-visible string here does, and this test is what keeps the next view model
    /// from being written by copying an older one.
    /// </para>
    /// </summary>
    public class BilingualValidationTests
    {
        /// <summary>
        /// Attributes that put a message on the screen in one language. <c>[DataType]</c> is absent
        /// on purpose: it chooses an input type and says nothing to the reader.
        /// </summary>
        private static readonly Regex SingleLanguage = new(
            @"\[(Required|StringLength|MinLength|MaxLength|Range|Compare|EmailAddress|Phone|Url|CreditCard|RegularExpression|Display)\b",
            RegexOptions.Compiled);

        private static string ModelsDirectory([CallerFilePath] string thisFile = "")
        {
            var repo = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", ".."));
            return Path.Combine(repo, "src", "Sms.Web", "Models");
        }

        public static IEnumerable<object[]> ModelFiles() =>
            Directory.EnumerateFiles(ModelsDirectory(), "*.cs", SearchOption.AllDirectories)
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => new object[] { Path.GetRelativePath(ModelsDirectory(), path) });

        [Theory]
        [MemberData(nameof(ModelFiles))]
        public void No_view_model_uses_a_single_language_validation_attribute(string fileName)
        {
            var path = Path.Combine(ModelsDirectory(), fileName);
            var offenders = File.ReadAllLines(path)
                .Select((text, index) => (Text: text.Trim(), Line: index + 1))
                .Where(l => SingleLanguage.IsMatch(l.Text)
                    && !l.Text.StartsWith("//", StringComparison.Ordinal))
                .ToList();

            Assert.True(
                offenders.Count == 0,
                $"{fileName} uses a framework validation attribute, whose message is English in every culture:{Environment.NewLine}"
                + string.Join(Environment.NewLine, offenders.Select(o => $"  line {o.Line}: {o.Text}"))
                + $"{Environment.NewLine}Use RequiredField / TextLength / MustMatch from BilingualValidation.cs instead.");
        }

        // ------------------------------------------------------------------ the attributes themselves

        public static IEnumerable<object[]> Cultures() => new[]
        {
            new object[] { "ar-SA", true },
            new object[] { "en-US", false },
        };

        [Theory]
        [MemberData(nameof(Cultures))]
        public void A_missing_required_field_is_refused_in_the_readers_language(string culture, bool arabic)
        {
            using var _ = new Culture(culture);
            var message = new RequiredFieldAttribute("username", "اسم المستخدم").FormatErrorMessage("UserName");

            Assert.Equal(arabic, message.Any(c => c >= '؀' && c <= 'ۿ'));
            Assert.DoesNotContain("UserName", message, StringComparison.Ordinal);
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void A_wrong_length_is_refused_in_the_readers_language(string culture, bool arabic)
        {
            using var _ = new Culture(culture);
            var attribute = new TextLengthAttribute("verification code", "رمز التحقق", 6, 8);

            Assert.Equal(arabic, attribute.FormatErrorMessage("Code").Any(c => c >= '؀' && c <= 'ۿ'));
            Assert.False(attribute.IsValid("12345"));
            Assert.True(attribute.IsValid("123456"));
            Assert.True(attribute.IsValid("12345678"));
            Assert.False(attribute.IsValid("123456789"));

            // Emptiness is [RequiredField]'s to refuse; two messages for one empty box is noise.
            Assert.True(attribute.IsValid(string.Empty));
        }

        [Theory]
        [MemberData(nameof(Cultures))]
        public void Two_passwords_that_disagree_are_refused_in_the_readers_language(string culture, bool arabic)
        {
            using var _ = new Culture(culture);
            var model = new ChangePasswordViewModel { NewPassword = "Aa1!aaaa", ConfirmPassword = "Bb2!bbbb" };
            var context = new ValidationContext(model) { MemberName = nameof(ChangePasswordViewModel.ConfirmPassword) };
            var results = new List<ValidationResult>();

            var valid = Validator.TryValidateProperty(model.ConfirmPassword, context, results);

            Assert.False(valid);
            var message = results.Single().ErrorMessage ?? string.Empty;
            Assert.Equal(arabic, message.Any(c => c >= '؀' && c <= 'ۿ'));
            Assert.DoesNotContain(nameof(ChangePasswordViewModel.NewPassword), message, StringComparison.Ordinal);
        }

        [Fact]
        public void The_login_form_refuses_an_empty_username_in_arabic()
        {
            using var _ = new Culture("ar-SA");
            var empty = new LoginViewModel();
            var results = new List<ValidationResult>();

            Validator.TryValidateObject(empty, new ValidationContext(empty), results, validateAllProperties: true);

            Assert.NotEmpty(results);
            Assert.All(results, r => Assert.Contains("مطلوب", r.ErrorMessage ?? string.Empty, StringComparison.Ordinal));
        }

        /// <summary>Sets the request culture the way the localization middleware would, and puts it back.</summary>
        private sealed class Culture : IDisposable
        {
            private readonly CultureInfo _culture;
            private readonly CultureInfo _uiCulture;

            public Culture(string name)
            {
                _culture = CultureInfo.CurrentCulture;
                _uiCulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentCulture = new CultureInfo(name);
                CultureInfo.CurrentUICulture = new CultureInfo(name);
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture = _culture;
                CultureInfo.CurrentUICulture = _uiCulture;
            }
        }
    }
}
