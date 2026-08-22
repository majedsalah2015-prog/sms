using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Lookups;

namespace Sms.Application.Lookups
{
    /// <summary>doc/Modules/01 §8 "Lookup management" backing (screen deferred, the upsert itself is core).</summary>
    public interface ILookupAdmin
    {
        Task<LookupCategory> DefineCategoryAsync(
            string code, LookupCategoryTier tier, string nameAr, string nameEn, CancellationToken cancellationToken = default);

        /// <summary>Creates the value, or overwrites the existing one's name, order and active flag. The administrator's edit path.</summary>
        Task<LookupValue> DefineValueAsync(
            string categoryCode, string code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates the value only if that code is not already present, and leaves an existing one
        /// exactly as it is. The seeding path.
        /// <para>
        /// The distinction is not cosmetic: a seeder that re-ran through <see cref="DefineValueAsync"/>
        /// would silently undo every rename an administrator had made to a product-tier value, and
        /// reactivate every value they had deactivated — a school that renamed a nationality would
        /// find it renamed back by the next deployment, with nothing in the audit trail to explain it.
        /// </para>
        /// </summary>
        Task<LookupValue> EnsureValueAsync(
            string categoryCode, string code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>BR-SET-002: status change, never a physical delete.</summary>
        Task DeactivateValueAsync(int lookupValueId, CancellationToken cancellationToken = default);
    }
}
