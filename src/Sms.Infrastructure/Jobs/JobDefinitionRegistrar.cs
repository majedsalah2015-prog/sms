using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Jobs;
using Sms.Domain.Common;
using Sms.Domain.Jobs;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Jobs
{
    /// <summary>
    /// Brings <c>ops.JobDefinition</c> in line with <see cref="JobCatalog"/>.
    /// <para>
    /// Called from two places for one reason each: the seeder, so a provisioned
    /// database has the rows, and the host at boot, so a database that was
    /// migrated without seeding does too. The second matters more than it looks
    /// — a missing row does not degrade a job, it fails it, and the failure is
    /// silent until someone asks why no notification has ever arrived.
    /// </para>
    /// <para>
    /// <c>IsEnabled</c> is written only when the row is created. Disabling a job
    /// is the one field on this record an operator is meant to change, and a boot
    /// that re-enabled it would make that switch useless.
    /// </para>
    /// </summary>
    public static class JobDefinitionRegistrar
    {
        public static async Task EnsureAsync(AppDbContext db, CancellationToken cancellationToken = default)
        {
            var existing = await db.JobDefinitions.ToDictionaryAsync(j => j.Code, cancellationToken);
            var changed = false;

            foreach (var spec in JobCatalog.Jobs)
            {
                if (existing.TryGetValue(spec.Code, out var job))
                {
                    if (job.CronExpression == spec.CronExpression
                        && job.Name.NameAr == spec.NameAr
                        && job.Name.NameEn == spec.NameEn)
                    {
                        continue;
                    }

                    // The catalogue is also what Hangfire's schedule is built from, so a row that
                    // disagrees with it is a row that lies about when the job runs.
                    job.Name = new LocalizedName(spec.NameAr, spec.NameEn);
                    job.CronExpression = spec.CronExpression;
                    changed = true;
                    continue;
                }

                db.JobDefinitions.Add(new JobDefinition
                {
                    Code = spec.Code,
                    Name = new LocalizedName(spec.NameAr, spec.NameEn),
                    CronExpression = spec.CronExpression,
                    IsEnabled = true,
                });
                changed = true;
            }

            if (changed)
            {
                await db.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
