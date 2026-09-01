using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sms.Application.Payments
{
    /// <summary>
    /// doc/Modules/21 §8.2, BR-PAY-001: a till session is cashier × till × day, and the
    /// doc never says who names the till — so the console assigns one rather than asking a
    /// cashier with a queue in front of them to invent a code.
    /// <para>
    /// The till is a <b>physical drawer</b>, not a session: BR-PAY-001's variance record and
    /// the module's daily-collection-by-till and cashier-variance-history reports are only
    /// worth reading while the same cashier comes back to the same code. So the rule is
    /// <i>keep the one you had</i>, and mint a new <c>TILL-n</c> only for a cashier who has
    /// never opened a session — a new code per session would give every report a column of one.
    /// </para>
    /// <para>
    /// Codes typed by hand before this existed ("T1", "Counter A") are kept as they are: a
    /// cashier's history stays on one code, and <see cref="Next"/> only has to avoid colliding
    /// with them, not replace them.
    /// </para>
    /// </summary>
    public static class TillCodeGenerator
    {
        /// <summary>Minted codes read <c>TILL-1</c>, <c>TILL-2</c>… — ASCII in both languages, and inside the column's 20.</summary>
        public const string Prefix = "TILL-";

        /// <summary>
        /// The code to open this cashier's next session on.
        /// </summary>
        /// <param name="cashiersLastCode">The code of this cashier's most recent session, open or closed; null for a cashier who has never opened one.</param>
        /// <param name="openCodes">Codes with a session open right now — a drawer in someone else's hands cannot be handed out again.</param>
        /// <param name="everUsedCodes">Every code the school has recorded, closed sessions included, so a minted code never reuses a retired one's history.</param>
        public static string Resolve(string? cashiersLastCode, IEnumerable<string?> openCodes, IEnumerable<string?> everUsedCodes)
        {
            var open = ToSet(openCodes);
            var mine = cashiersLastCode?.Trim();

            // Their own drawer, unless someone is standing at it — which only happens after a
            // hand-typed code was shared, or a handover. Then they get a fresh one rather than
            // a refusal they cannot act on.
            if (!string.IsNullOrEmpty(mine) && !open.Contains(mine))
            {
                return mine;
            }

            return Next(everUsedCodes);
        }

        /// <summary>The lowest <c>TILL-n</c> that <paramref name="takenCodes"/> does not already hold (compared case-insensitively).</summary>
        public static string Next(IEnumerable<string?> takenCodes)
        {
            var taken = ToSet(takenCodes);

            // Bounded by taken.Count + 1: the candidates are distinct, so at most taken.Count
            // of them can be blocked and one is always left over.
            for (var number = 1; number <= taken.Count + 1; number++)
            {
                var candidate = Prefix + number.ToString(CultureInfo.InvariantCulture);
                if (!taken.Contains(candidate))
                {
                    return candidate;
                }
            }

            throw new InvalidOperationException("Unreachable: more blocked candidates than taken codes.");
        }

        private static HashSet<string> ToSet(IEnumerable<string?> codes)
            => new(
                (codes ?? Enumerable.Empty<string?>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c!.Trim()),
                StringComparer.OrdinalIgnoreCase);
    }
}
