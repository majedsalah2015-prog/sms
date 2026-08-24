using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Workflow;
using Sms.Domain.Schools;
using Sms.Domain.Workflow;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Seeding;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// A seeded workflow is a state machine, and a state machine is wrong in ways a
    /// row count cannot see: an unreachable state, a chain with no way back to the
    /// submitter, two threshold bands that overlap so which approver is asked
    /// depends on insertion order. These tests run the real engine over the seeded
    /// definitions rather than asserting the rows exist.
    /// </summary>
    public sealed class WorkflowCatalogSeedContributorTests : IDisposable
    {
        private sealed class Tenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 1;
        }

        private sealed class User : ICurrentUser
        {
            public int UserId => 0;
        }

        private sealed class Clock : IClock
        {
            public DateTime UtcNow => new(2026, 8, 23, 9, 0, 0, DateTimeKind.Utc);
        }

        private readonly SqliteConnection _connection;

        public WorkflowCatalogSeedContributorTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
            db.Schools.Add(new School { NameAr = "مدرسة", NameEn = "School", LicenseNumber = "LIC-1", MinistryCode = "MIN-1" });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
            => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), new User(), new Clock());

        private async Task SeedAsync()
        {
            using var db = CreateContext();
            await new RoleTemplateSeedContributor(db).SeedAsync();
            await new PermissionSeedContributor(db).SeedAsync();
            await new WorkflowCatalogSeedContributor(db).SeedAsync();
        }

        private static async Task<WorkflowDefinition> LoadAsync(AppDbContext db, string code)
            => await db.WorkflowDefinitions
                .Include(d => d.States)
                .Include(d => d.Transitions)
                .SingleAsync(d => d.Code == code);

        [Fact]
        [BusinessRule("BR-WF-008")]
        public async Task The_catalogue_lands_once_and_re_running_adds_no_second_version()
        {
            await SeedAsync();

            using (var db = CreateContext())
            {
                var codes = await db.WorkflowDefinitions.AsNoTracking().Select(d => d.Code).OrderBy(c => c).ToListAsync();
                Assert.Equal(15, codes.Count);
                Assert.Equal("WF-01", codes.First());
                Assert.Equal("WF-15", codes.Last());
                Assert.All(await db.WorkflowDefinitions.AsNoTracking().ToListAsync(), d => Assert.Equal(1, d.Version));
            }

            await SeedAsync();

            using var again = CreateContext();
            Assert.Equal(15, await again.WorkflowDefinitions.CountAsync());
            Assert.Equal(15, await again.WorkflowDefinitions.Select(d => d.Code).Distinct().CountAsync());
        }

        [Fact]
        [BusinessRule("BR-WF-001")]
        public async Task Every_definition_is_a_reachable_state_machine()
        {
            await SeedAsync();
            using var db = CreateContext();

            foreach (var code in await db.WorkflowDefinitions.AsNoTracking().Select(d => d.Code).ToListAsync())
            {
                var definition = await LoadAsync(db, code);
                var stateIds = definition.States.Select(s => s.Id).ToHashSet();

                Assert.Single(definition.States.Where(s => s.IsInitial));
                Assert.NotEmpty(definition.States.Where(s => s.IsFinal));
                Assert.False(string.IsNullOrWhiteSpace(definition.Name.NameAr), $"{code} has no Arabic name");
                Assert.False(string.IsNullOrWhiteSpace(definition.Name.NameEn), $"{code} has no English name");

                foreach (var transition in definition.Transitions)
                {
                    Assert.Contains(transition.FromStateId, stateIds);
                    Assert.Contains(transition.ToStateId, stateIds);
                }

                // Every non-initial state is arrived at by some transition, and every
                // non-final state can be left again — an orphan or a dead end would
                // strand a record with no legal move (BR-WF-001).
                foreach (var state in definition.States)
                {
                    if (!state.IsInitial)
                    {
                        Assert.Contains(definition.Transitions, t => t.ToStateId == state.Id);
                    }

                    if (!state.IsFinal)
                    {
                        Assert.Contains(definition.Transitions, t => t.FromStateId == state.Id);
                    }
                }
            }
        }

        [Fact]
        [BusinessRule("BR-WF-010")]
        public async Task Rejecting_and_returning_always_demand_a_reason_and_return_reaches_the_submitter()
        {
            await SeedAsync();
            using var db = CreateContext();

            foreach (var code in await db.WorkflowDefinitions.AsNoTracking().Select(d => d.Code).ToListAsync())
            {
                var definition = await LoadAsync(db, code);
                var returned = definition.States.Single(s => s.Code == "Returned");

                foreach (var transition in definition.Transitions.Where(t =>
                             t.Action == WorkflowActionType.Reject || t.Action == WorkflowActionType.Return))
                {
                    Assert.Equal(ReasonPolicy.Required, transition.ReasonPolicy);
                }

                // Returned is editable and leads back into the chain (doc 05 §3).
                Assert.True(returned.IsEditableInState);
                Assert.Contains(definition.Transitions, t => t.FromStateId == returned.Id && t.Action == WorkflowActionType.Submit);
            }
        }

        /// <summary>
        /// doc 05 §4 P4: a discount at or under 10% is the finance manager's alone;
        /// above it the record moves to the principal. The bands must be disjoint,
        /// or which approver is asked depends on the order the rows were inserted.
        /// </summary>
        [Fact]
        [BusinessRule("BR-WF-005")]
        public async Task Threshold_routing_sends_a_small_discount_to_finance_and_a_large_one_upward()
        {
            await SeedAsync();
            using var db = CreateContext();
            var definition = await LoadAsync(db, "WF-04");

            var submitted = definition.States.Single(s => s.Code == "Submitted");
            var approved = definition.States.Single(s => s.Code == "Approved");
            var review = definition.States.Single(s => s.Code == "UnderReview");

            Assert.Equal(approved.Id, RouteOf(definition, submitted, 5m).ToStateId);
            Assert.Equal(approved.Id, RouteOf(definition, submitted, 10m).ToStateId);
            Assert.Equal(review.Id, RouteOf(definition, submitted, 10.01m).ToStateId);
            Assert.Equal(review.Id, RouteOf(definition, submitted, 40m).ToStateId);

            // Only the transition that lands on Approved may fire the final effect
            // (BR-WF-009) — an escalation must not apply the discount.
            Assert.True(RouteOf(definition, submitted, 5m).TriggersFinalEffect);
            Assert.False(RouteOf(definition, submitted, 40m).TriggersFinalEffect);
        }

        [Fact]
        [BusinessRule("BR-WF-004")]
        public async Task Every_bound_permission_names_a_screen_the_catalogue_defines()
        {
            await SeedAsync();
            using var db = CreateContext();

            foreach (var transition in await db.WorkflowTransitions.AsNoTracking().ToListAsync())
            {
                if (transition.PermissionModuleCode == null)
                {
                    Assert.Null(transition.PermissionScreenCode);
                    Assert.Null(transition.PermissionAction);
                    continue;
                }

                Assert.NotNull(transition.PermissionScreenCode);
                Assert.NotNull(transition.PermissionAction);
                Assert.True(
                    ScreenCatalog.Defines(transition.PermissionModuleCode, transition.PermissionScreenCode!, transition.PermissionAction!.Value),
                    $"transition binds {transition.PermissionModuleCode}/{transition.PermissionScreenCode}/{transition.PermissionAction}, which the screen catalogue does not define — the engine would deny it forever");
            }
        }

        [Fact]
        [BusinessRule("BR-WF-009")]
        public async Task Exactly_the_transitions_that_complete_a_chain_fire_the_final_effect()
        {
            await SeedAsync();
            using var db = CreateContext();

            foreach (var code in await db.WorkflowDefinitions.AsNoTracking().Select(d => d.Code).ToListAsync())
            {
                var definition = await LoadAsync(db, code);
                var approved = definition.States.Single(s => s.Code == "Approved");

                foreach (var transition in definition.Transitions)
                {
                    Assert.Equal(transition.ToStateId == approved.Id, transition.TriggersFinalEffect);
                }
            }
        }

        private static WorkflowTransition RouteOf(WorkflowDefinition definition, WorkflowState from, decimal routingValue)
        {
            var instance = WorkflowInstance.Start(definition, from, "DiscountGrant", 1, 1, routingValue: routingValue);
            return WorkflowEngine.FindTransition(definition, instance, WorkflowActionType.Approve);
        }
    }
}
