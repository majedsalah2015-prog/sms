using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Audit;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Workflow;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-005 orchestration over a WF-02-shaped P2 flow (Draft → Submitted →
    /// Approved with a final effect), persisted through the E-004 pipeline.
    /// </summary>
    public sealed class WorkflowServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private sealed class GrantAllPermissions : IPermissionService
        {
            public Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => Task.FromResult(true);

            public Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => Task.FromResult<EffectiveScope?>(new EffectiveScope());

            // "Grant all" cannot enumerate what it grants — the caller asks which screen codes exist
            // under a module, and this fake has no catalogue. Empty is the honest answer; no workflow
            // test reads it.
            public Task<IReadOnlyList<string>> GetGrantedScreenCodesAsync(int userAccountId, string moduleCode, ActionVerb action, CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyList<string>>(new List<string>());
        }

        private sealed class CreateAccountEffect : IWorkflowFinalEffect
        {
            private readonly AppDbContext _db;

            public CreateAccountEffect(AppDbContext db)
            {
                _db = db;
            }

            public bool ShouldFail { get; set; }

            public string WorkflowCode => "WF-02";

            public Task ApplyAsync(WorkflowInstance instance, CancellationToken cancellationToken = default)
            {
                if (ShouldFail)
                {
                    throw new InvalidOperationException("Final effect failed.");
                }

                _db.UserAccounts.Add(new UserAccount { UserName = "wf.effect", AccountType = AccountType.Staff });
                return Task.CompletedTask;
            }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly int _submitterId;
        private readonly int _approverId;
        private readonly int _approverRoleId;

        public WorkflowServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            var submitter = new UserAccount { UserName = "registrar", AccountType = AccountType.Staff };
            var approver = new UserAccount { UserName = "principal", AccountType = AccountType.Staff };
            db.UserAccounts.AddRange(submitter, approver);

            var approverRole = new Role { Code = "APPROVER", Name = new LocalizedName("معتمد", "Approver") };
            db.Roles.Add(approverRole);
            db.SaveChanges();

            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = approver.Id, RoleId = approverRole.Id });
            db.SaveChanges();

            _submitterId = submitter.Id;
            _approverId = approver.Id;
            _approverRoleId = approverRole.Id;

            SeedDefinition(db, version: 1);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private void SeedDefinition(AppDbContext db, int version, bool deactivateOlder = false)
        {
            if (deactivateOlder)
            {
                foreach (var old in db.WorkflowDefinitions.Where(d => d.Code == "WF-02"))
                {
                    old.IsActive = false;
                }
            }

            var definition = new WorkflowDefinition
            {
                Code = "WF-02",
                Version = version,
                EntityTypeName = "Enrollment",
                Name = new LocalizedName("إعادة التسجيل", "Re-registration"),
            };
            var draft = new WorkflowState { Code = "Draft", IsInitial = true, Name = new LocalizedName("مسودة", "Draft") };
            var submitted = new WorkflowState { Code = "Submitted", Name = new LocalizedName("مُقدَّم", "Submitted") };
            var approved = new WorkflowState { Code = "Approved", IsFinal = true, Name = new LocalizedName("معتمد", "Approved") };
            definition.States.AddRange(new[] { draft, submitted, approved });
            db.WorkflowDefinitions.Add(definition);
            db.SaveChanges();

            definition.Transitions.AddRange(new[]
            {
                new WorkflowTransition { FromStateId = draft.Id, ToStateId = submitted.Id, Action = WorkflowActionType.Submit },
                new WorkflowTransition { FromStateId = submitted.Id, ToStateId = approved.Id, Action = WorkflowActionType.Approve, RequiredRoleId = _approverRoleId, TriggersFinalEffect = true },
            });
            db.SaveChanges();
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite(_connection)
                .Options;

            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private WorkflowService CreateService(AppDbContext db, CreateAccountEffect? effect = null)
        {
            var events = new AuditEventWriter(db, _tenant, _tenant, _user, _clock, _audit);
            var effects = effect == null ? Array.Empty<IWorkflowFinalEffect>() : new IWorkflowFinalEffect[] { effect };
            return new WorkflowService(db, _user, _tenant, _clock, events, new GrantAllPermissions(), effects);
        }

        private int StateId(AppDbContext db, int definitionId, string code)
            => db.WorkflowStates.Single(s => s.WorkflowDefinitionId == definitionId && s.Code == code).Id;

        [Fact]
        public async Task Start_creates_the_instance_at_the_initial_state()
        {
            using var db = CreateContext();
            _user.UserId = _submitterId;

            var instance = await CreateService(db).StartAsync("WF-02", "Enrollment", 501, "ENR-2027-501");

            Assert.Equal(StateId(db, instance.WorkflowDefinitionId, "Draft"), instance.CurrentStateId);
            Assert.Equal(1, instance.SchoolId);
            Assert.Equal(2027, instance.AcademicYearId);
            Assert.False(instance.IsClosed);
        }

        [Fact]
        [BusinessRule("BR-WF-002")]
        public async Task Every_transition_records_step_trail_and_audit()
        {
            int instanceId;
            using (var db = CreateContext())
            {
                _user.UserId = _submitterId;
                var service = CreateService(db);
                var instance = await service.StartAsync("WF-02", "Enrollment", 501, "ENR-2027-501");
                instanceId = instance.Id;
                await service.ExecuteAsync(instanceId, WorkflowActionType.Submit);

                _user.UserId = _approverId;
                var result = await CreateService(db, new CreateAccountEffect(db)).ExecuteAsync(instanceId, WorkflowActionType.Approve, reason: "Seats available");
                Assert.Equal(WorkflowEvents.Approved, result.RaisedEvent);
            }

            using (var check = CreateContext())
            {
                var steps = check.WorkflowSteps.Where(s => s.WorkflowInstanceId == instanceId).OrderBy(s => s.Id).ToList();
                Assert.Equal(2, steps.Count);
                Assert.Equal(_submitterId, steps[0].ActorUserId);
                Assert.Equal(WorkflowActionType.Submit, steps[0].Action);
                Assert.Equal(_approverId, steps[1].ActorUserId);
                Assert.Equal("Seats available", steps[1].Reason);
                Assert.Equal(_clock.UtcNow, steps[1].OccurredAtUtc);

                // Tamper-evident copy: the WorkflowStep events plus the T2
                // field diff of CurrentStateId (before/after state).
                var eventEntries = check.AuditEntries.Where(e => e.Action == AuditAction.WorkflowStep).ToList();
                Assert.Equal(2, eventEntries.Count);
                Assert.Contains(eventEntries, e => e.Reason == "Seats available");

                var stateDiffs = check.AuditEntries
                    .Where(e => e.EntityType == nameof(WorkflowInstance) && e.FieldName == nameof(WorkflowInstance.CurrentStateId))
                    .ToList();
                Assert.Equal(2, stateDiffs.Count);
                Assert.All(stateDiffs, e => Assert.NotNull(e.OldValue));
            }
        }

        [Fact]
        [BusinessRule("BR-WF-009")]
        public async Task Final_effect_applies_with_the_final_approval()
        {
            using var db = CreateContext();
            _user.UserId = _submitterId;
            var effect = new CreateAccountEffect(db);
            var service = CreateService(db, effect);
            var instance = await service.StartAsync("WF-02", "Enrollment", 501);
            await service.ExecuteAsync(instance.Id, WorkflowActionType.Submit);

            _user.UserId = _approverId;
            await service.ExecuteAsync(instance.Id, WorkflowActionType.Approve);

            using var check = CreateContext();
            Assert.True(check.WorkflowInstances.Single(i => i.Id == instance.Id).IsClosed);
            Assert.Single(check.UserAccounts.Where(a => a.UserName == "wf.effect"));
        }

        [Fact]
        [BusinessRule("BR-WF-009")]
        public async Task Failed_final_effect_leaves_no_approved_but_not_applied_state()
        {
            int instanceId;
            using (var db = CreateContext())
            {
                _user.UserId = _submitterId;
                var service = CreateService(db);
                var instance = await service.StartAsync("WF-02", "Enrollment", 501);
                instanceId = instance.Id;
                await service.ExecuteAsync(instanceId, WorkflowActionType.Submit);
            }

            using (var db = CreateContext())
            {
                _user.UserId = _approverId;
                var effect = new CreateAccountEffect(db) { ShouldFail = true };
                var service = CreateService(db, effect);

                await Assert.ThrowsAsync<InvalidOperationException>(
                    () => service.ExecuteAsync(instanceId, WorkflowActionType.Approve));
            }

            using (var check = CreateContext())
            {
                var instance = check.WorkflowInstances.Single(i => i.Id == instanceId);
                Assert.Equal(StateId(check, instance.WorkflowDefinitionId, "Submitted"), instance.CurrentStateId);
                Assert.False(instance.IsClosed);
                Assert.Single(check.WorkflowSteps.Where(s => s.WorkflowInstanceId == instanceId));
                Assert.Empty(check.UserAccounts.Where(a => a.UserName == "wf.effect"));
            }
        }

        [Fact]
        [BusinessRule("BR-WF-008")]
        public async Task In_flight_instances_complete_on_their_pinned_version()
        {
            int inFlightId;
            int v1DefinitionId;
            using (var db = CreateContext())
            {
                _user.UserId = _submitterId;
                var service = CreateService(db);
                var inFlight = await service.StartAsync("WF-02", "Enrollment", 501);
                inFlightId = inFlight.Id;
                v1DefinitionId = inFlight.WorkflowDefinitionId;
                await service.ExecuteAsync(inFlightId, WorkflowActionType.Submit);

                SeedDefinition(db, version: 2, deactivateOlder: true);
            }

            using (var db = CreateContext())
            {
                _user.UserId = _approverId;
                var service = CreateService(db);

                // The in-flight instance still resolves against v1.
                await service.ExecuteAsync(inFlightId, WorkflowActionType.Approve);
                var completed = db.WorkflowInstances.Single(i => i.Id == inFlightId);
                Assert.Equal(v1DefinitionId, completed.WorkflowDefinitionId);
                Assert.Equal(StateId(db, v1DefinitionId, "Approved"), completed.CurrentStateId);

                // New instances start on v2.
                _user.UserId = _submitterId;
                var fresh = await service.StartAsync("WF-02", "Enrollment", 502);
                Assert.NotEqual(v1DefinitionId, fresh.WorkflowDefinitionId);
            }
        }

        [Fact]
        [BusinessRule("BR-WF-011")]
        public async Task Inbox_shows_pending_items_to_approvers_but_never_self_approvals()
        {
            using var db = CreateContext();
            _user.UserId = _submitterId;
            var service = CreateService(db);
            var instance = await service.StartAsync("WF-02", "Enrollment", 501, "ENR-2027-501");
            await service.ExecuteAsync(instance.Id, WorkflowActionType.Submit);

            var inbox = new ApprovalInboxQuery(db);

            var forApprover = await inbox.GetPendingForAsync(_approverId);
            var item = Assert.Single(forApprover);
            Assert.Equal("WF-02", item.WorkflowCode);
            Assert.Equal("Submitted", item.CurrentStateCode);
            Assert.Equal("ENR-2027-501", item.BusinessKey);

            // The submitter without the approver role sees nothing…
            Assert.Empty(await inbox.GetPendingForAsync(_submitterId));

            // …and even with the role, their own submission is excluded.
            db.RoleAssignments.Add(new RoleAssignment { UserAccountId = _submitterId, RoleId = _approverRoleId });
            await db.SaveChangesAsync();
            Assert.Empty(await inbox.GetPendingForAsync(_submitterId));
        }
    }
}
