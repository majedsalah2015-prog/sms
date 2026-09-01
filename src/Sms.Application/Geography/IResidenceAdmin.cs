using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Geography;

namespace Sms.Application.Geography
{
    /// <summary>
    /// Maintains the residence constants a student's and a parent's address are chosen from —
    /// محافظة → منطقة → حي (owner request, 2026-08-31; the hierarchy itself is not in the closed
    /// Analysis v1.0 docs).
    /// <para>
    /// The three levels arrive seeded from PCBS and the seeder only ever adds, so the school owns
    /// every correction after that: a misspelt quarter, a new neighbourhood, a locality the pack
    /// never listed. Until this port existed, the only way to record one was a hand-written INSERT.
    /// </para>
    /// <para>
    /// Standalone admin shape — each method saves itself. There is no larger transaction for a
    /// reference-list edit to ride, and the screen behind it edits one row at a time.
    /// </para>
    /// <para>
    /// <b>There is no remove.</b> BR-SET-002 / BR-GLB-005: a row an address already points at is
    /// deactivated, never deleted, and stays readable on the records that name it.
    /// </para>
    /// </summary>
    public interface IResidenceAdmin
    {
        /// <summary>
        /// Adds a governorate when <paramref name="id"/> is null, otherwise corrects the names and
        /// the order of that one. The code is the stable key and is never changed by an edit.
        /// </summary>
        Task<Governorate> SaveGovernorateAsync(
            int? id, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>Adds or corrects a locality inside <paramref name="governorateId"/>.</summary>
        Task<ResidenceArea> SaveLocalityAsync(
            int? id, int governorateId, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds or corrects a quarter inside <paramref name="localityId"/>. A locality with no
        /// quarters is the normal case — there, the locality is the whole address.
        /// </summary>
        Task<Neighbourhood> SaveQuarterAsync(
            int? id, int localityId, string? code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Takes a governorate out of the pickers, or puts it back. Its localities are not touched:
        /// they disappear with it from the cascading picker anyway, and reactivating a governorate
        /// that had silently retired thirty localities would be a second surprise.
        /// </summary>
        Task SetGovernorateActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>Takes a locality out of the pickers, or puts it back.</summary>
        Task SetLocalityActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);

        /// <summary>Takes a quarter out of the pickers, or puts it back.</summary>
        Task SetQuarterActiveAsync(int id, bool isActive, CancellationToken cancellationToken = default);
    }
}
