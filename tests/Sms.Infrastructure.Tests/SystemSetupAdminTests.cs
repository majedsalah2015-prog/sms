using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Setup;
using Sms.Domain.Grades;
using Sms.Domain.Lookups;
using Sms.Domain.Numbering;
using Sms.Domain.Schools;
using Sms.Domain.Setup;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Grades;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Notifications;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Schools;
using Sms.Infrastructure.Setup;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-101 System Setup (doc/Modules/01) over a real Sqlite-backed
    /// AppDbContext: country packs (BR-SET-004), effective-dated settings
    /// (BR-SET-005/007), feature toggles (BR-SET-006), the wizard checklist
    /// and the first-activation gate (BR-SET-003).
    /// </summary>
    public sealed class SystemSetupAdminTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 18, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 7;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 1;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public SystemSetupAdminTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private SystemSetupAdmin CreateAdmin(AppDbContext db) => new(db, _tenant, _clock, _user, _audit, new NotificationPublisher(db, new TestAddressBook()));

        private static CountryPackDefinition Ksa(decimal vat = 0.15m) => new(
            "KSA-01", "السعودية", "Saudi Arabia", "SA", "SAR", "Arab Standard Time", vat, true,
            new[] { "NationalId", "Iqama", "Passport" }, 10, new[] { "RPT-STU-001" },
            new[] { DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday });

        /// <summary>School row (Id = tenant 1) + Currency lookup with SAR — what the PROFILE/CURRENCY steps need.</summary>
        private async Task<School> SeedSchoolAsync(AppDbContext db, string currency = "SAR")
        {
            var school = await new SchoolAdmin(db).DefineSchoolAsync(null, "مدرسة", "School", "LIC-1", "MIN-1", "Arab Standard Time", currency);
            Assert.Equal(1, school.Id);
            var lookups = new LookupAdmin(db);
            await lookups.DefineCategoryAsync("Currency", LookupCategoryTier.ProductSeeded, "العملة", "Currency");
            await lookups.DefineValueAsync("Currency", "SAR", "ريال", "Riyal", sortOrder: 1);
            return school;
        }

        /// <summary>Everything a fully-ready wizard needs beyond the school: pack, settings, numbering, stage/grade.</summary>
        private async Task MakeAllStepsReadyAsync(AppDbContext db, SystemSetupAdmin admin)
        {
            await admin.DefineCountryPackAsync(Ksa());
            await admin.BindCountryPackAsync("KSA-01");
            await admin.SetSettingAsync(SettingKeys.EnabledLanguages, "ar,en");
            await admin.SetSettingAsync(SettingKeys.DefaultLanguage, "ar");
            await admin.SetSettingAsync(SettingKeys.CalendarType, "Both");
            db.NumberingSeries.Add(new NumberingSeries { Code = "STU", EntityName = "Student", FormatTemplate = "STU-{SEQ:6}", ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Normal, EffectiveFromUtc = _clock.UtcNow, IsActive = true });
            await db.SaveChangesAsync();
            var grades = new GradeStructureAdmin(db);
            var stage = await grades.DefineStageAsync("الابتدائية", "Elementary", 1, GenderPolicy.Mixed);
            await grades.DefineGradeLevelAsync(stage.Id, "G1", "أول", "Grade 1", 1, null, false);
        }

        // --- BR-SET-004 country packs -----------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-004")]
        public async Task Binding_a_pack_seeds_its_defaults_without_overwriting_explicit_settings()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            await admin.DefineCountryPackAsync(Ksa());
            await admin.SetSettingAsync(SettingKeys.WorkingDays, "Monday,Tuesday,Wednesday,Thursday,Friday");

            await admin.BindCountryPackAsync("KSA-01");

            Assert.Equal("0.15", await admin.GetSettingAsync(SettingKeys.VatRate));
            Assert.Equal("true", await admin.GetSettingAsync(SettingKeys.HijriDisplay));
            Assert.Equal("Monday,Tuesday,Wednesday,Thursday,Friday", await admin.GetSettingAsync(SettingKeys.WorkingDays)); // explicit value kept
            Assert.Equal("KSA-01", (await admin.GetBoundCountryPackAsync())!.Code);
        }

        [Fact]
        [BusinessRule("BR-SET-004")]
        public async Task Editing_a_bound_pack_creates_a_new_version_and_schools_stay_pinned()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            var v1 = await admin.DefineCountryPackAsync(Ksa(0.15m));
            await admin.BindCountryPackAsync("KSA-01");

            var v2 = await admin.DefineCountryPackAsync(Ksa(0.10m));

            Assert.NotEqual(v1.Id, v2.Id);
            Assert.Equal(2, v2.Version);
            Assert.False(db.CountryPacks.Single(p => p.Id == v1.Id).IsActive);
            Assert.Equal(v1.Id, (await admin.GetBoundCountryPackAsync())!.Id);

            // Editing an unbound pack updates in place.
            var v3 = await admin.DefineCountryPackAsync(Ksa(0.05m));
            Assert.Equal(v2.Id, v3.Id);
        }

        [Fact]
        [BusinessRule("BR-SET-004")]
        public async Task Changing_the_pack_after_go_live_requires_a_reason()
        {
            using var db = CreateContext();
            var school = await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            await admin.DefineCountryPackAsync(Ksa());
            await admin.DefineCountryPackAsync(Ksa() with { Code = "UAE-01", CountryIsoCode = "AE", DefaultCurrencyCode = "AED" });
            await admin.BindCountryPackAsync("KSA-01");

            _audit.Reason = "go-live";
            await new SchoolAdmin(db).ChangeStatusAsync(school.Id, SchoolStatus.Active);
            _audit.Reason = null;

            await Assert.ThrowsAsync<CountryPackChangeRequiresReasonException>(() => admin.BindCountryPackAsync("UAE-01"));

            await admin.BindCountryPackAsync("UAE-01", reason: "Relocated to Dubai campus");
            Assert.Equal("UAE-01", (await admin.GetBoundCountryPackAsync())!.Code);
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(School) && e.FieldName == nameof(School.CountryPackId) && e.Reason == "Relocated to Dubai campus");
        }

        [Fact]
        [BusinessRule("BR-SET-004")]
        public async Task Binding_an_unknown_pack_is_rejected()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            await Assert.ThrowsAsync<UnknownCountryPackException>(() => CreateAdmin(db).BindCountryPackAsync("MARS-01"));
        }

        // --- BR-SET-005 / BR-SET-007 settings ------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-005")]
        public async Task Unknown_keys_and_invalid_values_are_rejected_server_side()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<UnknownSettingKeyException>(() => admin.SetSettingAsync("Regional.Mascot", "owl"));
            await Assert.ThrowsAsync<InvalidSettingValueException>(() => admin.SetSettingAsync(SettingKeys.WorkingDays, "Sunday,Monday"));
            await Assert.ThrowsAsync<InvalidSettingValueException>(() => admin.SetSettingAsync(SettingKeys.VatRate, "15%"));
        }

        [Fact]
        [BusinessRule("BR-SET-005")]
        public async Task Year_pinned_values_resolve_for_that_year_and_defaults_back_the_rest()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var years = new AcademicYearAdmin(db);
            var y1 = await years.DefineYearAsync("٢٠٢٦", "2026-2027", "١٤٤٨", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));
            var admin = CreateAdmin(db);

            await admin.SetSettingAsync(SettingKeys.VatRate, "0.05");
            await admin.SetSettingAsync(SettingKeys.VatRate, "0.15", y1.Id);

            Assert.Equal("0.15", await admin.GetSettingAsync(SettingKeys.VatRate, y1.Id));
            Assert.Equal("0.05", await admin.GetSettingAsync(SettingKeys.VatRate));
            Assert.Equal("0.05", await admin.GetSettingAsync(SettingKeys.VatRate, 999));
        }

        [Fact]
        [BusinessRule("BR-SET-005")]
        public async Task Only_year_versionable_keys_may_be_pinned_and_financial_rows_cannot_target_an_ended_year()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var years = new AcademicYearAdmin(db);
            var past = await years.DefineYearAsync("٢٠٢٤", "2024-2025", "١٤٤٦", new DateTime(2024, 9, 1), new DateTime(2025, 6, 30));
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<SettingEffectiveDateException>(() => admin.SetSettingAsync(SettingKeys.DefaultLanguage, "en", past.Id));
            await Assert.ThrowsAsync<SettingEffectiveDateException>(() => admin.SetSettingAsync(SettingKeys.VatRate, "0.15", past.Id));
            await Assert.ThrowsAsync<SettingEffectiveDateException>(() => admin.SetSettingAsync(SettingKeys.VatRate, "0.15", 4242));

            // A non-financial versionable key may still be pinned to history (working week of a past year is a fact).
            await admin.SetSettingAsync(SettingKeys.WorkingDays, "Saturday,Sunday,Monday,Tuesday,Wednesday", past.Id);
        }

        [Fact]
        [BusinessRule("BR-SET-007")]
        public async Task Editing_an_existing_setting_is_T1_audited_and_demands_a_reason()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            await admin.SetSettingAsync(SettingKeys.CalendarType, "Gregorian"); // first definition: no reason needed

            _audit.Reason = null;
            await Assert.ThrowsAsync<MissingAuditReasonException>(() => admin.SetSettingAsync(SettingKeys.CalendarType, "Both"));

            db.ChangeTracker.Clear();
            _audit.Reason = "Ministry mandates dual calendar";
            await admin.SetSettingAsync(SettingKeys.CalendarType, "Both");
            Assert.Contains(db.AuditEntries, e => e.EntityType == nameof(SchoolSetting) && e.FieldName == nameof(SchoolSetting.Value) && e.Reason == "Ministry mandates dual calendar");
        }

        // --- BR-SET-006 feature toggles -------------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-006")]
        public async Task Feature_dependencies_block_in_both_directions_and_defaults_apply_without_rows()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);

            Assert.True(await admin.IsEnabledAsync(FeatureCatalog.Transport));
            Assert.False(await admin.IsEnabledAsync(FeatureCatalog.StudentAccounts));

            var ex = await Assert.ThrowsAsync<FeatureDependencyException>(() => admin.SetFeatureAsync(FeatureCatalog.Transport, false));
            Assert.Contains(FeatureCatalog.TransportFees, ex.Blockers);

            await admin.SetFeatureAsync(FeatureCatalog.TransportFees, false);
            await admin.SetFeatureAsync(FeatureCatalog.Transport, false);
            Assert.False(await admin.IsEnabledAsync(FeatureCatalog.Transport));

            await Assert.ThrowsAsync<FeatureDependencyException>(() => admin.SetFeatureAsync(FeatureCatalog.TransportFees, true));
            await Assert.ThrowsAsync<UnknownFeatureException>(() => admin.SetFeatureAsync("TELEPORTATION", true));

            var states = await admin.GetFeatureStatesAsync();
            Assert.Equal(FeatureCatalog.Features.Count, states.Count);
            Assert.False(states[FeatureCatalog.Transport]);
            Assert.True(states[FeatureCatalog.Library]);
        }

        // --- BR-SET-003 wizard + activation gate --------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-003")]
        public async Task A_step_cannot_be_completed_before_its_data_is_in_place()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);

            await Assert.ThrowsAsync<SetupStepNotReadyException>(() => admin.CompleteStepAsync(SetupWizardSteps.CountryPack));
            await Assert.ThrowsAsync<UnknownSetupStepException>(() => admin.CompleteStepAsync("MASCOT"));

            await admin.CompleteStepAsync(SetupWizardSteps.Profile, "looks good");
            var profile = (await admin.GetChecklistAsync()).Single(s => s.Step.Code == SetupWizardSteps.Profile);
            Assert.Equal(SetupStepStatus.Completed, profile.Status);
            Assert.Equal(7, db.SetupChecklists.Single().CompletedByUserId);
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public async Task Setup_complete_needs_every_mandatory_step_and_then_unblocks_the_first_activation()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            var years = new AcademicYearAdmin(db);
            var year = await years.DefineYearAsync("٢٠٢٦", "2026-2027", "١٤٤٨", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            // Gate: School row exists, wizard untouched → activation refused with the pending list.
            var gate = await Assert.ThrowsAsync<SetupIncompleteException>(() => years.ActivateAsync(year.Id));
            Assert.Equal(SetupWizardSteps.All.Count, gate.PendingSteps.Count);

            await MakeAllStepsReadyAsync(db, admin);
            foreach (var step in SetupWizardSteps.All.Take(8))
            {
                await admin.CompleteStepAsync(step.Code);
            }

            var declare = await Assert.ThrowsAsync<SetupIncompleteException>(() => admin.DeclareSetupCompleteAsync());
            Assert.Equal(new[] { SetupWizardSteps.StageStructure }, declare.PendingSteps);
            Assert.False(await admin.IsSetupCompleteAsync());

            await admin.CompleteStepAsync(SetupWizardSteps.StageStructure);
            await admin.DeclareSetupCompleteAsync();
            Assert.True(await admin.IsSetupCompleteAsync());
            Assert.Equal(_clock.UtcNow, db.Schools.Single().SetupCompletedAtUtc);

            await years.ActivateAsync(year.Id);
            Assert.Equal(AcademicYearStatus.Active, db.AcademicYears.Single().Status);
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public async Task Only_the_first_activation_is_gated()
        {
            using var db = CreateContext();
            await SeedSchoolAsync(db);
            var admin = CreateAdmin(db);
            var years = new AcademicYearAdmin(db);
            var y1 = await years.DefineYearAsync("٢٠٢٦", "2026-2027", "١٤٤٨", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));

            await MakeAllStepsReadyAsync(db, admin);
            foreach (var step in SetupWizardSteps.All)
            {
                await admin.CompleteStepAsync(step.Code);
            }

            await admin.DeclareSetupCompleteAsync();
            await years.ActivateAsync(y1.Id);

            // Un-stamp to prove a later activation doesn't re-check the wizard.
            db.Schools.Single().SetupCompletedAtUtc = null;
            _audit.Reason = "test";
            await db.SaveChangesAsync();
            var y2 = await years.DefineYearAsync("٢٠٢٧", "2027-2028", "١٤٤٩", new DateTime(2027, 9, 1), new DateTime(2028, 6, 30));
            await years.ActivateAsync(y2.Id);
            Assert.Equal(AcademicYearStatus.Closing, db.AcademicYears.Single(y => y.Id == y1.Id).Status);
        }

        [Fact]
        [BusinessRule("BR-SET-003")]
        public async Task Without_a_school_row_there_is_no_wizard_to_gate_on()
        {
            using var db = CreateContext();
            var years = new AcademicYearAdmin(db);
            var year = await years.DefineYearAsync("٢٠٢٦", "2026-2027", "١٤٤٨", new DateTime(2026, 9, 1), new DateTime(2027, 6, 30));
            await years.ActivateAsync(year.Id);
            Assert.Equal(AcademicYearStatus.Active, db.AcademicYears.Single().Status);
        }
    }
}
