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
        /// <summary>
        /// Edits identity/contact fields (T1 audited). Changing <paramref name="primaryIdNo"/>
        /// needs <c>IAuditContext.Reason</c> set first — it is an identity field
        /// (BR-PAR-009) and the register deduplicates on it (BR-PAR-002).
        /// </summary>
        Task<Parent> UpdateParentAsync(
            int parentId, string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar",
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null,
            ParentLifeStatus lifeStatus = ParentLifeStatus.Alive, string? lifeStatusNote = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Sets where the family lives: the locality, and the quarter inside it where the locality has
        /// any. Passing a neighbourhood that does not belong to the given area is refused rather than
        /// stored, since nothing else in the system would ever catch the mismatch.
        /// </summary>
        Task SetResidenceAsync(int parentId, int? residenceAreaId, int? neighbourhoodId, CancellationToken cancellationToken = default);

        Task<Parent> RegisterParentAsync(
            string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar",
            int? primaryIdTypeLookupId = null, string? primaryIdNo = null,
            ParentLifeStatus lifeStatus = ParentLifeStatus.Alive, string? lifeStatusNote = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Hard-deletes a parent file. Refused (InvalidOperationException) while the parent is still an
        /// active guardian of any student (unlink first, BR-GLB-004) or while financial / health /
        /// discipline records reference it. Ended guardian links go with the parent; admission
        /// applications that pointed at the parent are left without a parent.
        /// </summary>
        Task DeleteParentAsync(int parentId, CancellationToken cancellationToken = default);
    }
}
