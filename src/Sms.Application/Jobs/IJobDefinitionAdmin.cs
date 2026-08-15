using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Jobs;

namespace Sms.Application.Jobs
{
    /// <summary>Job registry upsert — the admin surface (WBS E-011) is Hangfire's own dashboard, not a custom screen.</summary>
    public interface IJobDefinitionAdmin
    {
        Task<JobDefinition> DefineJobAsync(
            string code, string nameAr, string nameEn, string cronExpression, bool isEnabled, CancellationToken cancellationToken = default);
    }
}
