using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Dashboards
{
    /// <summary>
    /// The statistics screen's one read: every figure on it, for one academic
    /// year, in a single call (doc/Modules/31 §8.1, BR-DSH-005).
    /// <para>
    /// One call rather than five because the sections are read together and must
    /// agree with each other — collected as a percentage of billed is nonsense if
    /// the two halves were counted a page-refresh apart. Read-only throughout:
    /// this never calls <c>SaveChangesAsync</c>, the same discipline
    /// <see cref="IDashboardQuery"/> keeps.
    /// </para>
    /// <para>
    /// Where a module already owns the arithmetic, this reuses it rather than
    /// re-deriving it — receivables come from the fee module's own position
    /// calculator (BR-FEE-008) — so a number here and the same number on the
    /// screen it drills into cannot disagree (BR-DSH-002).
    /// </para>
    /// </summary>
    public interface IStatisticsQuery
    {
        /// <summary>
        /// Every section for <paramref name="academicYearId"/>.
        /// <para>
        /// Expenses come back null when no ledger is attached — see
        /// <see cref="ExpenseStatistics"/>. Every other section is always present,
        /// zeroed where the school has no data yet.
        /// </para>
        /// </summary>
        Task<SchoolStatistics> GetAsync(int academicYearId, CancellationToken cancellationToken = default);
    }
}
