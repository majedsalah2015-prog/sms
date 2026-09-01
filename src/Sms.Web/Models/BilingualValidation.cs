using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace Sms.Web.Models
{
    /// <summary>
    /// Validation attributes that speak the reader's language.
    /// <para>
    /// The framework's own <c>[Required]</c> composes "The Username field is required." out of a
    /// hard-coded English sentence and the <c>[Display]</c> name, and it does so no matter what
    /// culture the request is in. On the Arabic login screen that is what an administrator was being
    /// shown — the same defect as an untranslated engine refusal, arriving through a different door,
    /// and with the same consequence: a rejection the reader cannot act on.
    /// </para>
    /// <para>
    /// The usual fix is a resource file per culture. This product does not have one: every other
    /// user-visible string here is a literal pair chosen at the Web boundary through <c>T(en, ar)</c>,
    /// and one file of .resx keys for fourteen messages would be a second convention to maintain and
    /// forget. So these subclasses carry the pair the same way everything else does, and name the
    /// field themselves rather than borrowing <c>[Display]</c> — which is single-language by
    /// construction and cannot be made otherwise.
    /// </para>
    /// <para>
    /// The message reaches the client too: unobtrusive validation reads it out of
    /// <c>data-val-required</c>, so the browser-side check says the same sentence in the same
    /// language as the server-side one.
    /// </para>
    /// </summary>
    public static class BilingualValidation
    {
        internal static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;
    }

    /// <summary>Carries the field's own name in both languages, since <c>[Display]</c> can hold only one.</summary>
    public abstract class BilingualFieldAttribute : ValidationAttribute
    {
        protected BilingualFieldAttribute(string en, string ar)
        {
            En = en;
            Ar = ar;
        }

        public string En { get; }

        public string Ar { get; }

        /// <summary>The field's name as the current reader would say it.</summary>
        protected string Field => BilingualValidation.IsArabic ? Ar : En;
    }

    /// <summary>
    /// <c>[Required]</c> that reads "حقل «اسم المستخدم» مطلوب." to an Arabic reader and
    /// "The username field is required." to an English one.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class RequiredFieldAttribute : BilingualFieldAttribute
    {
        public RequiredFieldAttribute(string en, string ar)
            : base(en, ar)
        {
        }

        public override bool IsValid(object? value) =>
            value switch
            {
                null => false,
                string text => !string.IsNullOrWhiteSpace(text),
                _ => true,
            };

        public override string FormatErrorMessage(string name) =>
            BilingualValidation.IsArabic ? $"حقل «{Ar}» مطلوب." : $"The {En} field is required.";
    }

    /// <summary>
    /// <c>[StringLength]</c> with both bounds said in words rather than in the framework's
    /// "must be a string with a minimum length of…" — which is English-only and, in either language,
    /// longer than the rule it states.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
    public sealed class TextLengthAttribute : BilingualFieldAttribute
    {
        public TextLengthAttribute(string en, string ar, int minimum, int maximum)
            : base(en, ar)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public int Minimum { get; }

        public int Maximum { get; }

        public override bool IsValid(object? value)
        {
            // An empty value is [RequiredField]'s question, not this one's: two refusals for one
            // empty box is noise, and the required rule says it better.
            if (value is not string text || text.Length == 0)
            {
                return true;
            }

            return text.Length >= Minimum && text.Length <= Maximum;
        }

        public override string FormatErrorMessage(string name) =>
            BilingualValidation.IsArabic
                ? (Minimum == Maximum
                    ? $"يجب أن يتكوّن «{Ar}» من {Minimum} خانة بالضبط."
                    : $"يجب أن يتكوّن «{Ar}» من {Minimum} إلى {Maximum} خانة.")
                : (Minimum == Maximum
                    ? $"The {En} must be exactly {Minimum} characters."
                    : $"The {En} must be between {Minimum} and {Maximum} characters.");
    }

    /// <summary>
    /// Two boxes that must agree — a new password and its confirmation. The framework's
    /// <c>[Compare]</c> names the <em>property</em> in its message ("'ConfirmPassword' and
    /// 'NewPassword' do not match"), which is a CLR name shown to a person, in English, twice.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
    public sealed class MustMatchAttribute : BilingualFieldAttribute
    {
        public MustMatchAttribute(string en, string ar, string otherProperty)
            : base(en, ar)
        {
            OtherProperty = otherProperty;
        }

        public string OtherProperty { get; }

        public override string FormatErrorMessage(string name) =>
            BilingualValidation.IsArabic ? $"{Ar} غير متطابقين." : $"The {En} do not match.";

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var other = validationContext.ObjectType.GetProperty(OtherProperty);
            if (other == null)
            {
                // A rename that broke the pairing. Refusing loudly beats silently accepting any two
                // values as a match, which is what returning success here would mean.
                return new ValidationResult($"Unknown property {OtherProperty}.");
            }

            var expected = other.GetValue(validationContext.ObjectInstance, null);
            return Equals(value, expected)
                ? ValidationResult.Success
                : new ValidationResult(FormatErrorMessage(validationContext.DisplayName), new[] { validationContext.MemberName ?? OtherProperty });
        }
    }
}
