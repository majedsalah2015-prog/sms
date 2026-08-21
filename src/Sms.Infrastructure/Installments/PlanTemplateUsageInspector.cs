using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Guards;
using Sms.Domain.Installments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Installments
{
    /// <summary>
    /// What stands in the way of removing a plan template.
    /// <para>
    /// Only assignments do. A template's splits belong to it and go with it; the
    /// schedules an assignment produced are copies of the shape, not references
    /// to it — which is exactly why an assigned template must survive: deleting
    /// it would leave a live schedule whose origin cannot be named, on a screen
    /// that shows every family which plan they are on.
    /// </para>
    /// </summary>
    public class PlanTemplateUsageInspector : IUsageInspector<PlanTemplate>
    {
        private readonly AppDbContext _db;

        public PlanTemplateUsageInspector(AppDbContext db)
        {
            _db = db;
        }

        public async Task<UsageReport> InspectAsync(int id, CancellationToken cancellationToken = default)
        {
            var assignments = await _db.PlanAssignments.CountAsync(a => a.PlanTemplateId == id, cancellationToken);
            return UsageReport.From(new UsageReference("assigned plan(s)", "خطة مُسنَدة", assignments));
        }
    }
}
