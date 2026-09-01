using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Payments;

namespace Sms.Application.Payments
{
    /// <summary>
    /// The catalogue of accounts student money is collected into — the school's
    /// bank accounts and cash boxes (doc/Modules/21 §3 BR-PAY-002).
    /// <para>
    /// Standalone admin: configuration, not a step inside a larger money
    /// transaction, so each method saves itself.
    /// </para>
    /// </summary>
    public interface ICollectionAccountAdmin
    {
        /// <summary>
        /// Adds an account. Throws <see cref="Common.Exceptions.DuplicateCollectionAccountCodeException"/>
        /// on a code the school already uses, and
        /// <see cref="Common.Exceptions.BankCollectionAccountNeedsNumberException"/>
        /// when a bank account carries neither an account number nor an IBAN.
        /// </summary>
        Task<CollectionAccount> DefineAsync(
            string code, string nameAr, string nameEn, CollectionAccountKind kind,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Changes an account. <paramref name="kind"/> is deliberately absent:
        /// a bank account does not become a cash box, and the receipts already
        /// pointing at it would be silently re-classified if it could.
        /// </summary>
        Task UpdateAsync(
            int id, string code, string nameAr, string nameEn,
            int? bankLookupId = null, string? bankName = null, string? accountNo = null, string? iban = null,
            string? glExportCode = null, int displayOrder = 0, bool isDefault = false,
            CancellationToken cancellationToken = default);

        /// <summary>Retires an account (BR-GLB-005 — no delete). Its receipts keep naming it.</summary>
        Task DeactivateAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Puts a retired account back in the cashier's picker.</summary>
        Task ReactivateAsync(int id, CancellationToken cancellationToken = default);
    }
}
