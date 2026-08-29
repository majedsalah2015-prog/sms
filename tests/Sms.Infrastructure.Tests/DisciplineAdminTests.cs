using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discipline;
using Sms.Domain.Common;
using Sms.Domain.Discipline;
using Sms.Domain.Numbering;
using Sms.Domain.Parents;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Discipline;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S6/E-603 (Discipline, doc/Modules/25, BR-DCP-001..010) over a real Sqlite-backed AppDbContext.</summary>
    public sealed class DisciplineAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _studentId;
        private int _parentId;

        public DisciplineAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.NumberingSeries.Add(new NumberingSeries { Code = "INC", EntityName = "Incident", FormatTemplate = "INC-{SEQ:4}", ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true });
            var year = new AcademicYear { LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri", StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active };
            db.AcademicYears.Add(year);
            var parent = new Parent { ParentFileNo = "PAR-000001", NameAr = "Guardian", NameEn = "Guardian", PrimaryMobile = "0500000000", UserAccountId = 42 };
            db.Parents.Add(parent);
            var student = new Student
            {
                StudentNo = "STU-1", FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam", Gender = Gender.Male, DateOfBirth = new DateTime(2015, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.StudentGuardianLinks.Add(new StudentGuardianLink { StudentId = student.Id, ParentId = parent.Id, RelationshipLookupId = 1, IsPrimaryContact = true, IsFinanciallyResponsible = true, EffectiveFromUtc = new DateTime(2026, 9, 1) });
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            _studentId = student.Id;
            _parentId = parent.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private DisciplineAdmin CreateAdmin(AppDbContext db) => new(db, new NumberIssuer(db, _tenant, _tenant, _clock), _clock, _audit, _tenant, new NotificationPublisher(db, new TestAddressBook()));

        // Consequences by index: 0 verbal (rank 1), 1 written (2), 2 detention (3), 3 summons (4), 4 external suspension (5, suspension-class)
        private static Task<BehaviorCode> StandardCodeAsync(DisciplineAdmin admin, int? maxSuspensionDays = 3) => admin.DefineBehaviorCodeAsync(
            "Code", "Behavior Code",
            new[]
            {
                new ViolationTypeInput("1.1", "Late", "Late to class", 1, 1), new ViolationTypeInput("2.3", "Disruption", "Class disruption", 2, 5),
                new ViolationTypeInput("3.1", "Fight", "Fighting", 3, 15), new ViolationTypeInput("4.1", "Grave", "Grave misconduct", 4, 30),
            },
            new[] { new MeritTypeInput("Helpful", "Helpfulness", 5, 10) },
            new[]
            {
                new ConsequenceTypeInput(ConsequenceKind.VerbalWarning, "Verbal", "Verbal warning", 1, false),
                new ConsequenceTypeInput(ConsequenceKind.WrittenWarning, "Written", "Written warning", 2, false),
                new ConsequenceTypeInput(ConsequenceKind.Detention, "Detention", "Detention", 3, false),
                new ConsequenceTypeInput(ConsequenceKind.ParentSummons, "Summons", "Parent summons", 4, false),
                new ConsequenceTypeInput(ConsequenceKind.ExternalSuspension, "Suspension", "External suspension", 5, true),
            },
            new[]
            {
                new LadderStepInput(2, 1, 1), new LadderStepInput(2, 2, 2), new LadderStepInput(2, 3, 3),
                new LadderStepInput(3, 1, 2), new LadderStepInput(4, 1, 4),
            },
            maxSuspensionDays: maxSuspensionDays);

        private static ViolationType Violation(BehaviorCode code, string article) => code.ViolationTypes.Single(v => v.ArticleRef == article);

        private static ConsequenceType Consequence(BehaviorCode code, ConsequenceKind kind) => code.ConsequenceTypes.Single(c => c.Kind == kind);

        // --- BR-DCP-002 recording --------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DCP-002")]
        public async Task Severity_one_resolves_teacher_level_and_severity_two_opens_a_case_with_a_ladder_proposal()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);

            var minor = await admin.RecordIncidentAsync(_studentId, Violation(code, "1.1").Id, reporterUserId: 3, "late again", _clock.UtcNow);
            var first = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disrupted class", _clock.UtcNow);
            var second = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disrupted again", _clock.UtcNow);

            Assert.Equal("INC-0001", minor.IncidentNo);
            Assert.True(minor.IsTeacherResolved);
            Assert.Null(minor.CaseId);
            Assert.NotNull(first.CaseId);
            Assert.Equal(Consequence(code, ConsequenceKind.WrittenWarning).Id, db.DisciplineCases.Single(c => c.Id == first.CaseId).ProposedConsequenceTypeId);
            Assert.Equal(Consequence(code, ConsequenceKind.Detention).Id, db.DisciplineCases.Single(c => c.Id == second.CaseId).ProposedConsequenceTypeId);
            Assert.Equal(-11, db.PointLedgerEntries.ToList().Sum(e => e.Points));
        }

        [Fact]
        [BusinessRule("BR-DCP-002")]
        public async Task Merit_points_must_stay_within_the_type_bounds()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var meritType = code.MeritTypes.Single();

            await admin.RecordMeritAsync(_studentId, meritType.Id, 8, 3);
            await Assert.ThrowsAsync<MeritPointsOutOfBoundsException>(() => admin.RecordMeritAsync(_studentId, meritType.Id, 11, 3));

            var (totals, _) = await admin.GetPointsAsync(_studentId, null);
            Assert.Equal(8, totals.MeritPoints);
        }

        // --- BR-DCP-003/004/005 decisions ---------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DCP-003")]
        public async Task A_decision_must_cite_an_article_and_severity_three_needs_a_statement()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var incident = await admin.RecordIncidentAsync(_studentId, Violation(code, "3.1").Id, 3, "fight", _clock.UtcNow);
            var caseId = incident.CaseId!.Value;
            var detention = Consequence(code, ConsequenceKind.Detention).Id;

            await Assert.ThrowsAsync<DecisionArticleRequiredException>(() => admin.DecideAsync(caseId, detention, " ", 5));
            await Assert.ThrowsAsync<StatementsRequiredException>(() => admin.DecideAsync(caseId, detention, "3.1", 5));
            await admin.AddStatementAsync(caseId, StatementKind.Student, "he started it");
            await admin.DecideAsync(caseId, detention, "3.1", 5);

            var decided = db.DisciplineCases.Single();
            Assert.Equal(CaseStatus.Decided, decided.Status);
            Assert.Equal("3.1", decided.DecisionArticleRef);
        }

        [Fact]
        [BusinessRule("BR-DCP-005")]
        public async Task Deviating_below_the_proposal_needs_a_reason_and_above_needs_the_principal()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var incident = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disruption", _clock.UtcNow);   // proposal: written warning (rank 2)
            var caseId = incident.CaseId!.Value;

            await Assert.ThrowsAsync<DecisionDeviationReasonRequiredException>(() => admin.DecideAsync(caseId, Consequence(code, ConsequenceKind.VerbalWarning).Id, "2.3", 5));
            await Assert.ThrowsAsync<PrincipalApprovalRequiredException>(() => admin.DecideAsync(caseId, Consequence(code, ConsequenceKind.Detention).Id, "2.3", 5));
            await admin.DecideAsync(caseId, Consequence(code, ConsequenceKind.VerbalWarning).Id, "2.3", 5, deviationReason: "first offence, remorseful");

            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(DisciplineCase) && e.FieldName == nameof(DisciplineCase.DeviationReason));
            Assert.Equal("first offence, remorseful", audit.Reason);
        }

        [Fact]
        [BusinessRule("BR-DCP-004")]
        public async Task Suspension_class_actions_need_the_principal_and_respect_the_pack_cap()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin, maxSuspensionDays: 3);
            var incident = await admin.RecordIncidentAsync(_studentId, Violation(code, "4.1").Id, 3, "grave", _clock.UtcNow);
            var caseId = incident.CaseId!.Value;
            await admin.AddStatementAsync(caseId, StatementKind.Parent, "statement");
            var suspension = Consequence(code, ConsequenceKind.ExternalSuspension).Id;

            await Assert.ThrowsAsync<PrincipalApprovalRequiredException>(() => admin.DecideAsync(caseId, suspension, "4.1", 5));
            await admin.DecideAsync(caseId, suspension, "4.1", 5, principalUserId: 9);
            await Assert.ThrowsAsync<SuspensionExceedsPackLimitException>(() => admin.ApplyActionAsync(caseId, new DateTime(2026, 10, 6), days: 5));
            var action = await admin.ApplyActionAsync(caseId, new DateTime(2026, 10, 6), days: 3);

            Assert.Equal(9, action.ApprovedByPrincipalUserId);
            Assert.Equal(CaseStatus.AppealWindow, db.DisciplineCases.Single().Status);
        }

        // --- BR-DCP-006 appeals ------------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DCP-006")]
        public async Task One_appeal_within_the_window_reviewed_by_a_non_decider_and_the_case_closes_after()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var incident = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disruption", _clock.UtcNow);
            var caseId = incident.CaseId!.Value;
            await admin.DecideAsync(caseId, Consequence(code, ConsequenceKind.WrittenWarning).Id, "2.3", decidedByUserId: 5);
            await admin.ApplyActionAsync(caseId, new DateTime(2026, 10, 6));

            await Assert.ThrowsAsync<CaseNotClosableException>(() => admin.CloseCaseAsync(caseId));
            var appeal = await admin.FileAppealAsync(caseId, _parentId, "misunderstanding");
            await Assert.ThrowsAsync<AppealNotAllowedException>(() => admin.FileAppealAsync(caseId, _parentId, "again"));
            await Assert.ThrowsAsync<AppealReviewerNotIndependentException>(() => admin.DecideAppealAsync(appeal.Id, reviewerUserId: 5, AppealOutcome.Upheld));
            await admin.DecideAppealAsync(appeal.Id, reviewerUserId: 6, AppealOutcome.Modified, "reduced", Consequence(code, ConsequenceKind.VerbalWarning).Id);
            await admin.CloseCaseAsync(caseId);

            var closed = db.DisciplineCases.Single();
            Assert.Equal(CaseStatus.Closed, closed.Status);
            Assert.Equal(Consequence(code, ConsequenceKind.VerbalWarning).Id, closed.DecidedConsequenceTypeId);
        }

        [Fact]
        [BusinessRule("BR-DCP-006")]
        public async Task An_appeal_after_the_window_is_refused_and_the_case_then_closes_on_its_own()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var incident = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disruption", _clock.UtcNow);
            var caseId = incident.CaseId!.Value;
            await admin.DecideAsync(caseId, Consequence(code, ConsequenceKind.WrittenWarning).Id, "2.3", 5);
            await admin.ApplyActionAsync(caseId, new DateTime(2026, 10, 6));
            _clock.UtcNow = _clock.UtcNow.AddDays(8);

            await Assert.ThrowsAsync<AppealNotAllowedException>(() => admin.FileAppealAsync(caseId, _parentId, "late"));
            await admin.CloseCaseAsync(caseId);
            Assert.Equal(CaseStatus.Closed, db.DisciplineCases.Single().Status);
        }

        // --- BR-DCP-007/008/009 -----------------------------------------------------------------------------

        [Fact]
        [BusinessRule("BR-DCP-007")]
        public async Task Points_aggregate_per_term_and_raise_flags()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            await admin.RecordIncidentAsync(_studentId, Violation(code, "3.1").Id, 3, "fight", _clock.UtcNow, termId: 1);
            await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "disruption", _clock.UtcNow, termId: 1);
            await admin.RecordIncidentAsync(_studentId, Violation(code, "1.1").Id, 3, "late", _clock.UtcNow, termId: 2);

            var (term1, flags) = await admin.GetPointsAsync(_studentId, termId: 1, welfareReviewThreshold: 20);
            var (year, _) = await admin.GetPointsAsync(_studentId, null);

            Assert.Equal(20, term1.ViolationPoints);
            Assert.True(flags.WelfareReview);
            Assert.Equal(21, year.ViolationPoints);
        }

        [Fact]
        [BusinessRule("BR-DCP-008")]
        public async Task The_parent_view_respects_the_policy_level_and_never_names_the_reporter()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);
            var code = await StandardCodeAsync(admin);
            var undecided = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, reporterUserId: 3, "secret narrative", _clock.UtcNow);
            var decided = await admin.RecordIncidentAsync(_studentId, Violation(code, "2.3").Id, 3, "second", _clock.UtcNow);
            await admin.DecideAsync(decided.CaseId!.Value, Consequence(code, ConsequenceKind.ParentSummons).Id, "2.3", 5, principalUserId: 9);

            var full = await admin.GetParentViewAsync(_studentId, PortalVisibilityLevel.Full);
            var decisionsOnly = await admin.GetParentViewAsync(_studentId, PortalVisibilityLevel.DecisionsOnly);
            var summonsOnly = await admin.GetParentViewAsync(_studentId, PortalVisibilityLevel.SummonsOnly);

            Assert.Equal(2, full.Count);
            Assert.Single(decisionsOnly);
            Assert.Null(decisionsOnly[0].Narrative);
            Assert.Equal("Parent summons", summonsOnly.Single().ConsequenceName);
            Assert.DoesNotContain(full, v => v.GetType().GetProperties().Any(p => p.Name.Contains("Reporter")));
        }

        [Fact]
        [BusinessRule("BR-DCP-009")]
        public async Task Keep_apart_pairs_are_normalized_and_contracts_track_signatures()
        {
            using var db = CreateContext();
            var admin = CreateAdmin(db);

            var pair = await admin.AddKeepApartPairAsync(20, 10, "repeated conflict");
            var contract = await admin.DraftBehaviorContractAsync(_studentId, "no phone in class");
            await admin.SignBehaviorContractAsync(contract.Id, parentSigned: true, studentAcknowledged: false);

            Assert.Equal((10, 20), (pair.StudentAId, pair.StudentBId));
            Assert.Single(await admin.ActiveKeepApartPairsAsync());
            var stored = db.BehaviorContracts.Single();
            Assert.NotNull(stored.ParentSignedAtUtc);
            Assert.Null(stored.StudentAcknowledgedAtUtc);
        }
    }
}
