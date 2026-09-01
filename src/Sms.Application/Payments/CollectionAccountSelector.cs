using System.Collections.Generic;
using System.Linq;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Payments;

namespace Sms.Application.Payments
{
    /// <summary>
    /// Which of the school's own accounts a payment may be collected into, and
    /// whether the one named is allowed (doc/Modules/21 §3 BR-PAY-002, §9).
    /// <para>
    /// A pure decision over a list — no database, no clock — so the cashier
    /// screen, the API and <c>PaymentAdmin</c> all filter and refuse by the
    /// same rule rather than each writing its own <c>if</c>.
    /// </para>
    /// </summary>
    public static class CollectionAccountSelector
    {
        /// <summary>
        /// Which pot a method's money ends up in.
        /// <para>
        /// Only cash stays in the building. A transfer arrives at a bank
        /// account by definition; a card settlement and a cheque both land in
        /// one a day or two later, and the school reconciles them against that
        /// account's statement — so all three name a bank account, and the
        /// difference between them stays in <see cref="Receipt.Method"/> where
        /// it belongs.
        /// </para>
        /// </summary>
        public static CollectionAccountKind KindFor(PaymentMethod method)
            => method == PaymentMethod.Cash ? CollectionAccountKind.CashBox : CollectionAccountKind.Bank;

        /// <summary>
        /// The accounts a cashier may choose from for this method, most-used
        /// first. Deactivated accounts are excluded: they are kept so old
        /// receipts still read back, not so new money can be put in them.
        /// </summary>
        public static IReadOnlyList<CollectionAccount> Eligible(IEnumerable<CollectionAccount> accounts, PaymentMethod method)
        {
            var kind = KindFor(method);
            return accounts
                .Where(a => a.IsActive && a.Kind == kind)
                .OrderByDescending(a => a.IsDefault)
                .ThenBy(a => a.DisplayOrder)
                .ThenBy(a => a.Code)
                .ToList();
        }

        /// <summary>The account to pre-select for this method, or null when the school has defined none of that kind.</summary>
        public static CollectionAccount? Preselected(IEnumerable<CollectionAccount> accounts, PaymentMethod method)
            => Eligible(accounts, method).FirstOrDefault();

        /// <summary>
        /// Refuses the three ways a capture can name the wrong destination.
        /// <para>
        /// <paramref name="anyEligible"/> is what makes the field conditional
        /// rather than mandatory: a school on its first morning has no accounts
        /// defined and must still be able to receipt a payment, but once it has
        /// defined one, a blank destination is an omission and is refused. Pass
        /// the result of <see cref="Eligible"/> being non-empty.
        /// </para>
        /// </summary>
        public static void Validate(PaymentMethod method, CollectionAccount? chosen, bool anyEligible)
        {
            var kind = KindFor(method);

            if (chosen == null)
            {
                if (anyEligible)
                {
                    throw new CollectionAccountRequiredException(method, kind);
                }

                return;
            }

            if (chosen.Kind != kind)
            {
                throw new CollectionAccountMethodMismatchException(method, kind, chosen.Kind);
            }

            if (!chosen.IsActive)
            {
                throw new CollectionAccountInactiveException(chosen.Code);
            }
        }
    }
}
