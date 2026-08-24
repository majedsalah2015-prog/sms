using System;
using System.Collections.Generic;
using Sms.Application.Discipline;
using Sms.Domain.Discipline;
using Sms.Domain.Sections;
using Sms.Domain.Students;

namespace Sms.Web.Models
{
    /// <summary>
    /// doc/Modules/25 §8 view models. Behaviour data is restricted (BR-DCP-008)
    /// and the reporter is protected (BR-DCP-010), so these projections carry
    /// only what a staff screen shows: nothing here is reachable from the portal,
    /// which has its own projection through
    /// <see cref="PortalVisibilityPolicy.ParentIncidentView"/>.
    /// </summary>
    public static class DisciplineLabels
    {
        public static string CaseStatus(CaseStatus s, bool ar) => !ar ? SpaceOut(s.ToString()) : s switch
        {
            Sms.Domain.Discipline.CaseStatus.Reported => "مُبلَّغ عنها",
            Sms.Domain.Discipline.CaseStatus.UnderInvestigation => "قيد التحقيق",
            Sms.Domain.Discipline.CaseStatus.Decided => "صدر القرار",
            Sms.Domain.Discipline.CaseStatus.ActionApplied => "نُفِّذ الإجراء",
            Sms.Domain.Discipline.CaseStatus.AppealWindow => "مهلة التظلّم",
            Sms.Domain.Discipline.CaseStatus.Closed => "مغلقة",
            _ => s.ToString(),
        };

        public static string ConsequenceKind(ConsequenceKind k, bool ar) => !ar ? SpaceOut(k.ToString()) : k switch
        {
            Sms.Domain.Discipline.ConsequenceKind.VerbalWarning => "تنبيه شفهي",
            Sms.Domain.Discipline.ConsequenceKind.WrittenWarning => "إنذار كتابي",
            Sms.Domain.Discipline.ConsequenceKind.ParentSummons => "استدعاء ولي الأمر",
            Sms.Domain.Discipline.ConsequenceKind.Detention => "احتجاز",
            Sms.Domain.Discipline.ConsequenceKind.CommunityService => "خدمة مجتمعية",
            Sms.Domain.Discipline.ConsequenceKind.ActivityBan => "حرمان من الأنشطة",
            Sms.Domain.Discipline.ConsequenceKind.InSchoolSuspension => "فصل داخلي",
            Sms.Domain.Discipline.ConsequenceKind.ExternalSuspension => "فصل خارجي",
            Sms.Domain.Discipline.ConsequenceKind.BehaviorContract => "تعهّد سلوكي",
            _ => k.ToString(),
        };

        public static string StatementKind(StatementKind k, bool ar) => !ar ? k.ToString() : k switch
        {
            Sms.Domain.Discipline.StatementKind.Student => "إفادة الطالب",
            Sms.Domain.Discipline.StatementKind.Parent => "إفادة ولي الأمر",
            Sms.Domain.Discipline.StatementKind.Witness => "إفادة شاهد",
            Sms.Domain.Discipline.StatementKind.Staff => "إفادة موظف",
            _ => k.ToString(),
        };

        public static string AppealOutcome(AppealOutcome o, bool ar) => !ar ? o.ToString() : o switch
        {
            Sms.Domain.Discipline.AppealOutcome.Pending => "قيد النظر",
            Sms.Domain.Discipline.AppealOutcome.Upheld => "تأييد القرار",
            Sms.Domain.Discipline.AppealOutcome.Modified => "تعديل القرار",
            Sms.Domain.Discipline.AppealOutcome.Dismissed => "رفض التظلّم",
            _ => o.ToString(),
        };

        /// <summary>BR-DCP-001's 1–4 scale in words, so a number nobody has memorised reads as what it means.</summary>
        public static string Severity(int severity, bool ar) => severity switch
        {
            1 => ar ? "بسيطة" : "Minor",
            2 => ar ? "متوسطة" : "Moderate",
            3 => ar ? "جسيمة" : "Serious",
            4 => ar ? "بالغة" : "Gravest",
            _ => severity.ToString(System.Globalization.CultureInfo.InvariantCulture),
        };

