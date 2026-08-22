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
