using System.ComponentModel.DataAnnotations;

namespace Sms.Web.Models
{
    public sealed class LoginViewModel
    {
        [Required]
        [Display(Name = "Username")]
        public string UserName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public sealed class TwoFactorViewModel
    {
        [Required]
        [StringLength(8, MinimumLength = 6)]
        public string Code { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public sealed class ChangePasswordViewModel
    {
        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword))]
        public string ConfirmPassword { get; set; } = string.Empty;

        /// <summary>True when BR-SEC-005 forced the user here (first login / admin reset).</summary>
        public bool IsForced { get; set; }
    }
}