        /// <summary>Bootstrap contextual class per severity — the same colour everywhere the level appears.</summary>
        public static string SeverityClass(int severity) => severity switch
        {
            1 => "text-bg-secondary",
            2 => "text-bg-warning",
            3 => "text-bg-danger",
            4 => "text-bg-dark",
            _ => "text-bg-light",
        };

        private static string SpaceOut(string pascal)
        {
            var chars = new List<char>(pascal.Length + 4);
            for (var i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i]))
                {
                    chars.Add(' ');
                }

                chars.Add(i == 0 ? pascal[i] : char.ToLowerInvariant(pascal[i]));
            }

            return new string(chars.ToArray());
        }
    }

    /// <summary>§8.1 — the year's code, its catalogue, and the composer for the next version.</summary>
    public sealed class BehaviorCodeViewModel
    {
        public BehaviorCode? Active { get; set; }

        public IReadOnlyList<BehaviorCode> Versions { get; set; } = Array.Empty<BehaviorCode>();

        public IReadOnlyList<ViolationType> Violations { get; set; } = Array.Empty<ViolationType>();

        public IReadOnlyList<MeritType> Merits { get; set; } = Array.Empty<MeritType>();

        public IReadOnlyList<ConsequenceType> Consequences { get; set; } = Array.Empty<ConsequenceType>();

        /// <summary>(severity, repetition) → consequence, the BR-DCP-005 proposal grid.</summary>
        public IReadOnlyDictionary<(int Severity, int Repetition), ConsequenceType> Ladder { get; set; }
            = new Dictionary<(int, int), ConsequenceType>();

        /// <summary>How many incidents already cite this code — a version is not re-writable, only superseded.</summary>
        public int IncidentsRecorded { get; set; }

        /// <summary>Blank rows the composer offers on top of whatever the active version already holds.</summary>
        public const int BlankRows = 5;

        public const int MaxSeverity = 4;

        public const int MaxRepetition = 3;
    }

    /// <summary>§8.2 — the quick-record desk: pick a section, pick a child, say what happened.</summary>
    public sealed class RecordBehaviorViewModel
    {
        public sealed record RosterEntry(int StudentId, StudentLabel Label, int ViolationCount, int MeritPoints);

        public sealed record RecentEntry(
            DateTime OccurredAtUtc, StudentLabel Student, string What, int Severity, bool IsMerit, string? IncidentNo, int? CaseId);

        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        public int? SectionId { get; set; }

        public IReadOnlyList<RosterEntry> Roster { get; set; } = Array.Empty<RosterEntry>();

        public IReadOnlyList<ViolationType> Violations { get; set; } = Array.Empty<ViolationType>();

        public IReadOnlyList<MeritType> Merits { get; set; } = Array.Empty<MeritType>();

        public IReadOnlyList<RecentEntry> Recent { get; set; } = Array.Empty<RecentEntry>();

        public bool HasPublishedCode { get; set; }

        public DateTime Today { get; set; }
    }

    /// <summary>§8.3 — the officer's board: one column per WF-11 state, aged.</summary>
    public sealed class CaseBoardViewModel
    {
        public sealed record Card(
            DisciplineCase Case, StudentLabel Student, string ViolationAr, string ViolationEn,
            string IncidentNo, int AgeDays, string? ProposedAr, string? ProposedEn, bool HasPendingAppeal);

        public IReadOnlyList<Card> Cards { get; set; } = Array.Empty<Card>();

        public int? SeverityFilter { get; set; }

        public bool IncludeClosed { get; set; }

        /// <summary>Days in a state before the board flags it — doc §8.3's "SLA aging", not a stored rule.</summary>
        public const int AgingThresholdDays = 5;

        public IReadOnlyList<CaseStatus> Columns { get; } = new[]
        {
            CaseStatus.Reported,
            CaseStatus.UnderInvestigation,
            CaseStatus.Decided,
            CaseStatus.ActionApplied,
            CaseStatus.AppealWindow,
        };
    }

    /// <summary>§8.4 — one case, its whole trail, and whatever it can legally do next.</summary>
    public sealed class CaseFileViewModel
    {
        public sealed record TimelineEvent(DateTime AtUtc, string TitleAr, string TitleEn, string? BodyAr, string? BodyEn, string Icon);

        public DisciplineCase Case { get; set; } = null!;

        public Incident Incident { get; set; } = null!;

        public StudentLabel Student { get; set; } = null!;

        public ViolationType Violation { get; set; } = null!;

        public IReadOnlyList<CaseStatement> Statements { get; set; } = Array.Empty<CaseStatement>();

        public ActionApplied? Action { get; set; }

        public Appeal? Appeal { get; set; }

        public IReadOnlyList<ConsequenceType> Consequences { get; set; } = Array.Empty<ConsequenceType>();

        /// <summary>BR-DCP-006: an appeal is filed by a named guardian, so the counter path has to pick one.</summary>
        public IReadOnlyList<GuardianOption> Guardians { get; set; } = Array.Empty<GuardianOption>();

        public sealed record GuardianOption(int ParentId, string NameAr, string NameEn);

        public ConsequenceType? Proposed { get; set; }

        public ConsequenceType? Decided { get; set; }

        public IReadOnlyList<TimelineEvent> Timeline { get; set; } = Array.Empty<TimelineEvent>();

        public int? MaxSuspensionDays { get; set; }

        public int AppealWindowDays { get; set; }

        /// <summary>BR-DCP-003: severity ≥ 3 cannot be decided until a student or parent statement exists.</summary>
        public bool DueProcessSatisfied { get; set; }

        /// <summary>BR-DCP-005/004 shown before the decision rather than as a refusal after it.</summary>
        public bool DecisionNeedsPrincipal { get; set; }

        public DateTime? AppealWindowClosesAtUtc { get; set; }
    }

    /// <summary>§8.5 — consequences in force: who is serving what, and what needs closing.</summary>
    public sealed class ActionTrackerViewModel
    {
        public sealed record Row(
            ActionApplied Action, StudentLabel Student, ConsequenceType Consequence,
            int CaseId, DateTime? EndsOn, bool IsOverdue);

        public sealed record ContractRow(BehaviorContract Contract, StudentLabel Student, bool FullySigned);

        public IReadOnlyList<Row> Detentions { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Suspensions { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<Row> Other { get; set; } = Array.Empty<Row>();

        public IReadOnlyList<ContractRow> Contracts { get; set; } = Array.Empty<ContractRow>();

        public DateTime Today { get; set; }

        public bool ShowCompleted { get; set; }
    }

    /// <summary>§8.6 — where behaviour concentrates. Positive-first per the doc: merits lead.</summary>
    public sealed class BehaviorAnalyticsViewModel
    {
        public sealed record HeatCell(int GradeId, int ViolationTypeId, int Count);

        public sealed record GradeColumn(int GradeId, string NameAr, string NameEn);

        public sealed record ViolationRow(int ViolationTypeId, string NameAr, string NameEn, int Severity);

        public sealed record OffenderRow(StudentLabel Student, int Incidents, int ViolationPoints, int HighestSeverity);

        public sealed record MeritRow(StudentLabel Student, int Awards, int Points);

        public IReadOnlyList<GradeColumn> Grades { get; set; } = Array.Empty<GradeColumn>();

        public IReadOnlyList<ViolationRow> Violations { get; set; } = Array.Empty<ViolationRow>();

        public IReadOnlyList<HeatCell> Cells { get; set; } = Array.Empty<HeatCell>();

        public IReadOnlyList<OffenderRow> RepeatOffenders { get; set; } = Array.Empty<OffenderRow>();

        public IReadOnlyList<MeritRow> MeritLeaders { get; set; } = Array.Empty<MeritRow>();

        /// <summary>Incidents per hour of the school day — doc §8.6's time-of-day dimension.</summary>
        public IReadOnlyDictionary<int, int> ByHour { get; set; } = new Dictionary<int, int>();

        public int MaxCell { get; set; }

        /// <summary>BR-DCP-005's "period" for repetition is the academic year, so the list follows it.</summary>
        public int RepeatThreshold { get; set; } = 2;
    }
}
