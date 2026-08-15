using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Numbering
{
    /// <summary>
    /// BR-NUM-001: the only path to a generated number — nothing else in the
    /// product may mint one. Mutates the ambient SeriesState/NumberingSeries
    /// through the injected DbContext and never calls SaveChanges itself;
    /// the caller's own save commits the number atomically with its business
    /// row (BR-NUM-003: materializes only on commit, a failed posting never
    /// consumes one), the same composition rule as
    /// <see cref="Workflow.IWorkflowFinalEffect"/>.
    /// </summary>
    public interface INumberIssuer
    {
        /// <summary>Throws <see cref="Sms.Application.Common.Exceptions.NoActiveNumberingSeriesException"/> when the code has no active series.</summary>
        Task<string> IssueAsync(string seriesCode, CancellationToken cancellationToken = default);
    }
}
