using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Common.Interfaces;
using Sms.Application.Jobs;
using Sms.Application.ReadModels;

namespace Sms.Infrastructure.ReadModels
{
    /// <summary>DB/04 §4: "Snapshots refresh via ops.JobDefinition schedules" — daily aged receivables (RPT-FEE-004, D refresh class).</summary>
    public class AgedReceivablesSnapshotJobHandler : IJobHandler
    {
        private readonly ISnapshotRefreshService _snapshots;

        public AgedReceivablesSnapshotJobHandler(ISnapshotRefreshService snapshots) => _snapshots = snapshots;

        public string JobCode => SnapshotJobCodes.AgedReceivables;

        public Task RunAsync(CancellationToken cancellationToken = default) => _snapshots.RefreshAgedReceivablesAsync(cancellationToken: cancellationToken);
    }

    /// <summary>Today's attendance summary per section (C15 refresh class for Principal/VP widgets — schedule every 15 minutes during the school day).</summary>
    public class DailyAttendanceSummarySnapshotJobHandler : IJobHandler
    {
        private readonly ISnapshotRefreshService _snapshots;
        private readonly IClock _clock;

        public DailyAttendanceSummarySnapshotJobHandler(ISnapshotRefreshService snapshots, IClock clock)
        {
            _snapshots = snapshots;
            _clock = clock;
        }

        public string JobCode => SnapshotJobCodes.DailyAttendanceSummary;

        public Task RunAsync(CancellationToken cancellationToken = default) => _snapshots.RefreshDailyAttendanceSummaryAsync(_clock.UtcNow.Date, cancellationToken);
    }

    /// <summary>Collection calendar / cashflow forecast (RPT-INS-001), daily.</summary>
    public class CollectionCalendarSnapshotJobHandler : IJobHandler
    {
        private readonly ISnapshotRefreshService _snapshots;

        public CollectionCalendarSnapshotJobHandler(ISnapshotRefreshService snapshots) => _snapshots = snapshots;

        public string JobCode => SnapshotJobCodes.CollectionCalendar;

        public Task RunAsync(CancellationToken cancellationToken = default) => _snapshots.RefreshCollectionCalendarAsync(cancellationToken: cancellationToken);
    }
}
