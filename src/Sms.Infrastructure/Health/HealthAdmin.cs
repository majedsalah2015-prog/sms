using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attendance;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Health;
using Sms.Application.Notifications;
using Sms.Application.Numbering;
using Sms.Domain.Attendance;
using Sms.Domain.Audit;
using Sms.Domain.Calendar;
using Sms.Domain.Health;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Health
{
    /// <summary>Standalone — saves itself. The nurse's identity for T0/T1 attribution is the ambient ICurrentUser (event writer) plus explicit NurseUserId on visit/administration rows.</summary>
    public class HealthAdmin : IHealthAdmin
    {
        public const string VisitSeriesCode = "MED";
        public const string SentHomeEventCode = "ClinicStudentSentHome";
        public const string EmergencyEventCode = "SchoolEmergencyProtocol";
        public const string MedicationEventCode = "MedicationAdministered";
        public const string ExposureEventCode = "HealthExposureNotice";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;
        private readonly IAuditEventWriter _auditEvents;
        private readonly INotificationPublisher _notifications;
        private readonly IAttendanceAdmin _attendance;

        public HealthAdmin(
            AppDbContext db, INumberIssuer numberIssuer, IClock clock, IWorkingYearContext workingYear,
            IAuditEventWriter auditEvents, INotificationPublisher notifications, IAttendanceAdmin attendance)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _workingYear = workingYear;
            _auditEvents = auditEvents;
            _notifications = notifications;
            _attendance = attendance;
        }

        // ------------------------------------------------------------------ file

        public async Task<MedicalFile> EnsureMedicalFileAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var file = await _db.MedicalFiles.SingleOrDefaultAsync(f => f.StudentId == studentId, cancellationToken);
            if (file != null)
            {
                return file;
            }

            file = new MedicalFile { StudentId = studentId };
            _db.MedicalFiles.Add(file);
            await _db.SaveChangesAsync(cancellationToken);
            return file;
        }

        public async Task<MedicalFile> OpenMedicalFileAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var file = await _db.MedicalFiles.Include(f => f.Allergies).Include(f => f.Conditions).SingleAsync(f => f.StudentId == studentId, cancellationToken);
            // BR-HLT-001: T0 read audit — every full-file open is an event, atomic with this unit of work.
            _auditEvents.Log(AuditAction.View, nameof(MedicalFile), file.Id, businessKey: studentId.ToString(CultureInfo.InvariantCulture));
            await _db.SaveChangesAsync(cancellationToken);
            return file;
        }

        public async Task VerifyIntakeAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            file.IntakeVerifiedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReconfirmAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            file.LastReconfirmedAcademicYearId = _workingYear.AcademicYearId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<int>> StaleReconfirmationsAsync(CancellationToken cancellationToken = default)
        {
            var yearId = _workingYear.AcademicYearId;
            return await _db.MedicalFiles.Where(f => f.LastReconfirmedAcademicYearId != yearId).Select(f => f.StudentId).OrderBy(id => id).ToListAsync(cancellationToken);
        }

        public async Task<Allergy> AddAllergyAsync(int studentId, string substance, AllergySeverity severity, string? notes = null, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var allergy = new Allergy { MedicalFileId = file.Id, Substance = substance, Severity = severity, Notes = notes };
            _db.Allergies.Add(allergy);
            await _db.SaveChangesAsync(cancellationToken);
            return allergy;
        }

        public async Task<MedicalCondition> AddConditionAsync(int studentId, string name, bool isCritical, string? notes = null, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var condition = new MedicalCondition { MedicalFileId = file.Id, Name = name, IsCritical = isCritical, Notes = notes };
            _db.MedicalConditions.Add(condition);
            await _db.SaveChangesAsync(cancellationToken);
            return condition;
        }

        public async Task SetEmergencyBannerAsync(int studentId, string? bannerAr, string? bannerEn, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            file.EmergencyBannerAr = bannerAr;
            file.EmergencyBannerEn = bannerEn;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<EmergencyBanner?> GetEmergencyBannerAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var file = await _db.MedicalFiles.SingleOrDefaultAsync(f => f.StudentId == studentId, cancellationToken);
            if (file == null)
            {
                return null;
            }

            var severe = await _db.Allergies.Where(a => a.MedicalFileId == file.Id && a.Severity == AllergySeverity.Severe).Select(a => a.Substance).ToListAsync(cancellationToken);
            var critical = await _db.MedicalConditions.Where(c => c.MedicalFileId == file.Id && c.IsCritical).Select(c => c.Name).ToListAsync(cancellationToken);
            return new EmergencyBanner(studentId, file.EmergencyBannerAr, file.EmergencyBannerEn, severe, critical);
        }

        public async Task<CarePlan> DefineCarePlanAsync(int studentId, string conditionName, string triggers, string responseSteps, DateTime reviewDueDate, string? emergencyContactsNote = null, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var plan = new CarePlan { MedicalFileId = file.Id, ConditionName = conditionName, Triggers = triggers, ResponseSteps = responseSteps, ReviewDueDate = reviewDueDate.Date, EmergencyContactsNote = emergencyContactsNote };
            _db.CarePlans.Add(plan);
            await _db.SaveChangesAsync(cancellationToken);
            return plan;
        }

        public async Task<IReadOnlyList<CarePlan>> CarePlansDueForReviewAsync(DateTime asOf, CancellationToken cancellationToken = default)
            => await _db.CarePlans.Where(p => p.ReviewDueDate <= asOf.Date).OrderBy(p => p.ReviewDueDate).ToListAsync(cancellationToken);

        // ------------------------------------------------------------------ visits

        public async Task<ClinicVisit> RecordVisitAsync(
            int studentId, int nurseUserId, string reason, ClinicVisitOutcome outcome, string? triageNotes = null,
            decimal? temperatureC = null, int? pulseBpm = null, string? bloodPressure = null,
            string? pickupByName = null, string? pickupExceptionNote = null, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            string? verifiedBy = null;
            if (outcome == ClinicVisitOutcome.SentHome)
            {
                var verified = !string.IsNullOrWhiteSpace(pickupByName) && await IsPickupAuthorizedAsync(studentId, pickupByName!, cancellationToken);
                if (!SentHomePolicy.IsAcceptable(verified, pickupExceptionNote))
                {
                    throw new SentHomeWithoutVerifiedPickupException(studentId);
                }

                verifiedBy = verified ? pickupByName : null;
            }

            var visit = new ClinicVisit
            {
                MedicalFileId = file.Id, StudentId = studentId, VisitNo = await _numberIssuer.IssueAsync(VisitSeriesCode, cancellationToken), NurseUserId = nurseUserId,
                ArrivedAtUtc = _clock.UtcNow, Reason = reason, TriageNotes = triageNotes, TemperatureC = temperatureC, PulseBpm = pulseBpm, BloodPressure = bloodPressure,
                Outcome = outcome, PickupVerifiedByName = verifiedBy, PickupExceptionNote = outcome == ClinicVisitOutcome.SentHome && verifiedBy == null ? pickupExceptionNote : null,
            };
            _db.ClinicVisits.Add(visit);
            await _db.SaveChangesAsync(cancellationToken);

            var payload = new Dictionary<string, string> { ["VisitNo"] = visit.VisitNo, ["Outcome"] = outcome.ToString() };
            if (outcome == ClinicVisitOutcome.SentHome)
            {
                await NotifyGuardiansAsync(studentId, SentHomeEventCode, payload, cancellationToken);
            }
            else if (outcome == ClinicVisitOutcome.Emergency)
            {
                payload["Urgent"] = "true";
                await NotifyGuardiansAsync(studentId, EmergencyEventCode, payload, cancellationToken);
            }

            // BR-HLT-005 "visit during a period auto-notifies the session teacher": needs the live timetable session for the
            // student's section at ArrivedAtUtc (E-401 Sessions exist, but a school-local time -> period-slot resolver doesn't) - deferred.
            return visit;
        }

        private async Task<bool> IsPickupAuthorizedAsync(int studentId, string name, CancellationToken cancellationToken)
        {
            var guardianIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.IsPickupAuthorized && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            if (await _db.Parents.AnyAsync(p => guardianIds.Contains(p.Id) && (p.NameAr == name || p.NameEn == name), cancellationToken))
            {
                return true;
            }

            return await _db.EmergencyContacts.AnyAsync(c => c.StudentId == studentId && c.IsPickupAuthorized && (c.NameAr == name || c.NameEn == name), cancellationToken);
        }

        // ------------------------------------------------------------------ medication

        public async Task<MedicationAuthorization> AuthorizeMedicationAsync(
            int studentId, string medicationName, decimal dosePerAdministration, string doseUnit, string scheduleTimes, DateTime startDate, DateTime endDate,
            int authorizedByParentId, bool isControlled = false, int? physicianNoteAttachmentId = null, CancellationToken cancellationToken = default)
        {
            MedicationAdministrationPolicy.ParseScheduleTimes(scheduleTimes); // validates format early
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var authorization = new MedicationAuthorization
            {
                MedicalFileId = file.Id, MedicationName = medicationName, DosePerAdministration = dosePerAdministration, DoseUnit = doseUnit, ScheduleTimes = scheduleTimes,
                StartDate = startDate.Date, EndDate = endDate.Date, AuthorizedByParentId = authorizedByParentId, IsControlled = isControlled, PhysicianNoteAttachmentId = physicianNoteAttachmentId,
            };
            _db.MedicationAuthorizations.Add(authorization);
            await _db.SaveChangesAsync(cancellationToken);
            return authorization;
        }

        public async Task<AdministrationLog> LogAdministrationAsync(int medicationAuthorizationId, int nurseUserId, decimal doseGiven, AdministrationStatus status, string? deviationReason = null, CancellationToken cancellationToken = default)
        {
            var authorization = await _db.MedicationAuthorizations.SingleAsync(a => a.Id == medicationAuthorizationId, cancellationToken);
            var now = _clock.UtcNow;
            // Missed/refused doses aren't dosage deviations - the recorded dose is 0 by definition; only Given doses are checked against the authorization.
            var isDeviation = status == AdministrationStatus.Given
                ? MedicationAdministrationPolicy.IsDeviation(now, authorization.StartDate, authorization.EndDate, authorization.ScheduleTimes, doseGiven, authorization.DosePerAdministration)
                : !MedicationAdministrationPolicy.IsWithinWindow(now, authorization.StartDate, authorization.EndDate);
            if (isDeviation && string.IsNullOrWhiteSpace(deviationReason))
            {
                throw new MedicationDeviationReasonRequiredException(medicationAuthorizationId);
            }

            var log = new AdministrationLog
            {
                MedicationAuthorizationId = medicationAuthorizationId, AtUtc = now, NurseUserId = nurseUserId, DoseGiven = status == AdministrationStatus.Given ? doseGiven : 0m,
                Status = status, IsDeviation = isDeviation, DeviationReason = isDeviation ? deviationReason : null,
            };
            _db.AdministrationLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);

            var file = await _db.MedicalFiles.SingleAsync(f => f.Id == authorization.MedicalFileId, cancellationToken);
            await NotifyGuardiansAsync(file.StudentId, MedicationEventCode, new Dictionary<string, string>
            {
                ["Medication"] = authorization.MedicationName, ["Status"] = status.ToString(), ["At"] = now.ToString("O", CultureInfo.InvariantCulture),
            }, cancellationToken);
            return log;
        }

        public async Task<IReadOnlyList<MedicationAuthorization>> ControlledStorageListAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcNow.Date;
            return await _db.MedicationAuthorizations.Where(a => a.IsControlled && a.StartDate <= today && a.EndDate >= today).OrderBy(a => a.MedicationName).ToListAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ vaccinations

        public async Task DefineVaccinationScheduleAsync(IReadOnlyList<(string VaccineCode, int DoseNumber, int DueAgeMonths)> entries, CancellationToken cancellationToken = default)
        {
            foreach (var (code, dose, months) in entries)
            {
                var existing = await _db.VaccinationScheduleEntries.SingleOrDefaultAsync(e => e.VaccineCode == code && e.DoseNumber == dose, cancellationToken);
                if (existing != null)
                {
                    existing.DueAgeMonths = months;
                    continue;
                }

                _db.VaccinationScheduleEntries.Add(new VaccinationScheduleEntry { VaccineCode = code, DoseNumber = dose, DueAgeMonths = months });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<VaccinationRecord> RecordExternalVaccinationAsync(int studentId, string vaccineCode, int doseNumber, DateTime givenOn, int? cardAttachmentId = null, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var record = new VaccinationRecord { MedicalFileId = file.Id, VaccineCode = vaccineCode, DoseNumber = doseNumber, GivenOn = givenOn.Date, Source = VaccinationSource.External, ExternalCardAttachmentId = cardAttachmentId };
            _db.VaccinationRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
            return record;
        }

        public async Task<IReadOnlyList<VaccinationDueEvaluator.DoseStatus>> VaccinationStatusAsync(int studentId, CancellationToken cancellationToken = default)
        {
            var student = await _db.Students.SingleAsync(s => s.Id == studentId, cancellationToken);
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var schedule = await _db.VaccinationScheduleEntries.Select(e => new VaccinationDueEvaluator.ScheduleEntry(e.VaccineCode, e.DoseNumber, e.DueAgeMonths)).ToListAsync(cancellationToken);
            var given = await _db.VaccinationRecords.Where(r => r.MedicalFileId == file.Id).Select(r => new VaccinationDueEvaluator.GivenDose(r.VaccineCode, r.DoseNumber)).ToListAsync(cancellationToken);
            return VaccinationDueEvaluator.Evaluate(student.DateOfBirth, _clock.UtcNow, schedule, given);
        }

        public async Task<VaccinationCampaign> DefineVaccinationCampaignAsync(string nameAr, string nameEn, string vaccineCode, int doseNumber, DateTime scheduledDate, CancellationToken cancellationToken = default)
        {
            var campaign = new VaccinationCampaign { AcademicYearId = _workingYear.AcademicYearId, NameAr = nameAr, NameEn = nameEn, VaccineCode = vaccineCode, DoseNumber = doseNumber, ScheduledDate = scheduledDate.Date };
            _db.VaccinationCampaigns.Add(campaign);
            await _db.SaveChangesAsync(cancellationToken);
            return campaign;
        }

        public async Task<ConsentRecord> RecordConsentAsync(int campaignId, int studentId, int parentId, bool isGranted, int? attachmentId = null, CancellationToken cancellationToken = default)
        {
            var consent = new ConsentRecord { VaccinationCampaignId = campaignId, StudentId = studentId, ConsentedByParentId = parentId, IsGranted = isGranted, RecordedAtUtc = _clock.UtcNow, AttachmentId = attachmentId };
            _db.ConsentRecords.Add(consent);
            await _db.SaveChangesAsync(cancellationToken);
            return consent;
        }

        public async Task<VaccinationRecord> AdministerCampaignDoseAsync(int campaignId, int studentId, DateTime givenOn, CancellationToken cancellationToken = default)
        {
            var campaign = await _db.VaccinationCampaigns.SingleAsync(c => c.Id == campaignId, cancellationToken);
            var latestConsent = await _db.ConsentRecords.Where(c => c.VaccinationCampaignId == campaignId && c.StudentId == studentId).OrderByDescending(c => c.RecordedAtUtc).ThenByDescending(c => c.Id).FirstOrDefaultAsync(cancellationToken);
            if (latestConsent == null || !latestConsent.IsGranted)
            {
                throw new VaccinationConsentMissingException(campaignId, studentId);
            }

            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var record = new VaccinationRecord { MedicalFileId = file.Id, VaccineCode = campaign.VaccineCode, DoseNumber = campaign.DoseNumber, GivenOn = givenOn.Date, Source = VaccinationSource.SchoolAdministered, VaccinationCampaignId = campaignId };
            _db.VaccinationRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);
            return record;
        }

        // ------------------------------------------------------------------ screenings

        public async Task<ScreeningCampaign> DefineScreeningCampaignAsync(ScreeningType type, DateTime date, int? gradeYearProfileId = null, CancellationToken cancellationToken = default)
        {
            var campaign = new ScreeningCampaign { AcademicYearId = _workingYear.AcademicYearId, Type = type, Date = date.Date, GradeYearProfileId = gradeYearProfileId };
            _db.ScreeningCampaigns.Add(campaign);
            await _db.SaveChangesAsync(cancellationToken);
            return campaign;
        }

        public async Task<ScreeningResult> RecordScreeningResultAsync(int campaignId, int studentId, bool isAbnormal, decimal? value1 = null, decimal? value2 = null, string? notes = null, CancellationToken cancellationToken = default)
        {
            var result = new ScreeningResult
            {
                ScreeningCampaignId = campaignId, StudentId = studentId, IsAbnormal = isAbnormal, Value1 = value1, Value2 = value2, Notes = notes,
                ReferralIssuedAtUtc = isAbnormal ? _clock.UtcNow : null,
            };
            _db.ScreeningResults.Add(result);
            await _db.SaveChangesAsync(cancellationToken);
            return result;
        }

        public async Task CompleteFollowUpAsync(int screeningResultId, CancellationToken cancellationToken = default)
        {
            var result = await _db.ScreeningResults.SingleAsync(r => r.Id == screeningResultId, cancellationToken);
            result.FollowUpCompletedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ScreeningStatsCalculator.Stats> ScreeningStatsAsync(int campaignId, CancellationToken cancellationToken = default)
        {
            var rows = await _db.ScreeningResults.Where(r => r.ScreeningCampaignId == campaignId)
                .Select(r => new { r.IsAbnormal, Referred = r.ReferralIssuedAtUtc != null, FollowedUp = r.FollowUpCompletedAtUtc != null })
                .ToListAsync(cancellationToken);
            return ScreeningStatsCalculator.Compute(rows.Select(r => (r.IsAbnormal, r.Referred, r.FollowedUp)).ToList());
        }

        // ------------------------------------------------------------------ infectious disease

        public async Task<InfectiousCase> RecordInfectiousCaseAsync(int studentId, string diseaseName, DateTime absenceFrom, DateTime absenceTo, bool preApproveAbsence, ISet<DayOfWeek> weekendDays, int recordedByUserId, CancellationToken cancellationToken = default)
        {
            var file = await EnsureMedicalFileAsync(studentId, cancellationToken);
            var infectiousCase = new InfectiousCase { MedicalFileId = file.Id, StudentId = studentId, DiseaseName = diseaseName, AbsenceFrom = absenceFrom.Date, AbsenceTo = absenceTo.Date };
            _db.InfectiousCases.Add(infectiousCase);
            await _db.SaveChangesAsync(cancellationToken);

            if (!preApproveAbsence)
            {
                return infectiousCase;
            }

            // BR-HLT-009 -> Module 14: pre-approved medical leave for each working day of the window, through E-301's own capture path.
            var yearId = _workingYear.AcademicYearId;
            var enrollment = await _db.Enrollments.SingleOrDefaultAsync(e => e.StudentId == studentId && e.AcademicYearId == yearId, cancellationToken);
            if (enrollment == null)
            {
                return infectiousCase;
            }

            var overrides = await _db.CalendarDays.Where(d => d.AcademicYearId == yearId).ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);
            for (var day = absenceFrom.Date; day <= absenceTo.Date; day = day.AddDays(1))
            {
                if (CalendarDayResolver.Resolve(day, weekendDays, overrides) != DayType.Working)
                {
                    continue;
                }

                try
                {
                    await _attendance.CaptureAsync(enrollment.Id, day, AttendanceStatus.MedicalLeave, recordedByUserId, cancellationToken);
                }
                catch (DuplicateAttendanceRecordException)
                {
                    // already captured that day - the nurse's window doesn't rewrite history (BR-ATD-007 corrections need their own reason)
                }
                catch (NoSectionMembershipOnDateException)
                {
                    // not placed in a section on that date - nothing to pre-approve
                }
            }

            return infectiousCase;
        }

        public async Task<ExposureNotice> DraftExposureNoticeAsync(int sectionId, string diseaseName, DateTime exposureFrom, DateTime exposureTo, CancellationToken cancellationToken = default)
        {
            var notice = new ExposureNotice { SectionId = sectionId, DiseaseName = diseaseName, ExposureFrom = exposureFrom.Date, ExposureTo = exposureTo.Date };
            _db.ExposureNotices.Add(notice);
            await _db.SaveChangesAsync(cancellationToken);
            return notice;
        }

        public async Task ApproveAndSendExposureNoticeAsync(int exposureNoticeId, int approvedByUserId, CancellationToken cancellationToken = default)
        {
            var notice = await _db.ExposureNotices.SingleAsync(n => n.Id == exposureNoticeId, cancellationToken);
            if (notice.Status == ExposureNoticeStatus.Sent)
            {
                throw new ExposureNoticeAlreadySentException(exposureNoticeId);
            }

            notice.Status = ExposureNoticeStatus.Approved;
            notice.ApprovedByUserId = approvedByUserId;

            var enrollmentIds = await _db.SectionMemberships.Where(m => m.SectionId == notice.SectionId && m.EffectiveToUtc == null).Select(m => m.EnrollmentId).ToListAsync(cancellationToken);
            var studentIds = await _db.Enrollments.Where(e => enrollmentIds.Contains(e.Id)).Select(e => e.StudentId).ToListAsync(cancellationToken);
            var recipients = await GuardianRecipientsAsync(studentIds, cancellationToken);
            // Anonymized: disease + window only, no student in the payload (BR-HLT-009).
            await _notifications.PublishAsync(ExposureEventCode, recipients, new Dictionary<string, string>
            {
                ["Disease"] = notice.DiseaseName,
                ["From"] = notice.ExposureFrom.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["To"] = notice.ExposureTo.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            }, cancellationToken);

            notice.Status = ExposureNoticeStatus.Sent;
            notice.SentAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ recipients

        private async Task<IReadOnlyCollection<NotificationRecipient>> GuardianRecipientsAsync(IReadOnlyCollection<int> studentIds, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => studentIds.Contains(l.StudentId) && l.EffectiveToUtc == null).Select(l => l.ParentId).Distinct().ToListAsync(cancellationToken);
            var parents = await _db.Parents.Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null).Select(p => new { p.UserAccountId, p.PreferredLanguage }).ToListAsync(cancellationToken);
            return parents.Select(p => new NotificationRecipient(p.UserAccountId!.Value, p.PreferredLanguage)).ToList();
        }

        private async Task NotifyGuardiansAsync(int studentId, string eventCode, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
            => await _notifications.PublishAsync(eventCode, await GuardianRecipientsAsync(new[] { studentId }, cancellationToken), payload, cancellationToken);
    }
}
