using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Lookups
{
    /// <summary>
    /// One place a lookup value is spoken for: the referencing entity, the column that
    /// holds the reference, and how many rows currently do (doc/Modules/01 §8 "value
    /// grid … usage counter before deactivate").
    /// <para>
    /// Both names are carried, not just a number, because the confirmation the operator
    /// has to read is "12 students, 3 employees" — a bare 15 tells them nothing about
    /// what they are about to break, and an entity alone is ambiguous where one table
    /// points at lookups from several columns (a Student names a nationality, an ID
    /// type and the mother's education level).
    /// </para>
    /// <para>
    /// <see cref="EntityName"/> and <see cref="PropertyName"/> are CLR names — "Student",
    /// "NationalityLookupId" — deliberately not display text: the Web boundary owns the
    /// Arabic and English wording, as it does for every other user-visible string.
    /// </para>
    /// </summary>
    public sealed record LookupUsage(string EntityName, string PropertyName, int Count);

    /// <summary>
    /// doc/Modules/01 §9: "deactivation of a lookup shows usage count and requires
    /// confirmation". BR-SET-002: a lookup value referenced anywhere is deactivatable,
    /// never deletable (BR-GLB-005/006) — so the count is not a permission check that
    /// can refuse the operation. Nothing here blocks a deactivation; the number exists
    /// to tell the operator how much of the school's data is about to keep pointing at
    /// a value that will no longer be offered in any picker.
    /// </summary>
    public interface ILookupUsageQuery
    {
        /// <summary>
        /// Every entity + column with at least one row referencing
        /// <paramref name="lookupValueId"/>, ordered by count descending so a screen can
        /// show the worst offenders first. Empty means nothing points at the value, which
        /// is the only case where deactivating it costs the school nothing.
        /// <para>
        /// Deactivated referencing rows are counted too: a withdrawn student still records
        /// the nationality that was true of them, and hiding that from the operator would
        /// understate what the value still means to the archive.
        /// </para>
        /// </summary>
        Task<IReadOnlyList<LookupUsage>> CountUsagesAsync(int lookupValueId, CancellationToken cancellationToken = default);

        /// <summary>
        /// The same answer for a whole category at once, keyed by value id. A screen
        /// that shows the count beside every row must use this: asking per value costs
        /// one round-trip per referencing column <em>per row</em>, which on a real
        /// nationality list (~195 values × 11 columns) is over two thousand serial
        /// queries before the page renders. This is a fixed number of queries — one
        /// per referencing column — however many values are on screen.
        /// <para>
        /// A value nothing references is absent from the dictionary rather than
        /// present with an empty list, so a caller reads "not there" as "free to
        /// retire" without a second check.
        /// </para>
        /// </summary>
        Task<IReadOnlyDictionary<int, IReadOnlyList<LookupUsage>>> CountUsagesAsync(
            IReadOnlyCollection<int> lookupValueIds, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// The one derived number the confirmation prompt needs, as a pure static helper
    /// rather than a field on the result — the breakdown is the answer, the total is a
    /// view of it, and computing it twice from the same list can never disagree.
    /// </summary>
    public static class LookupUsageSummary
    {
        /// <summary>Rows referencing the value across every entity — the "usage count" of doc/Modules/01 §9.</summary>
        public static int TotalCount(this IReadOnlyList<LookupUsage> usages) => usages.Sum(u => u.Count);

        /// <summary>True when deactivating the value leaves live rows pointing at it (BR-SET-002's whole reason for existing).</summary>
        public static bool IsReferenced(this IReadOnlyList<LookupUsage> usages) => usages.Count > 0;
    }
}
