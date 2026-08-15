using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Jobs;
using Sms.Domain.Common;
using Sms.Domain.Jobs;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Jobs
{
    /// <summary>Standalone admin operation — saves itself, no larger transaction to ride.</summary>
    public class JobDefinitionAdmin : IJobDefinitionAdmin
    {
        private readonly AppDbContext _db;

        public JobDefinitionAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<JobDefinition> DefineJobAsync(
            string code, string nameAr, string nameEn, string cronExpression, bool isEnabled, CancellationToken cancellationToken = default)
        {
            var job = await _db.JobDefinitions.SingleOrDefaultAsync(j => j.Code == code, cancellationToken);
            if (job == null)
            {
                job = new JobDefinition { Code = code };
                _db.JobDefinitions.Add(job);
            }

            job.Name = new LocalizedName(nameAr, nameEn);
            job.CronExpression = cronExpression;
            job.IsEnabled = isEnabled;

            await _db.SaveChangesAsync(cancellationToken);
            return job;
        }
    }
}
