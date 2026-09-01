using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Payments;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Payments
{
    /// <summary>
    /// Standalone admin over the collection-account catalogue — saves itself,
    /// no larger transaction to ride (doc/Modules/21 §3 BR-PAY-002).
    /// </summary>
    public class CollectionAccountAdmin : ICollectionAccountAdmin
    {
        private readonly AppDbContext _db;

        public CollectionAccountAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<CollectionAccount> DefineAsync(
            string code, string nameAr, string nameEn, CollectionAccountKind kind,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false,
            CancellationToken cancellationToken = default)
        {
            await GuardCodeAsync(code, existingId: null, cancellationToken);
            GuardBankNumber(kind, accountNo, iban);

            var account = new CollectionAccount
            {
                Code = code,
                NameAr = nameAr,
                NameEn = nameEn,
                Kind = kind,
                BankLookupId = kind == CollectionAccountKind.Bank ? bankLookupId : null,
                BankName = kind == CollectionAccountKind.Bank ? bankName : null,
                AccountNo = kind == CollectionAccountKind.Bank ? accountNo : null,
                Iban = kind == CollectionAccountKind.Bank ? iban : null,
                GlExportCode = glExportCode,
                DisplayOrder = displayOrder,
                IsDefault = isDefault,
                IsActive = true,
            };
            _db.CollectionAccounts.Add(account);
            await _db.SaveChangesAsync(cancellationToken);

            // After the save, so the row has an id to exclude itself by.
            await ApplyDefaultAsync(account, isDefault, cancellationToken);
            return account;
        }

        public async Task UpdateAsync(
            int id, string code, string nameAr, string nameEn,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false,
            CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: a retired account is still edited — a bank changes an IBAN on an
            // account the school closed last term and the old receipts must still read back right.
            var account = await _db.CollectionAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == id, cancellationToken);
            await GuardCodeAsync(code, existingId: id, cancellationToken);
            GuardBankNumber(account.Kind, accountNo, iban);

            account.Code = code;
            account.NameAr = nameAr;
            account.NameEn = nameEn;
            account.BankLookupId = account.Kind == CollectionAccountKind.Bank ? bankLookupId : null;
            account.BankName = account.Kind == CollectionAccountKind.Bank ? bankName : null;
            account.AccountNo = account.Kind == CollectionAccountKind.Bank ? accountNo : null;
            account.Iban = account.Kind == CollectionAccountKind.Bank ? iban : null;
            account.GlExportCode = glExportCode;
            account.DisplayOrder = displayOrder;
            account.IsDefault = isDefault;

            await _db.SaveChangesAsync(cancellationToken);
            await ApplyDefaultAsync(account, isDefault, cancellationToken);
        }

        public async Task DeactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            var account = await _db.CollectionAccounts.SingleAsync(a => a.Id == id, cancellationToken);

            // No in-use check, unlike the fee-category catalogue. An account is retired precisely
            // because it holds history: the receipts that named it keep naming it, which is what
            // ISoftActiveFiltered is for. Refusing here would mean a school could never close a
            // bank account it had ever collected into.
            account.IsActive = false;
            account.IsDefault = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReactivateAsync(int id, CancellationToken cancellationToken = default)
        {
            var account = await _db.CollectionAccounts.IgnoreQueryFilters().SingleAsync(a => a.Id == id, cancellationToken);
            account.IsActive = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>At most one default per kind — a picker with two pre-selections has none.</summary>
        private async Task ApplyDefaultAsync(CollectionAccount account, bool isDefault, CancellationToken cancellationToken)
        {
            if (!isDefault)
            {
                return;
            }

            var others = await _db.CollectionAccounts.IgnoreQueryFilters()
                .Where(a => a.SchoolId == _db.CurrentSchoolId && a.Kind == account.Kind && a.Id != account.Id && a.IsDefault)
                .ToListAsync(cancellationToken);
            if (others.Count == 0)
            {
                return;
            }

            foreach (var other in others)
            {
                other.IsDefault = false;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>IgnoreQueryFilters: a retired account still owns its code, or reactivating it would collide.</summary>
        private async Task GuardCodeAsync(string code, int? existingId, CancellationToken cancellationToken)
        {
            var taken = await _db.CollectionAccounts.IgnoreQueryFilters()
                .AnyAsync(a => a.SchoolId == _db.CurrentSchoolId && a.Code == code && (existingId == null || a.Id != existingId), cancellationToken);
            if (taken)
            {
                throw new DuplicateCollectionAccountCodeException(code);
            }
        }

        private static void GuardBankNumber(CollectionAccountKind kind, string? accountNo, string? iban)
        {
            if (kind == CollectionAccountKind.Bank && string.IsNullOrWhiteSpace(accountNo) && string.IsNullOrWhiteSpace(iban))
            {
                throw new BankCollectionAccountNeedsNumberException();
            }
        }
    }
}
