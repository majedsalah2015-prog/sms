using System;
using Sms.Domain.Security;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// The one change the role designer will not make: leaving the school with nobody who can
    /// administer permissions. There is no way back from it inside the product — the screen that
    /// would fix it is the screen that just became unreachable — so it is refused rather than warned
    /// about, on every path that could cause it.
    /// </summary>
    public class LastPermissionAdministratorException : InvalidOperationException
    {
        public LastPermissionAdministratorException(string what)
            : base($"{what} would leave nobody able to administer permissions (doc 06 §4). "
                   + "Grant another active account a role holding Configure on the Roles screen first.")
        {
        }
    }

    /// <summary>BR-SEC: role codes are the key grants, assignments and the seeder all use.</summary>
    public class DuplicateRoleCodeException : InvalidOperationException
    {
        public DuplicateRoleCodeException(string code)
            : base($"A role with code '{code}' already exists.")
        {
        }
    }

    /// <summary>
    /// doc 06 §2: the user name is what somebody types at a keyboard, so the set of names this
    /// product accepts is deliberately narrow — one case, no spaces, and nothing that has to be
    /// described over a telephone.
    /// </summary>
    public class InvalidUserNameException : InvalidOperationException
    {
        public InvalidUserNameException(string userName)
            : base($"'{userName}' is not a usable user name: {Sms.Application.Security.UserNameRules.MinLength}"
                   + $"-{Sms.Application.Security.UserNameRules.MaxLength} characters, letters, digits, and . _ - @ only, starting with a letter or a digit.")
        {
            UserName = userName;
        }

        public string UserName { get; }
    }

    /// <summary>
    /// The name is taken. Deactivated accounts count: the name still belongs to the person who had
    /// it, and reissuing it would make a year of audit entries read as though they were theirs.
    /// </summary>
    public class DuplicateUserNameException : InvalidOperationException
    {
        public DuplicateUserNameException(string userName)
            : base($"The user name '{userName}' is already taken (doc 06 §2).")
        {
            UserName = userName;
        }

        public string UserName { get; }
    }

    /// <summary>
    /// BR-GLB-002 / BR-SYS-001: one person, one account. A second login for the same person splits
    /// their history in two and leaves neither half answering "what did they do".
    /// </summary>
    public class PersonAlreadyHasAccountException : InvalidOperationException
    {
        public PersonAlreadyHasAccountException(Sms.Application.Security.ProvisionableAccountType accountType, int personId)
            : base($"{accountType} {personId} already has an account (BR-GLB-002 — one person, one account).")
        {
            AccountType = accountType;
            PersonId = personId;
        }

        public Sms.Application.Security.ProvisionableAccountType AccountType { get; }

        public int PersonId { get; }
    }

    /// <summary>
    /// Deactivating one's own account is refused rather than confirmed. It is the one mistake on
    /// this screen that cannot be undone from this screen, and an administrator who genuinely
    /// intends to leave can be deactivated by the colleague who inherits the job.
    /// </summary>
    public class SelfAccountDeactivationException : InvalidOperationException
    {
        public SelfAccountDeactivationException()
            : base("You cannot deactivate the account you are signed in with.")
        {
        }
    }

    /// <summary>
    /// The account is deactivated, so there is no sign-in for a new password to be used at.
    /// Reactivate it first — resetting the password of a deactivated account only looks like it
    /// restored the person's access.
    /// </summary>
    public class InactiveAccountException : InvalidOperationException
    {
        public InactiveAccountException(string userName)
            : base($"Account '{userName}' is deactivated; reactivate it before issuing a password.")
        {
            UserName = userName;
        }

        public string UserName { get; }
    }

    /// <summary>
    /// A grant must name a triple <see cref="Sms.Application.Security.ScreenCatalog"/> defines.
    /// Anything else would create a permission row no screen ever checks, which reads on the designer
    /// as access the holder does not actually have.
    /// </summary>
    public class UncataloguedPermissionException : InvalidOperationException
    {
        public UncataloguedPermissionException(string moduleCode, string screenCode, ActionVerb action)
            : base($"'{moduleCode}/{screenCode}/{action}' is not in the screen catalogue, so no screen can ever check it.")
        {
        }
    }
}
