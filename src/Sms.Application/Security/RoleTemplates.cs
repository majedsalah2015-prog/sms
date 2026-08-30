using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// The seeded role templates (doc 06 §4.3) this system names in code, and the one mapping over
    /// them that is a rule rather than a choice: which role a portal account holds.
    /// <para>
    /// A staff account's roles are a decision, and doc 06 §7 keeps <c>SYS/Users/Create</c> apart
    /// from <c>SYS/UserRoles</c> precisely so that deciding who exists is not the same authority as
    /// deciding what they may do. A parent's is not a decision at all: the account type was fixed
    /// when the account was created, <c>PortalAreaFilter</c> already confines the account to the
    /// portal (BR-SEC-010), and exactly one seeded role opens it. Granting it here therefore widens
    /// nobody's authority — it only makes the account type mean what it says.
    /// </para>
    /// <para>
    /// Leaving that last step to be remembered by hand is what let a school provision a parent
    /// (BR-SEC-006), hand over a working password, and have the parent sign in successfully and
    /// meet a bare not-found at <c>/portal</c> — the deny-by-default screen guard doing exactly
    /// what it should to an account holding no permission at all.
    /// </para>
    /// </summary>
    public static class RoleTemplates
    {
        /// <summary>doc 06 §4.3's parent template — the portal's own audience.</summary>
        public const string Parent = "PARENT";

        /// <summary>doc 06 §4.3's student template — the portal minus the family's money.</summary>
        public const string Student = "STUDENT";

        /// <summary>
        /// The role a portal account cannot function without, or <c>null</c> for an account type
        /// whose roles are the school's to choose.
        /// </summary>
        public static string? ForPortalAccount(AccountType accountType) => accountType switch
        {
            AccountType.Parent => Parent,
            AccountType.Student => Student,
            _ => null,
        };
    }
}
