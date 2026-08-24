using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Discounts;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Fills <c>core.WorkflowDefinition</c> with doc 05 §5's catalogue, WF-01 to
    /// WF-15. The engine has been complete since its own epic and no definition
    /// ever shipped, so every "approval" in the built screens is a status change
    /// with no chain behind it — <c>IWorkflowService.StartAsync</c> throws
    /// "no active workflow definition" for every code a module might name.
    /// <para>
    /// <b>Seeding a definition does not migrate a screen.</b> A module's approval
    /// becomes a real chain only when its controller calls
    /// <c>StartAsync</c>/<c>ExecuteAsync</c> instead of setting a status, and that
    /// is a change to committed behaviour, one module at a time. What this
    /// contributor does is make the catalogue exist so those migrations have
    /// something to start on, and so the approvals inbox has definitions to read.
    /// <b>WF-04 is migrated</b> (M22's manual grants run on this definition); the
    /// other fourteen are catalogued and waiting for their module.
    /// </para>
    /// <para>
    /// <b>Shape.</b> Every flow is generated from doc 05 §3's state vocabulary
    /// (Draft → Submitted → Under Review → Approved | Rejected | Returned |
    /// Cancelled) and §4's patterns: P2 is one level, P3 is a sequential chain of
    /// levels, and P4 adds a routing band so a value under the threshold finishes
    /// at the level it is on. Reject and Return always demand a reason, which the
    /// engine enforces regardless of policy (BR-WF-010).
    /// </para>
    /// <para>
    /// <b>Where the doc could not be met, and why.</b>
    /// <list type="bullet">
    /// <item><b>WF-04's owner tier.</b> §4 routes a discount over 25% to the Owner.
    /// No OWNER role exists in the seeded templates, and binding the tier to
    /// another role would put approval authority somewhere the doc did not. The
    /// chain stops at Finance → Principal; adding the tier is a new
    /// <em>version</em> of WF-04 (BR-WF-008), not an edit to this one.</item>
    /// <item><b>WF-06 and WF-11 routing bands.</b> Both are P4/P5 in the catalogue
    /// and neither doc fixes a number (write-off threshold, severity cut-off).
    /// Inventing one would be a policy decision. Both ship as sequential chains
    /// through every level until a school setting supplies the band.</item>
    /// <item><b>WF-01's seat-availability routing.</b> §5 calls it P4, but the
    /// condition is seat availability, not a value on a scale; the transition
    /// model routes on a decimal only. The chain models the P3 half.</item>
    /// <item><b>WF-03's clearance.</b> doc §11 Q1 is still open on parallel vs
    /// sequential. Modeled sequentially, registrar then finance, which matches the
    /// doc's own recommendation of a finance veto.</item>
    /// <item><b>WF-10 and WF-15 name entities that are not built yet</b> (employee
    /// leave, store/cafeteria void as one code over two sale types). The
    /// definitions are inert until those modules call them — the instance carries
    /// its own entity type, so WF-15 serves <c>StoreSale</c> as well.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Permissions.</b> A transition binds a permission only where the screen
    /// exists and declares the verb, because <c>WorkflowEngine.Authorize</c> denies
    /// by default when a bound permission resolves to no scope — binding a chain to
    /// a screen nobody has built would make it unapprovable. Levels in modules
    /// without screens are gated by role alone until their screens land.
    /// </para>
    /// </summary>
    public class WorkflowCatalogSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;

        public WorkflowCatalogSeedContributor(AppDbContext db)
        {
            _db = db;
        }

        public string Name => "Workflow catalogue WF-01..WF-15 (doc 05 §5)";

        // After the roles (20) whose ids the approval levels name.
        public int Order => 37;

        // ------------------------------------------------------------------ the catalogue

        /// <summary>
        /// One approval level. <paramref name="EscalateAbove"/> is P4's band: a
        /// routing value at or below it finishes here, above it moves to the next
        /// level. Null means this level always passes the record on (or completes
        /// it, when it is the last).
        /// </summary>
        private sealed record Level(string RoleCode, string? Module, string? Screen, decimal? EscalateAbove = null);

        private sealed record Flow(
            string Code, string EntityTypeName, string NameEn, string NameAr,
            Level[] Levels, string? SubmitModule = null, string? SubmitScreen = null,
            bool PortalVisible = false, bool ApprovalNeedsReason = false);

        private const string Principal = "PRINCIPAL";
        private const string VicePrincipal = "VICE_PRINCIPAL";
        private const string Registrar = "REGISTRAR";
        private const string FinanceManager = "FINANCE_MANAGER";
        private const string AdmissionsOfficer = "ADMISSIONS_OFFICER";
        private const string HeadOfDepartment = "HEAD_OF_DEPARTMENT";
        private const string HrOfficer = "HR_OFFICER";
        private const string StageSupervisor = "STAGE_SUPERVISOR";

        private static readonly Flow[] Catalogue =
        {
            new("WF-01", "Application", "Admission application", "طلب القبول",
                new[]
                {
                    new Level(AdmissionsOfficer, ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications),
                    new Level(Registrar, ScreenCatalog.Modules.Admissions, ScreenCatalog.Admissions.Applications),
                },
                PortalVisible: true),

            new("WF-02", "Enrollment", "Re-registration", "إعادة التسجيل",
                new[] { new Level(Registrar, null, null) },
                PortalVisible: true),

            new("WF-03", "Student", "Student withdrawal", "انسحاب الطالب",
                new[]
                {
                    new Level(Registrar, ScreenCatalog.Modules.Students, ScreenCatalog.Students.File),
                    new Level(FinanceManager, null, null),
                }),

            new(DiscountWorkflow.Code, DiscountWorkflow.EntityTypeName, "Discount or scholarship grant", "منح خصم أو منحة",
                new[]
                {
                    new Level(FinanceManager, ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants, EscalateAbove: DiscountWorkflow.FinanceManagerCeilingPercent),
                    new Level(Principal, ScreenCatalog.Modules.Discounts, ScreenCatalog.Discounts.Grants),
                },
                SubmitModule: ScreenCatalog.Modules.Discounts, SubmitScreen: ScreenCatalog.Discounts.Grants),

            new("WF-05", "RefundVoucher", "Refund", "استرداد مبلغ",
                new[]
                {
                    new Level(FinanceManager, ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds),
                    new Level(Principal, ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds),
                },
                SubmitModule: ScreenCatalog.Modules.Payments, SubmitScreen: ScreenCatalog.Payments.Refunds),

            new("WF-06", "Installment", "Fee write-off", "شطب رسوم",
                new[]
                {
                    new Level(FinanceManager, null, null),
                    new Level(Principal, null, null),
                },
                ApprovalNeedsReason: true),

            new("WF-07", "Marksheet", "Marks approval", "اعتماد الدرجات",
                new[]
                {
                    new Level(HeadOfDepartment, ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets),
                    new Level(Registrar, ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets),
                },
                SubmitModule: ScreenCatalog.Modules.Grading, SubmitScreen: ScreenCatalog.Grading.Marksheets),

            new("WF-08", "MarkEntry", "Post-publication mark change", "تعديل درجة بعد النشر",
                new[] { new Level(Principal, ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets) },
                SubmitModule: ScreenCatalog.Modules.Grading, SubmitScreen: ScreenCatalog.Grading.Marksheets,
                ApprovalNeedsReason: true),

            new("WF-09", "CertificateRequest", "Certificate issuance", "إصدار شهادة",
                new[] { new Level(Registrar, null, null) },
                PortalVisible: true),

            new("WF-10", "EmployeeLeave", "Employee leave request", "طلب إجازة موظف",
                new[] { new Level(HrOfficer, null, null) }),

            new("WF-11", "Incident", "Discipline case", "قضية سلوكية",
                new[]
                {
                    new Level(VicePrincipal, null, null),
                    new Level(Principal, null, null),
                },
                ApprovalNeedsReason: true),

            new("WF-12", "TimetableVersion", "Timetable publication", "نشر الجدول الدراسي",
                new[] { new Level(VicePrincipal, ScreenCatalog.Modules.Timetable, ScreenCatalog.Timetable.Versions) }),

            new("WF-13", "AcademicYear", "Posting into a closed year", "ترحيل في عام مغلق",
                new[] { new Level(Principal, ScreenCatalog.Modules.AcademicYears, ScreenCatalog.AcademicYears.Years) },
                ApprovalNeedsReason: true),

            new("WF-14", "AttendanceDay", "Attendance correction", "تصويب حضور يوم سابق",
                new[] { new Level(StageSupervisor, ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Corrections) },
                ApprovalNeedsReason: true),

            new("WF-15", "Sale", "Sale void (store and cafeteria)", "إلغاء عملية بيع (المتجر والمقصف)",
                new[] { new Level(FinanceManager, null, null) },
                ApprovalNeedsReason: true),
        };

        // ------------------------------------------------------------------ state vocabulary (doc 05 §3)

        private const string Draft = "Draft";
        private const string Submitted = "Submitted";
        private const string UnderReview = "UnderReview";
        private const string Approved = "Approved";
        private const string Rejected = "Rejected";
        private const string Returned = "Returned";
        private const string Cancelled = "Cancelled";

        private static readonly (string Code, string Ar, string En)[] LevelStates =
        {
            (Submitted, "مُقدَّم", "Submitted"),
            (UnderReview, "قيد المراجعة", "Under review"),
        };

        // ------------------------------------------------------------------ seeding

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            if (!await _db.Schools.AnyAsync(cancellationToken))
            {
                return;
            }

            // Any version counts as present: WorkflowDefinition is deliberately not
            // soft-active filtered, because a superseded version must stay loadable
            // for the instances pinned to it (BR-WF-008). Re-seeding must never add
            // a second version of a code a school already has.
            var existing = await _db.WorkflowDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Where(d => d.SchoolId == _db.CurrentSchoolId)
                .Select(d => d.Code)
                .ToListAsync(cancellationToken);
            var have = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var roles = await _db.Roles.AsNoTracking()
                .Select(r => new { r.Id, r.Code })
                .ToListAsync(cancellationToken);
            var roleIds = roles.ToDictionary(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase);

            foreach (var flow in Catalogue)
            {
                if (have.Contains(flow.Code))
                {
                    continue;
                }

                // A level whose role the school has retired leaves the chain with a
                // gap nobody could approve. Skip the whole flow rather than seed half.
                if (flow.Levels.Any(l => !roleIds.ContainsKey(l.RoleCode)))
                {
                    continue;
                }

                await SeedFlowAsync(flow, roleIds, cancellationToken);
                _db.ChangeTracker.Clear();
            }
        }

        private async Task SeedFlowAsync(Flow flow, IReadOnlyDictionary<string, int> roleIds, CancellationToken cancellationToken)
        {
            var definition = new WorkflowDefinition
            {
                Code = flow.Code,
                Version = 1,
                EntityTypeName = flow.EntityTypeName,
                Name = new LocalizedName(flow.NameAr, flow.NameEn),
            };

            var draft = State(Draft, "مسودة", "Draft", isInitial: true, isEditable: true);
            var approved = State(Approved, "معتمد", "Approved", isFinal: true, isPortalVisible: flow.PortalVisible);
            var rejected = State(Rejected, "مرفوض", "Rejected", isFinal: true, isPortalVisible: flow.PortalVisible);
            var returned = State(Returned, "مُعاد للتصحيح", "Returned", isEditable: true);
            var cancelled = State(Cancelled, "ملغى", "Cancelled", isFinal: true);

            var levelStates = flow.Levels
                .Select((_, i) => State(LevelStates[i].Code, LevelStates[i].Ar, LevelStates[i].En, isPortalVisible: flow.PortalVisible))
                .ToList();

            definition.States.Add(draft);
            definition.States.AddRange(levelStates);
            definition.States.AddRange(new[] { approved, rejected, returned, cancelled });

            _db.WorkflowDefinitions.Add(definition);
            await _db.SaveChangesAsync(cancellationToken);

            var transitions = new List<WorkflowTransition>
            {
                Move(draft, levelStates[0], WorkflowActionType.Submit, null, flow.SubmitModule, flow.SubmitScreen, ActionVerb.Submit),
                Move(draft, cancelled, WorkflowActionType.Cancel, null, null, null, null),
                Move(returned, levelStates[0], WorkflowActionType.Submit, null, flow.SubmitModule, flow.SubmitScreen, ActionVerb.Submit),
                Move(returned, cancelled, WorkflowActionType.Cancel, null, null, null, null),
            };

            for (var i = 0; i < flow.Levels.Length; i++)
            {
                var level = flow.Levels[i];
                var from = levelStates[i];
                var approver = roleIds[level.RoleCode];
                var next = i + 1 < levelStates.Count ? levelStates[i + 1] : approved;

                if (level.EscalateAbove is decimal band && i + 1 < levelStates.Count)
                {
                    // P4 (BR-WF-005): at or below the band this level is the whole
                    // chain; above it the record moves up. The two ranges are
                    // disjoint, so the engine's first-match is deterministic.
                    var settles = Move(from, approved, WorkflowActionType.Approve, approver, level.Module, level.Screen, ActionVerb.Approve, flow.ApprovalNeedsReason);
                    settles.MaxRoutingValue = band;
                    settles.TriggersFinalEffect = true;
                    transitions.Add(settles);

                    var escalates = Move(from, next, WorkflowActionType.Approve, approver, level.Module, level.Screen, ActionVerb.Approve, flow.ApprovalNeedsReason);
                    escalates.MinRoutingValue = band;
                    transitions.Add(escalates);
                }
                else
                {
                    var move = Move(from, next, WorkflowActionType.Approve, approver, level.Module, level.Screen, ActionVerb.Approve, flow.ApprovalNeedsReason);
                    move.TriggersFinalEffect = ReferenceEquals(next, approved);
                    transitions.Add(move);
                }

                transitions.Add(Move(from, rejected, WorkflowActionType.Reject, approver, level.Module, level.Screen, ActionVerb.Approve, needsReason: true));
                transitions.Add(Move(from, returned, WorkflowActionType.Return, approver, level.Module, level.Screen, ActionVerb.Approve, needsReason: true));
            }

            definition.Transitions.AddRange(transitions);
            await _db.SaveChangesAsync(cancellationToken);
        }

        private static WorkflowState State(
            string code, string ar, string en,
            bool isInitial = false, bool isFinal = false, bool isEditable = false, bool isPortalVisible = false)
            => new()
            {
                Code = code,
                Name = new LocalizedName(ar, en),
                IsInitial = isInitial,
                IsFinal = isFinal,
                IsEditableInState = isEditable,
                IsPortalVisible = isPortalVisible,
            };

        private static WorkflowTransition Move(
            WorkflowState from, WorkflowState to, WorkflowActionType action, int? requiredRoleId,
            string? module, string? screen, ActionVerb? verb, bool needsReason = false)
            => new()
            {
                FromStateId = from.Id,
                ToStateId = to.Id,
                Action = action,
                RequiredRoleId = requiredRoleId,
                ReasonPolicy = needsReason ? ReasonPolicy.Required : ReasonPolicy.Optional,
                PermissionModuleCode = module,
                PermissionScreenCode = screen,
                PermissionAction = module == null ? null : verb,
            };
    }
}
