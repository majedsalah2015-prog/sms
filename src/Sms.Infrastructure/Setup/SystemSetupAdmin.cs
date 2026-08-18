using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Notifications;
using Sms.Application.Setup;
using Sms.Domain.Schools;
using Sms.Domain.Setup;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Setup
{
    /// <summary>
    /// E-101 System Setup (doc/Modules/01) — standalone admin over the
    /// tenant's School: country pack binding (BR-SET-004), effective-dated
    /// settings (BR-SET-005), feature toggles (BR-SET-006) and the Setup
    /// Wizard checklist (BR-SET-003). Audit tiers come from the entity tags
    /// (BR-SET-007); the only bespoke reason rule is the post-go-live country
    /// pack change, which the attribute can't express.
    /// </summary>
    public class SystemSetupAdmin : ISystemSetupAdmin, IFeatureGate
    {
        public const string CurrencyLookupCategory = "Currency";

        public const string SetupStepCompletedEvent = "SetupStepCompleted";
        public const string SettingChangedEvent = "SettingChanged";
        public const string CountryPackChangedEvent = "CountryPackChanged";

        private readonly AppDbContext _db;
        private readonly ITenantContext _tenant;
        private readonly IClock _clock;
        private readonly ICurrentUser _currentUser;
        private readonly IAuditContext _audit;
        private readonly INotificationPublisher _notifications;

        public SystemSetupAdmin(
            AppDbContext db, ITenantContext tenant, IClock clock, ICurrentUser currentUser, IAuditContext audit, INotificationPublisher notifications)
        {
            _db = db;
            _tenant = tenant;
            _clock = clock;
            _currentUser = currentUser;
            _audit = audit;
            _notifications = notifications;
        }

        // ------------------------------------------------------------------
        // Country packs
        // ------------------------------------------------------------------

        public async Task<CountryPack> DefineCountryPackAsync(CountryPackDefinition d, CancellationToken cancellationToken = default)
        {
            var current = await _db.CountryPacks
                .Where(p => p.Code == d.Code && p.IsActive)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken);

            CountryPack pack;
            if (current == null)
            {
                pack = new CountryPack { Code = d.Code, Version = 1 };
                _db.CountryPacks.Add(pack);
            }
            else if (await _db.Schools.IgnoreQueryFilters().AnyAsync(s => s.CountryPackId == current.Id, cancellationToken))
            {
                // Bound somewhere: deactivate-and-new-row so pinned schools keep resolving v(n).
                current.IsActive = false;
                pack = new CountryPack { Code = d.Code, Version = current.Version + 1 };
                _db.CountryPacks.Add(pack);
            }
            else
            {
                pack = current;
            }

            pack.Name.NameAr = d.NameAr;
            pack.Name.NameEn = d.NameEn;
            pack.CountryIsoCode = d.CountryIsoCode;
            pack.DefaultCurrencyCode = d.DefaultCurrencyCode;
            pack.DefaultTimeZoneId = d.DefaultTimeZoneId;
            pack.DefaultVatRate = d.DefaultVatRate;
            pack.HijriDisplayDefault = d.HijriDisplayDefault;
            pack.RequiredIdTypeCodes = string.Join(",", d.RequiredIdTypeCodes);
            pack.AuditRetentionYearsMinimum = d.AuditRetentionYearsMinimum;
            pack.StatutoryReportCodes = string.Join(",", d.StatutoryReportCodes);
            pack.DefaultWorkingDays = WorkingWeek.Format(d.DefaultWorkingDays);
            pack.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return pack;
        }

        public async Task BindCountryPackAsync(string packCode, string? reason = null, CancellationToken cancellationToken = default)
        {
            var pack = await _db.CountryPacks
                .Where(p => p.Code == packCode && p.IsActive)
                .OrderByDescending(p => p.Version)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new UnknownCountryPackException(packCode);

            var school = await RequireSchoolAsync(cancellationToken);
            if (school.CountryPackId == pack.Id)
            {
                return;
            }

            var isChangeAfterGoLive = school.CountryPackId != null && school.Status != SchoolStatus.Setup;
            if (isChangeAfterGoLive)
            {
                if (string.IsNullOrWhiteSpace(reason) && string.IsNullOrWhiteSpace(_audit.Reason))
                {
                    throw new CountryPackChangeRequiresReasonException();
                }

                _audit.Reason ??= reason;
            }

            var previous = school.CountryPackId;
            school.CountryPackId = pack.Id;

            // BR-SET-004: the pack binds defaults; the wizard pre-fills from them
            // without overwriting values a school already set explicitly.
            await SeedDefaultIfMissingAsync(SettingKeys.HijriDisplay, pack.HijriDisplayDefault ? "true" : "false", SettingValueType.Boolean, cancellationToken);
            if (!string.IsNullOrEmpty(pack.DefaultWorkingDays))
            {
                await SeedDefaultIfMissingAsync(SettingKeys.WorkingDays, pack.DefaultWorkingDays, SettingValueType.CodeList, cancellationToken);
            }

            await SeedDefaultIfMissingAsync(SettingKeys.VatRate, pack.DefaultVatRate.ToString(CultureInfo.InvariantCulture), SettingValueType.Decimal, cancellationToken);

            await _db.SaveChangesAsync(cancellationToken);

            if (previous != null)
            {
                await PublishToCurrentUserAsync(CountryPackChangedEvent, new Dictionary<string, string> { ["pack"] = pack.Code, ["version"] = pack.Version.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
            }
        }

        public async Task<CountryPack?> GetBoundCountryPackAsync(CancellationToken cancellationToken = default)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId, cancellationToken);
            return school?.CountryPackId == null
                ? null
                : await _db.CountryPacks.AsNoTracking().SingleAsync(p => p.Id == school.CountryPackId, cancellationToken);
        }

        // ------------------------------------------------------------------
        // Settings
        // ------------------------------------------------------------------

        public async Task<SchoolSetting> SetSettingAsync(string key, string value, int? academicYearId = null, CancellationToken cancellationToken = default)
        {
            if (!SettingKeys.TryGet(key, out var definition))
            {
                throw new UnknownSettingKeyException(key);
            }

            var problem = definition.Validate(value ?? string.Empty);
            if (problem != null)
            {
                throw new InvalidSettingValueException(key, problem);
            }

            if (academicYearId is int yearId)
            {
                if (!definition.YearVersionable)
                {
                    throw new SettingEffectiveDateException(key, "this key is school-wide, not year-versionable");
                }

                var year = await _db.AcademicYears.AsNoTracking().SingleOrDefaultAsync(y => y.Id == yearId, cancellationToken)
                    ?? throw new SettingEffectiveDateException(key, $"academic year {yearId} does not exist");

                // doc §9: "VAT rate change requires effective date ≥ today" — an
                // ended year is history; its in-force value must stay what it was.
                if (definition.Group == "Financial" && year.EndDate.Date < _clock.UtcNow.Date)
                {
                    throw new SettingEffectiveDateException(key, $"academic year '{year.LabelEn}' has already ended");
                }
            }

            var row = await _db.SchoolSettings.SingleOrDefaultAsync(s => s.Key == definition.Key && s.AcademicYearId == academicYearId, cancellationToken);
            var isNew = row == null;
            if (row == null)
            {
                row = new SchoolSetting { Key = definition.Key, AcademicYearId = academicYearId, ValueType = definition.ValueType };
                _db.SchoolSettings.Add(row);
            }

            var changed = isNew || row.Value != value;
            row.Value = value!;
            row.ValueType = definition.ValueType;
            await _db.SaveChangesAsync(cancellationToken);

            if (changed && !isNew && definition.Group == "Financial")
            {
                // doc §12: SettingChanged (financial keys) → Principal + Finance Manager.
                // Role-based recipient resolution is E-703's matrix work; until then
                // the acting sys admin gets the in-app trace so the event isn't lost.
                await PublishToCurrentUserAsync(SettingChangedEvent, new Dictionary<string, string> { ["key"] = definition.Key, ["value"] = value! }, cancellationToken);
            }

            return row;
        }

        public async Task<string?> GetSettingAsync(string key, int? academicYearId = null, CancellationToken cancellationToken = default)
        {
            if (!SettingKeys.TryGet(key, out var definition))
            {
                throw new UnknownSettingKeyException(key);
            }

            var rows = await _db.SchoolSettings.AsNoTracking().Where(s => s.Key == definition.Key).ToListAsync(cancellationToken);
            return SettingResolver.Resolve(rows, academicYearId)?.Value;
        }

        public async Task<IReadOnlyList<SchoolSetting>> ListSettingsAsync(CancellationToken cancellationToken = default) =>
            await _db.SchoolSettings.AsNoTracking().OrderBy(s => s.Key).ThenBy(s => s.AcademicYearId).ToListAsync(cancellationToken);

        // ------------------------------------------------------------------
        // Feature toggles
        // ------------------------------------------------------------------

        public async Task SetFeatureAsync(string featureCode, bool enabled, CancellationToken cancellationToken = default)
        {
            if (!FeatureCatalog.TryGet(featureCode, out var feature))
            {
                throw new UnknownFeatureException(featureCode);
            }

            var states = await GetFeatureStatesAsync(cancellationToken);
            var blockers = FeatureDependencyEvaluator.Blockers(feature.Code, enabled, states);
            if (blockers.Count > 0)
            {
                throw new FeatureDependencyException(feature.Code, enabled, blockers);
            }

            var row = await _db.FeatureToggles.SingleOrDefaultAsync(t => t.FeatureCode == feature.Code, cancellationToken);
            if (row == null)
            {
                row = new FeatureToggle { FeatureCode = feature.Code };
                _db.FeatureToggles.Add(row);
            }

            row.IsEnabled = enabled;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyDictionary<string, bool>> GetFeatureStatesAsync(CancellationToken cancellationToken = default)
        {
            var rows = await _db.FeatureToggles.AsNoTracking().ToListAsync(cancellationToken);
            var explicitStates = rows.ToDictionary(r => r.FeatureCode, r => r.IsEnabled, StringComparer.OrdinalIgnoreCase);
            return FeatureCatalog.Features.ToDictionary(
                f => f.Code,
                f => FeatureDependencyEvaluator.IsOn(f.Code, explicitStates),
                StringComparer.OrdinalIgnoreCase);
        }

        public async Task<bool> IsEnabledAsync(string featureCode, CancellationToken cancellationToken = default)
        {
            if (!FeatureCatalog.TryGet(featureCode, out var feature))
            {
                return true; // unknown code = not toggleable = always on
            }

            var row = await _db.FeatureToggles.AsNoTracking().SingleOrDefaultAsync(t => t.FeatureCode == feature.Code, cancellationToken);
            return row?.IsEnabled ?? feature.DefaultEnabled;
        }

        // ------------------------------------------------------------------
        // Setup wizard
        // ------------------------------------------------------------------

        public async Task<IReadOnlyList<StepState>> GetChecklistAsync(CancellationToken cancellationToken = default) =>
            SetupWizardEvaluator.Evaluate(await BuildSnapshotAsync(cancellationToken));

        public async Task CompleteStepAsync(string stepCode, string? notes = null, CancellationToken cancellationToken = default)
        {
            if (!SetupWizardSteps.TryGet(stepCode, out var step))
            {
                throw new UnknownSetupStepException(stepCode);
            }

            var snapshot = await BuildSnapshotAsync(cancellationToken);
            if (!SetupWizardEvaluator.IsReady(step.Code, snapshot))
            {
                throw new SetupStepNotReadyException(step.Code);
            }

            var row = await _db.SetupChecklists.SingleOrDefaultAsync(c => c.StepCode == step.Code, cancellationToken);
            if (row == null)
            {
                row = new SetupChecklist { StepCode = step.Code };
                _db.SetupChecklists.Add(row);
            }

            row.Status = SetupStepStatus.Completed;
            row.CompletedAtUtc = _clock.UtcNow;
            row.CompletedByUserId = _currentUser.UserId;
            row.Notes = notes;
            await _db.SaveChangesAsync(cancellationToken);

            await PublishToCurrentUserAsync(SetupStepCompletedEvent, new Dictionary<string, string> { ["step"] = step.Code }, cancellationToken);
        }

        public async Task DeclareSetupCompleteAsync(CancellationToken cancellationToken = default)
        {
            var school = await RequireSchoolAsync(cancellationToken);
            var states = SetupWizardEvaluator.Evaluate(await BuildSnapshotAsync(cancellationToken));
            if (!SetupWizardEvaluator.CanDeclareComplete(states))
            {
                throw new SetupIncompleteException(states.Where(s => s.Step.IsMandatory && s.Status != SetupStepStatus.Completed).Select(s => s.Step.Code).ToList());
            }

            if (school.SetupCompletedAtUtc == null)
            {
                school.SetupCompletedAtUtc = _clock.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId, cancellationToken);
            return school?.SetupCompletedAtUtc != null;
        }

        // ------------------------------------------------------------------

        private async Task<SetupSnapshot> BuildSnapshotAsync(CancellationToken cancellationToken)
        {
            var school = await _db.Schools.AsNoTracking().SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId, cancellationToken);
            var settings = await _db.SchoolSettings.AsNoTracking().Where(s => s.AcademicYearId == null).ToListAsync(cancellationToken);
            string? Setting(string key) => settings.FirstOrDefault(s => s.Key == key)?.Value;

            var currencyValid = school != null && await _db.LookupValues.AsNoTracking()
                .Join(_db.LookupCategories.AsNoTracking(), v => v.LookupCategoryId, c => c.Id, (v, c) => new { v, c })
                .AnyAsync(x => x.c.Code == CurrencyLookupCategory && x.v.Code == school.CurrencyCode, cancellationToken);

            return new SetupSnapshot
            {
                SchoolExists = school != null,
                ProfileComplete = school != null
                    && !string.IsNullOrWhiteSpace(school.NameAr) && !string.IsNullOrWhiteSpace(school.NameEn)
                    && !string.IsNullOrWhiteSpace(school.LicenseNumber) && !string.IsNullOrWhiteSpace(school.MinistryCode),
                CountryPackBound = school?.CountryPackId != null,
                CurrencyValid = currencyValid,
                TimeZoneValid = school != null && IsKnownTimeZone(school.TimeZoneId),
                WorkingWeekDefined = Setting(SettingKeys.WorkingDays) is string wd && WorkingWeek.Validate(wd) == null,
                LanguagesDefined = Setting(SettingKeys.EnabledLanguages) != null && Setting(SettingKeys.DefaultLanguage) != null,
                CalendarTypeDefined = Setting(SettingKeys.CalendarType) != null,
                NumberingSeriesDefined = await _db.NumberingSeries.AsNoTracking().AnyAsync(cancellationToken),
                StageStructureDefined = await _db.Stages.AsNoTracking().AnyAsync(cancellationToken) && await _db.GradeLevels.AsNoTracking().AnyAsync(cancellationToken),
                Checklist = await _db.SetupChecklists.AsNoTracking().ToListAsync(cancellationToken),
            };
        }

        private static bool IsKnownTimeZone(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return false;
            }

            try
            {
                TimeZoneInfo.FindSystemTimeZoneById(id);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        private async Task<School> RequireSchoolAsync(CancellationToken cancellationToken) =>
            await _db.Schools.SingleOrDefaultAsync(s => s.Id == _tenant.SchoolId, cancellationToken)
            ?? throw new InvalidOperationException($"No School row for tenant {_tenant.SchoolId}; define the school profile first (BR-SET-003 step PROFILE).");

        private async Task SeedDefaultIfMissingAsync(string key, string value, SettingValueType type, CancellationToken cancellationToken)
        {
            var exists = await _db.SchoolSettings.AnyAsync(s => s.Key == key && s.AcademicYearId == null, cancellationToken)
                || _db.SchoolSettings.Local.Any(s => s.Key == key && s.AcademicYearId == null);
            if (!exists)
            {
                _db.SchoolSettings.Add(new SchoolSetting { Key = key, Value = value, ValueType = type });
            }
        }

        private async Task PublishToCurrentUserAsync(string eventCode, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
        {
            if (_currentUser.UserId <= 0)
            {
                return; // system actor (seeding, jobs) — nobody to notify
            }

            var language = await GetSettingAsync(SettingKeys.DefaultLanguage, null, cancellationToken) ?? "ar";
            await _notifications.PublishAsync(eventCode, new[] { new NotificationRecipient(_currentUser.UserId, language) }, payload, cancellationToken);
        }
    }
}
