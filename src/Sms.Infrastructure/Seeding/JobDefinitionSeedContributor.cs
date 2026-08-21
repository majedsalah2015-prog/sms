using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Jobs;
using Sms.Application.Seeding;
using Sms.Infrastructure.Jobs;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Writes an <c>ops.JobDefinition</c> row for every entry in
    /// <see cref="JobCatalog"/>, through the same registrar the host runs at
    /// boot.
    /// <para>
    /// Nothing called <c>DefineJobAsync</c> anywhere in the repository, so the
    /// table was empty and all five recurring jobs failed on every fire:
    /// <c>JobRunner</c> looks the code up first and throws
    /// <c>UnknownJobException</c> when it finds nothing. The scheduler was
    /// working perfectly and every handler was registered — and the audit
    /// checkpoint had never run, no notification had ever been delivered, and
    /// three dashboards were reading snapshots nothing refreshed.
    /// </para>
    /// </summary>
    public class JobDefinitionSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;

        public JobDefinitionSeedContributor(AppDbContext db)
        {
            _db = db;
        }

        public string Name => "Recurring job definitions (doc 02 T-6)";

        // Early: system-level, and depends on nothing else that is seeded.
        public int Order => 15;

        public Task SeedAsync(CancellationToken cancellationToken = default)
            => JobDefinitionRegistrar.EnsureAsync(_db, cancellationToken);
    }
}
