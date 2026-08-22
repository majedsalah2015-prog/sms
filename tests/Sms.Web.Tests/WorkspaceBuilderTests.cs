using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Schools;
using Sms.Domain.Security;
using Sms.Domain.Setup;
using Sms.Web.Navigation;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The landing page answers "what is my job", and the only way it can lie is by showing a person
    /// a department they cannot work in — or hiding one they can. Both are silent: the tile simply
    /// is or is not there, and nothing throws either way.
    /// </summary>
    public class WorkspaceBuilderTests
    {
        /// <summary>Grants exactly the module/screen pairs it is given, and records what was asked.</summary>
        private sealed class StubPermissions : IPermissionService
        {
            private readonly HashSet<(string, string)> _granted;

            public StubPermissions(params (string Module, string Screen)[] granted) =>
                _granted = new HashSet<(string, string)>(granted.Select(g => (g.Module, g.Screen)));

            public int Questions { get; private set; }

            public Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
            {
                Questions++;
                return Task.FromResult(_granted.Contains((moduleCode, screenCode)));
            }

            public Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Task<IReadOnlyList<string>> GetGrantedScreenCodesAsync(int userAccountId, string moduleCode, ActionVerb action, CancellationToken cancellationToken = default)
                => throw new NotSupportedException();
        }

        /// <summary>Every feature on unless named as off.</summary>
        private sealed class StubSetup : ISystemSetupAdmin
        {
            private readonly HashSet<string> _off;

            public StubSetup(params string[] off) => _off = new HashSet<string>(off);

            public Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(CancellationToken cancellationToken = default)
                => Task.FromResult<IReadOnlyDictionary<string, bool>>(
                    FeatureCatalog.Features.ToDictionary(f => f.Code, f => !_off.Contains(f.Code)));

            public Task<CountryPack> DefineCountryPackAsync(CountryPackDefinition definition, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task BindCountryPackAsync(string packCode, string? reason = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<CountryPack?> GetBoundCountryPackAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<SchoolSetting> SetSettingAsync(string key, string value, int? academicYearId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<SchoolSetting>> ListSettingsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task SetFeatureAsync(string featureCode, bool enabled, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<IReadOnlyList<StepState>> GetChecklistAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task CompleteStepAsync(string stepCode, string? notes = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task DeclareSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
            public Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        }

        private static WorkspaceBuilder Builder(
            IPermissionService permissions, ISystemSetupAdmin? setup = null, ErpNavigationSource? erp = null) =>
            new(new ModuleVisibility(permissions), setup ?? new StubSetup(), erp ?? TestErpNavigation.EmptySource());

        [Fact]
        public async Task An_account_with_no_grants_sees_no_departments()
        {
            var workspaces = await Builder(new StubPermissions()).BuildAllAsync(TestErpNavigation.Holding());

            Assert.Empty(workspaces);
        }

        /// <summary>
        /// One grant brings its own department and nothing else — not the module's other screens, and
        /// certainly not the six departments this person has no business in.
        /// </summary>
        [Fact]
        public async Task One_grant_opens_one_department_with_one_screen()
        {
            var permissions = new StubPermissions(("TTB", "Cover"));

            var workspaces = await Builder(permissions).BuildAllAsync(TestErpNavigation.Holding());

            var cover = Assert.Single(workspaces);
            Assert.Equal("cover", cover.Info.Key);
            var link = Assert.Single(cover.Links);
            Assert.Equal("Cover", link.Action);
            Assert.True(cover.IsSingleScreen);
        }

        /// <summary>
        /// The timetable grant does not leak into the cover rota, nor the other way. They are one
        /// module and two departments precisely because a school separates the two jobs.
        /// </summary>
        [Fact]
        public async Task Building_the_timetable_and_covering_for_today_are_separate_grants()
        {
            var permissions = new StubPermissions(("TTB", "Builder"));

            var workspaces = await Builder(permissions).BuildAllAsync(TestErpNavigation.Holding());

            var timetable = Assert.Single(workspaces);
            Assert.Equal("timetable", timetable.Info.Key);
            Assert.DoesNotContain(timetable.Links, l => l.Action == "Cover");
        }

        [Fact]
        public async Task A_department_that_survives_keeps_only_the_screens_the_user_holds()
        {
            var permissions = new StubPermissions(("FEE", "Charges"), ("PAY", "Cashier"));

            var workspaces = await Builder(permissions).BuildAllAsync(TestErpNavigation.Holding());

            var finance = Assert.Single(workspaces);
            Assert.Equal("finance", finance.Info.Key);
            Assert.Equal(2, finance.Links.Count);
            Assert.Equal(2, finance.ScreenCount);
            Assert.False(finance.IsSingleScreen);
        }

        /// <summary>
        /// BR-SET-006: a feature switched off at the deployment removes its screens for everyone,
        /// grant or no grant. The two filters are independent and both have to apply — instalments
        /// are optional, the fee ledger they instalment is not, which is why the toggle exists on one
        /// and not the other.
        /// </summary>
        [Fact]
        public async Task A_feature_switched_off_removes_its_screens_even_from_someone_who_holds_them()
        {
            var permissions = new StubPermissions(("FEE", "Charges"), ("INS", "Templates"));

            var workspaces = await Builder(permissions, new StubSetup(FeatureCatalog.Installments))
                .BuildAllAsync(TestErpNavigation.Holding());

            var finance = Assert.Single(workspaces);
            var link = Assert.Single(finance.Links);
            Assert.Equal("FEE", link.ModuleCode);
        }

        /// <summary>The same grant with the feature left on keeps both — proving the test above measures the toggle.</summary>
        [Fact]
        public async Task The_same_grant_keeps_both_screens_when_the_feature_is_on()
        {
            var permissions = new StubPermissions(("FEE", "Charges"), ("INS", "Templates"));

            var workspaces = await Builder(permissions).BuildAllAsync(TestErpNavigation.Holding());

            var finance = Assert.Single(workspaces);
            Assert.Equal(2, finance.Links.Count);
        }

        /// <summary>
        /// The embedded ERP reaches the launcher the same way it reaches the sidebar: through its own
        /// navigation, filtered by its own claims. It lands in finance and nowhere else.
        /// </summary>
        [Fact]
        public async Task The_embedded_accounting_arrives_in_finance_only()
        {
            var permissions = new StubPermissions(("FEE", "Charges"), ("TTB", "Builder"));

            var workspaces = await Builder(permissions, erp: TestErpNavigation.Source())
                .BuildAllAsync(TestErpNavigation.Administrator());

            var finance = Assert.Single(workspaces, w => w.Info.Key == "finance");
            Assert.NotEmpty(finance.ErpGroups);
            Assert.True(finance.ScreenCount > finance.Links.Count);

            var timetable = Assert.Single(workspaces, w => w.Info.Key == "timetable");
            Assert.Empty(timetable.ErpGroups);
        }

        /// <summary>
        /// A user who holds no school screen but does hold accounting still gets the finance tile —
        /// the accountant of a school whose fee desk is somebody else's job.
        /// </summary>
        [Fact]
        public async Task Accounting_alone_is_enough_to_open_the_finance_department()
        {
            var workspaces = await Builder(new StubPermissions(), erp: TestErpNavigation.Source())
                .BuildAllAsync(TestErpNavigation.Administrator());

            var finance = Assert.Single(workspaces);
            Assert.Equal("finance", finance.Info.Key);
            Assert.Empty(finance.Links);
            Assert.NotEmpty(finance.ErpGroups);
        }

        [Fact]
        public async Task A_department_the_user_cannot_enter_is_null_rather_than_empty()
        {
            var builder = Builder(new StubPermissions(("TTB", "Cover")));

            Assert.NotNull(await builder.BuildAsync("cover", TestErpNavigation.Holding()));
            Assert.Null(await builder.BuildAsync("finance", TestErpNavigation.Holding()));
            Assert.Null(await builder.BuildAsync("payroll", TestErpNavigation.Holding()));
        }
    }
}
