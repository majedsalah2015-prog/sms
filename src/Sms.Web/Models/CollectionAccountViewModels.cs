using System;
using System.Collections.Generic;
using Sms.Application.GlExport;
using Sms.Domain.Payments;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/21 §3 BR-PAY-002 — the catalogue of accounts student money is
    /// collected into, and the picker the cashier screen renders from it.
    /// <para>
    /// Its own file rather than another class in <c>FinanceViewModels</c>: the
    /// destination of a payment is a small, self-contained idea and it is
    /// easier to read beside the screen that edits it.
    /// </para>
    /// </summary>
    public sealed class CollectionAccountCatalogViewModel
    {
        public IReadOnlyList<Row> Rows { get; set; } = Array.Empty<Row>();

        /// <summary>The row whose edit form is open, if any.</summary>
        public int? EditId { get; set; }

        /// <summary>The "Bank" lookup catalogue, for the bank picker. Empty when the school has not filled it in — the free-text name stands.</summary>
        public IReadOnlyList<(int Id, string Ar, string En)> Banks { get; set; } = Array.Empty<(int, string, string)>();

        /// <summary>The attached ledger's postable accounts, or empty when no ledger is attached (see <see cref="IGlAccountDirectory"/>).</summary>
        public IReadOnlyList<GlAccountOption> GlAccounts { get; set; } = Array.Empty<GlAccountOption>();

        /// <summary>
        /// One account, with what has been collected into it. The counts are
        /// what make a retired account safe to read: they say out loud how much
        /// history is pointing at the row before anyone edits its IBAN.
        /// </summary>
        public sealed record Row(CollectionAccount Account, string? BankAr, string? BankEn, int ReceiptCount, decimal Collected);
    }

    /// <summary>
    /// One destination as the cashier screen offers it — flattened so the view
    /// renders a bank's name without a second lookup, and carries the account
    /// number so the cashier can read it out to a parent asking where to send
    /// the transfer, which is the question the picker exists to answer.
    /// </summary>
    public sealed record CollectionAccountOption(
        int Id, string Code, string NameAr, string NameEn, CollectionAccountKind Kind,
        string? BankAr, string? BankEn, string? AccountNo, string? Iban, bool IsDefault)
    {
        public string Name(bool arabic) => arabic ? NameAr : NameEn;

        public string? Bank(bool arabic) => arabic ? BankAr : BankEn;
    }

    /// <summary>Enum display for the collection-account catalogue — never <c>ToString()</c> on screen.</summary>
    public static class CollectionAccountLabels
    {
        public static string Kind(CollectionAccountKind kind, bool arabic) => kind switch
        {
            CollectionAccountKind.Bank => arabic ? "حساب بنكي" : "Bank account",
            CollectionAccountKind.CashBox => arabic ? "صندوق نقدي" : "Cash box",
            _ => kind.ToString(),
        };

        /// <summary>
        /// The kind as a definite noun phrase, for a sentence that goes on to
        /// qualify it — "الحساب البنكي <b>الذي</b> وصل إليه المبلغ". Arabic will
        /// not take a definite relative pronoun after an indefinite noun, so
        /// <see cref="Kind"/>'s bare "حساب بنكي" cannot be dropped into that
        /// frame; both forms are needed and neither substitutes for the other.
        /// </summary>
        public static string KindDefinite(CollectionAccountKind kind, bool arabic) => kind switch
        {
            CollectionAccountKind.Bank => arabic ? "الحساب البنكي" : "the bank account",
            CollectionAccountKind.CashBox => arabic ? "الصندوق النقدي" : "the cash box",
            _ => Kind(kind, arabic),
        };

        /// <summary>What the picker's label says for a given method — "transfer to", "received into".</summary>
        public static string Destination(PaymentMethod method, bool arabic) => method switch
        {
            PaymentMethod.Cash => arabic ? "الصندوق المستلم" : "Cash box receiving it",
            PaymentMethod.BankTransfer => arabic ? "الحساب المحوَّل إليه" : "Account transferred to",
            _ => arabic ? "الحساب المودع فيه" : "Account it is banked into",
        };
    }
}
