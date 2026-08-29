using System.ComponentModel.DataAnnotations;

namespace Sms.Web.Models
{
    // The validation attributes here are this product's own (see BilingualValidation.cs), not the
    // framework's. [Required] composes an English sentence whatever the culture, and the login
    // screen is the one place in the system a person reaches before anything else — being told
    // "The Username field is required." in the middle of an Arabic page is the first thing a new
    // administrator would see of this system.
    //
    // [DataType(DataType.Password)] stays: it emits type="password" and says nothing to the reader.

    public sealed class LoginViewModel
    {
        [RequiredField("username", "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [RequiredField("password", "كلمة المرور")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // Nullable, and not because it can be unknown. MVC infers a "required" rule from a
        // non-nullable value type and writes the framework's English sentence into the page as
        // data-val-required, where the browser's own validator reads it — on a checkbox, which
        // always posts a value and so can never fail the rule. Making it nullable removes the
        // inferred rule at its source rather than translating a message nobody can ever see.
        public bool? RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public sealed class TwoFactorViewModel
    {
        [RequiredField("verification code", "رمز التحقق")]
        [TextLength("verification code", "رمز التحقق", 6, 8)]
        public string Code { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public sealed class ChangePasswordViewModel
    {
        [RequiredField("current password", "كلمة المرور الحالية")]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [RequiredField("new password", "كلمة المرور الجديدة")]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [RequiredField("password confirmation", "تأكيد كلمة المرور")]
        [DataType(DataType.Password)]
        [MustMatch("two passwords", "كلمتا المرور", nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>True when BR-SEC-005 forced the user here (first login / admin reset).</summary>
        public bool IsForced { get; set; }
    }
}
