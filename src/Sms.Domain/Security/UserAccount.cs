using Sms.Domain.Common;

namespace Sms.Domain.Security
{
    /// <summary>
    /// sec.UserAccount. One person = one account (BR-GLB-002, doc 06 §2);
    /// accounts are deactivated, never deleted. Credential fields
    /// (BR-SEC-001..006) arrive with the authentication slice of E-003.
    /// </summary>
    public class UserAccount : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string UserName { get; set; } = string.Empty;

        public AccountType AccountType { get; set; }

        /// <summary>Link to the person entity once People modules (S2) exist.</summary>
        public int? PersonId { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
