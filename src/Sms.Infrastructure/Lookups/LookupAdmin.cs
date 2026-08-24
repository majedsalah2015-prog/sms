using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Lookups;
using Sms.Domain.Common;
using Sms.Domain.Lookups;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Lookups
{
    /// <summary>
    /// Standalone admin operations — saves themselves, no larger transaction to ride.
    /// <para>
    /// <b>Every lookup here reads past the soft-active filter, on purpose.</b> A
    /// deactivated category or value still owns its code and is still pointed at by
    /// rows already saved, so the administrator's own screens have to be able to see
    /// it: reading through the filter made "reactivate" try to insert a second row
    /// with the same <c>(category, code)</c> and die on the unique index, and made
    /// "deactivate" throw "sequence contains no elements" on a value that was already
    /// retired. Both reached the operator as an untranslated crash. Reading past the
    /// filter costs the tenant predicate, which is why each of these re-applies
    /// <c>SchoolId</c> by hand — dropping that would let one school edit another's
    /// reference data.
    /// </para>
    /// </summary>
    public class LookupAdmin : ILookupAdmin
    {
        private readonly AppDbContext _db;

        public LookupAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<LookupCategory> DefineCategoryAsync(
            string code, LookupCategoryTier tier, string nameAr, string nameEn, CancellationToken cancellationToken = default)
        {
            var category = await _db.LookupCategories.IgnoreQueryFilters()
                .SingleOrDefaultAsync(c => c.SchoolId == _db.CurrentSchoolId && c.Code == code, cancellationToken);
            if (category == null)
            {
                category = new LookupCategory { Code = code };
                _db.LookupCategories.Add(category);
            }

            category.Tier = tier;
            category.Name = new LocalizedName(nameAr, nameEn);
            category.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return category;
        }

        public async Task<LookupValue> DefineValueAsync(
            string categoryCode, string code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default)
        {
            var category = await _db.LookupCategories.IgnoreQueryFilters()
                .SingleAsync(c => c.SchoolId == _db.CurrentSchoolId && c.Code == categoryCode, cancellationToken);

            // Past the filter: a retired value keeps its code, so an "edit" or a
            // "reactivate" must find it rather than insert a duplicate beside it.
            var value = await _db.LookupValues.IgnoreQueryFilters().SingleOrDefaultAsync(
                v => v.SchoolId == _db.CurrentSchoolId && v.LookupCategoryId == category.Id && v.Code == code, cancellationToken);
            if (value == null)
            {
                value = new LookupValue { LookupCategoryId = category.Id, Code = code };
                _db.LookupValues.Add(value);
            }

            value.Name = new LocalizedName(nameAr, nameEn);
            value.SortOrder = sortOrder;
            value.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return value;
        }

        public async Task<LookupValue> EnsureValueAsync(
            string categoryCode, string code, string nameAr, string nameEn, int sortOrder, CancellationToken cancellationToken = default)
        {
            var category = await _db.LookupCategories.IgnoreQueryFilters()
                .SingleAsync(c => c.SchoolId == _db.CurrentSchoolId && c.Code == categoryCode, cancellationToken);

            // IgnoreQueryFilters: a deactivated value still holds its code, so re-adding it would fail
            // on the unique index — and reviving it is exactly the overwrite this method exists to avoid.
            var existing = await _db.LookupValues.IgnoreQueryFilters().SingleOrDefaultAsync(
                v => v.SchoolId == _db.CurrentSchoolId && v.LookupCategoryId == category.Id && v.Code == code, cancellationToken);
            if (existing != null)
            {
                return existing;
            }

            var value = new LookupValue
            {
                LookupCategoryId = category.Id,
                Code = code,
                Name = new LocalizedName(nameAr, nameEn),
                SortOrder = sortOrder,
                IsActive = true,
            };
            _db.LookupValues.Add(value);
            await _db.SaveChangesAsync(cancellationToken);
            return value;
        }

        public async Task DeactivateValueAsync(int lookupValueId, CancellationToken cancellationToken = default)
        {
            // Past the filter so retiring an already-retired value is the no-op the
            // operator expects, not "sequence contains no elements".
            var value = await _db.LookupValues.IgnoreQueryFilters()
                .SingleAsync(v => v.SchoolId == _db.CurrentSchoolId && v.Id == lookupValueId, cancellationToken);
            value.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
