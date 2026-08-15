using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Lookups;
using Sms.Domain.Common;
using Sms.Domain.Lookups;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Lookups
{
    /// <summary>Standalone admin operations — saves themselves, no larger transaction to ride.</summary>
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
            var category = await _db.LookupCategories.SingleOrDefaultAsync(c => c.Code == code, cancellationToken);
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
            var category = await _db.LookupCategories.SingleAsync(c => c.Code == categoryCode, cancellationToken);

            var value = await _db.LookupValues.SingleOrDefaultAsync(
                v => v.LookupCategoryId == category.Id && v.Code == code, cancellationToken);
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

        public async Task DeactivateValueAsync(int lookupValueId, CancellationToken cancellationToken = default)
        {
            var value = await _db.LookupValues.SingleAsync(v => v.Id == lookupValueId, cancellationToken);
            value.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
