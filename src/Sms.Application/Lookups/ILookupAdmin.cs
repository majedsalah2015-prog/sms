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

        Task<LookupValue> DefineValueAsync(
            string categoryCode, string code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default);

        /// <summary>BR-SET-002: status change, never a physical delete.</summary>
        Task DeactivateValueAsync(int lookupValueId, CancellationToken cancellationToken = default);
    }
}
