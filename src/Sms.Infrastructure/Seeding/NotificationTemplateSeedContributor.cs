using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Notifications;
using Sms.Application.Seeding;
using Sms.Domain.Notifications;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Writes the wording behind every notification a module actually raises (doc 09 §5).
    /// <para>
    /// <b>Why this exists.</b> <see cref="NotificationDefaultsSeedContributor"/> made the
    /// subscription matrix real — 44 rules, enabled, in-app. It changed nothing about what a
    /// parent receives, because <see cref="Notifications.NotificationPublisher"/> skips any
    /// event with no <c>msg.Template</c> behind it, silently and by design (BR-NOT-003). With
    /// the rules seeded and no templates, every publish in the product resolved to a rule,
    /// found no content, and queued nothing. The engine, the rules, the dispatcher and the
    /// notification centre were all in place and the system still told nobody anything.
    /// </para>
    /// <para>
    /// <b>What is seeded, and what is not.</b> One in-app template per event that a module
    /// actually publishes — the thirteen <c>NotificationEvent.HasPublisher</c> events. An
    /// event nothing raises gets no template: it would be content that can never render, and
    /// the template studio is where a school writes ahead of a module if it wants to. Channel
    /// is in-app only, matching the rules the defaults contributor enables; seeding an SMS
    /// template with no SMS rule behind it produces the same inert row from the other side.
    /// </para>
    /// <para>
    /// <b>These are starter wordings, not final ones.</b> Deliberately plain, deliberately
    /// short, and deliberately free of anything a school might consider its own voice — the
    /// studio exists so they can rewrite every one of them, and a new version there supersedes
    /// what is written here without this contributor ever fighting it back (see the idempotency
    /// note below).
    /// </para>
    /// <para>
    /// <b>Published on write, and why that does not bypass BR-NTF-001.</b> The test-send gate
    /// exists so a human cannot put untested wording live — it protects against a typo in a
    /// placeholder and against a channel that rejects the message. Neither risk applies here:
    /// these placeholders are taken from <see cref="TemplatePlaceholderRules"/>, which is
    /// derived from the publishers' own payloads, and in-app has no gateway to reject
    /// anything. A seeded template left as a draft would instead ship a product whose
    /// notifications are all one manual step away from working, which is the state this
    /// contributor exists to end.
    /// </para>
    /// <para>
    /// <b>Idempotency.</b> Matched on (event, channel) past the soft-active filter, because
    /// <see cref="Template"/> is <c>ISoftActiveFiltered</c> and a template a school has retired
    /// is invisible to a plain query — reading it as missing would re-create it, undo the
    /// school's decision, and die on the unique index over (SchoolId, EventCode, Channel).
    /// An existing template is left entirely alone: a school that has rewritten the absence
    /// notice does not get the product's words back on the next deployment.
    /// </para>
    /// </summary>
    public class NotificationTemplateSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly INotificationConfigAdmin _config;
        private readonly INotificationOpsAdmin _ops;

        public NotificationTemplateSeedContributor(
            AppDbContext db, INotificationConfigAdmin config, INotificationOpsAdmin ops)
        {
            _db = db;
            _config = config;
            _ops = ops;
        }

        public string Name => "Notification templates for the events modules actually raise (doc 09 §5)";

        /// <summary>
        /// Immediately after the subscription defaults (56): a template without its rule is
        /// never consulted, and the run log should read in that order. Both sit after the
        /// demo tenant because both are school-scoped and guard on the school existing — see
        /// the note on <c>NotificationDefaultsSeedContributor.Order</c> for what happens when
        /// they do not.
        /// </summary>
        public int Order => 57;

        /// <summary>
        /// The wording, per event. Placeholder names are exactly the keys the publishing module
        /// puts in its payload — <see cref="TemplatePlaceholderRules"/> is the same table read
        /// from the same source, and the test in <c>Sms.Application.Tests</c> holds these to it.
        /// A mismatch here would reach a parent as the literal token.
        /// </summary>
        private static readonly (string Code, string SubjectAr, string SubjectEn, string BodyAr, string BodyEn)[] Content =
        {
            ("LibraryOverdue",
                "تأخر إعادة كتاب", "Library loan overdue",
                "تأخرت إعادة الكتاب ({Barcode}) عن موعدها بـ {DaysOverdue} يوماً. نرجو إعادته إلى المكتبة.",
                "The book ({Barcode}) is {DaysOverdue} day(s) past its return date. Please return it to the library."),

            ("LibraryReservationReady",
                "الحجز جاهز للاستلام", "Reservation ready",
                "الكتاب المحجوز ({Barcode}) جاهز للاستلام من المكتبة حتى {HoldUntil}.",
                "The reserved book ({Barcode}) is ready to collect from the library until {HoldUntil}."),

            ("InstallmentDueSoon",
                "قرب استحقاق قسط", "Installment due soon",
                "يستحق القسط رقم {InstallmentNo} بمبلغ {Amount} في {DueDate}. تجدون تفاصيل الحساب في البوابة.",
                "Installment {InstallmentNo} of {Amount} is due on {DueDate}. The full statement is on the portal."),

            ("InstallmentOverdue",
                "تأخر سداد قسط", "Installment overdue",
                "لم يُسدَّد القسط رقم {InstallmentNo} بمبلغ {Amount} المستحق في {DueDate}. نرجو مراجعة الإدارة المالية.",
                "Installment {InstallmentNo} of {Amount}, due on {DueDate}, is unpaid. Please contact the finance office."),

            ("ClinicStudentSentHome",
                "إرسال الطالب إلى المنزل من العيادة", "Sent home from the clinic",
                "زار ابنكم العيادة المدرسية (زيارة رقم {VisitNo}) وتقرر إرساله إلى المنزل. نرجو التواصل مع المدرسة.",
                "Your child attended the school clinic (visit {VisitNo}) and is being sent home. Please contact the school."),

            ("SchoolEmergencyProtocol",
                "بروتوكول الطوارئ الصحية", "Clinic emergency",
                "جرى تفعيل بروتوكول الطوارئ الصحية لابنكم (زيارة رقم {VisitNo}). سيتصل بكم مسؤول العيادة فوراً.",
                "The health emergency protocol has been activated for your child (visit {VisitNo}). The clinic will call you immediately."),

            ("MedicationAdministered",
                "إعطاء دواء", "Medication administered",
                "أُعطي ابنكم {Medication} في {At} — الحالة: {Status}.",
                "Your child was given {Medication} at {At} — status: {Status}."),

            ("HealthExposureNotice",
                "إشعار مخالطة", "Exposure notice",
                "سُجّلت حالة {Disease} في محيط ابنكم بين {From} و{To}. نرجو متابعة الأعراض ومراجعة الطبيب عند اللزوم.",
                "A case of {Disease} was recorded around your child between {From} and {To}. Please watch for symptoms and see a doctor if needed."),

            ("DisciplineIncidentRecorded",
                "تسجيل مخالفة سلوكية", "Behaviour incident recorded",
                "سُجّلت مخالفة سلوكية لابنكم (رقم {IncidentNo}، الدرجة {Severity}). تفاصيلها في البوابة.",
                "A behaviour incident was recorded for your child (number {IncidentNo}, severity {Severity}). The details are on the portal."),

            ("DisciplineDecision",
                "قرار في قضية سلوك", "Behaviour decision",
                "صدر قرار في قضية ابنكم استناداً إلى المادة {Article}: {Consequence}.",
                "A decision was issued in your child's case under article {Article}: {Consequence}."),

            ("TransportRouteChanged",
                "تغيير مسار النقل", "Transport route changed",
                "تغيّر مسار النقل المدرسي لابنكم. تجدون المسار والمحطة الجديدين في البوابة.",
                "Your child's school transport route has changed. The new route and stop are on the portal."),

            ("TransportSuspended",
                "إيقاف اشتراك النقل", "Transport suspended",
                "أُوقف اشتراك النقل المدرسي لابنكم اعتباراً من {EffectiveDate}. نرجو مراجعة الإدارة.",
                "Your child's school transport subscription is suspended from {EffectiveDate}. Please contact the school office."),

            ("TransportStudentNotBoarded",
                "الطالب لم يصعد الحافلة", "Child did not board",
                "لم يصعد ابنكم حافلة المدرسة اليوم {Date}. نرجو التواصل مع المدرسة فوراً.",
                "Your child did not board the school bus on {Date}. Please contact the school immediately."),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            const NotificationChannel channel = NotificationChannel.InApp;

            var have = (await _db.Templates.IgnoreQueryFilters().AsNoTracking()
                    .Where(t => t.SchoolId == _db.CurrentSchoolId && t.Channel == channel)
                    .Select(t => t.EventCode)
                    .ToListAsync(cancellationToken))
                .Select(c => c.ToUpperInvariant())
                .ToHashSet(StringComparer.Ordinal);

            foreach (var row in Content)
            {
                if (have.Contains(row.Code.ToUpperInvariant()))
                {
                    continue;
                }

                // A code the catalogue does not define would be a template no rule can ever
                // match — a silent no-op rather than a visible mistake, so refuse it here.
                if (!NotificationEventCatalog.TryGet(row.Code, out _))
                {
                    throw new InvalidOperationException(
                        $"Template seed names '{row.Code}', which NotificationEventCatalog does not define.");
                }

                var version = await _config.DefineTemplateAsync(
                    row.Code, channel, row.SubjectAr, row.SubjectEn, row.BodyAr, row.BodyEn, cancellationToken);

                await _ops.MarkTemplateVersionTestSentAsync(version.Id, cancellationToken);
                await _ops.PublishTemplateVersionAsync(version.Id, cancellationToken);

                // Each of those three calls saves. Without this the tracker carries every
                // template and version across thirteen commits and DetectChanges re-walks the
                // lot each time — the same quadratic the rollover paid for once already.
                _db.ChangeTracker.Clear();
            }
        }
    }
}
