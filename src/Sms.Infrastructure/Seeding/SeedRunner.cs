using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Seeding;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>Runs every registered ISeedContributor in Order — the demo-tenant seeder harness (doc 02 §9, IP-02 §2).</summary>
    public class SeedRunner
    {
        private readonly IEnumerable<ISeedContributor> _contributors;

        public SeedRunner(IEnumerable<ISeedContributor> contributors)
        {
            _contributors = contributors;
        }

        public async Task<IReadOnlyList<string>> RunAllAsync(CancellationToken cancellationToken = default)
        {
            var ran = new List<string>();
            foreach (var contributor in _contributors.OrderBy(c => c.Order))
            {
                await contributor.SeedAsync(cancellationToken);
                ran.Add(contributor.Name);
            }

            return ran;
        }
    }
}
