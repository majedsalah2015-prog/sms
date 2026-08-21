using System.Collections.Generic;
using Sms.Application.ReadModels;

namespace Sms.Application.Jobs
{
    /// <summary>
    /// The recurring jobs this system runs, with their schedules (doc 02 T-6).
    /// <para>
    /// One table, two readers: the seeder writes an <c>ops.JobDefinition</c> row
    /// per entry, and the host registers the same entries with Hangfire. The
    /// schedules used to be written out twice — once here in prose and once as
    /// cron literals in <c>Startup</c> — and two sources for one schedule is a
    /// drift waiting to happen; the row would say daily and the scheduler would
    /// fire hourly, with nothing to notice the disagreement.
    /// </para>
    /// <para>
    /// A definition row is not paperwork. <c>JobRunner</c> resolves the job by
    /// code and throws <c>UnknownJobException</c> when there is no row, so
    /// without these the scheduler fires on time, every time, and every fire
    /// fails: no audit checkpoint, no notification ever delivered, and three
    /// dashboards reading snapshots nothing refreshes.
    /// </para>
    /// </summary>
    public static class JobCatalog
    {
        public sealed record JobDefinitionSpec(string Code, string NameAr, string NameEn, string CronExpression, string Rationale);

        private static readonly JobDefinitionSpec[] All =
        {
            new("AuditIntegrityCheckpoint", "نقطة تحقق سلامة التدقيق", "Audit integrity checkpoint", "0 2 * * *",
                "Daily 02:00 UTC — matches IntegrityCheckpointService's one-day default period, so consecutive checkpoints meet without a gap."),

            new("NotificationDispatch", "إرسال الإشعارات", "Notification dispatch", "*/5 * * * *",
                "Every five minutes. This is the only thing that delivers a notification at all, in-app included."),

            new(SnapshotJobCodes.AgedReceivables, "لقطة أعمار الذمم", "Aged receivables snapshot", "30 2 * * *",
                "Daily 02:30 UTC — the D refresh class of DB/04 §4 (RPT-FEE-004, the finance donut)."),

            new(SnapshotJobCodes.DailyAttendanceSummary, "لقطة ملخص الحضور اليومي", "Daily attendance summary snapshot", "*/15 4-12 * * *",
                "Every fifteen minutes across the school day in UTC — the C15 refresh class. Outside those hours nothing changes to summarise."),

            new(SnapshotJobCodes.CollectionCalendar, "لقطة تقويم التحصيل", "Collection calendar snapshot", "45 2 * * *",
                "Daily 02:45 UTC — RPT-INS-001's cashflow forecast, after the receivables snapshot it reads from."),
        };

        public static IReadOnlyList<JobDefinitionSpec> Jobs => All;
    }
}
