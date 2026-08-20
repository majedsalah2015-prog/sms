using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Parents;

namespace Sms.Application.Parents
{
    /// <summary>
    /// doc/Modules/11 §8 Parent File screens backing (screens deferred, the
    /// operation is core). Issues the parent's permanent file number via
    /// E-006's INumberIssuer (series "PAR"). Registrar-direct creation path
    /// only — the Admissions-portal dedup pipeline (BR-PAR-002) and merge
    /// tool (BR-PAR-003) are deferred.
    /// </summary>
    public interface IParentAdmin
    {
        /// <summary>Edits identity/contact fields (T1 audited).</summary>
        Task<Parent> UpdateParentAsync(
            int parentId, string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar", CancellationToken cancellationToken = default);

        Task<Parent> RegisterParentAsync(
            string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar", CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-deletes a parent file. Refused (InvalidOperationException) while the parent is still an
        /// active guardian of any student (unlink first, BR-GLB-004) or while financial / health /
        /// discipline records reference it. Ended guardian links go with the parent; admission
        /// applications that pointed at the parent are left without a parent.
        /// </summary>
        Task DeleteParentAsync(int parentId, CancellationToken cancellationToken = default);
    }
}
