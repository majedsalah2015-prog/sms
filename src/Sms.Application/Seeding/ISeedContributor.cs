using System.Threading;
using System.Threading.Tasks;

namespace Sms.Application.Seeding
{
    /// <summary>
    /// One module's slice of the demo-tenant seed (doc 02 §9, IP-02 §2 — "the
    /// same fixture sales/QA/perf tests use"). Registered as a fan-out
    /// collection (same shape as IWorkflowFinalEffect/IChannelSender) and run
    /// in <see cref="Order"/> so dependencies (e.g. numbering series before
    /// anything that issues a number) seed first. Implementations must be
    /// idempotent — re-running the seeder on an already-seeded tenant is a
    /// no-op, never a duplicate.
    /// </summary>
    public interface ISeedContributor
    {
        string Name { get; }

        int Order { get; }

        Task SeedAsync(CancellationToken cancellationToken = default);
    }
}
