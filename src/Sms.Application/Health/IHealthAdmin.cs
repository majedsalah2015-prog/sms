using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Health;

namespace Sms.Application.Health
{
    /// <summary>BR-HLT-002: the denormalized banner subset — what a teacher/supervisor sees at fixed display points without opening the file.</summary>
    public sealed record EmergencyBanner(int StudentId, string? BannerAr, string? BannerEn, IReadOnlyList<string> SevereAllergies, IReadOnlyList<string> CriticalConditions);

    /// <summary>
    /// doc/Modules/24 §8 Medical file / Clinic desk / Medication log /
    /// Vaccination & screening campaigns / Exposure notices screens
    /// backing (screens deferred, operations are core). Every full-file
    /// read goes through <see cref="OpenMedicalFileAsync"/> so it is
    /// T0-audited (AuditAction.View); the banner read is not.
    /// </summary>
    public interface IHealthAdmin
    {
        /// <summary>BR-HLT-001/003: creates or returns the student's file (parent-declared intake; nurse verifies via <see cref="VerifyIntakeAsync"/>).</summary>
        Task<MedicalFile> EnsureMedicalFileAsync(int studentId, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-001: T0 read-audited full-file open — logs AuditAction.View for the current user.</summary>
        Task<MedicalFile> OpenMedicalFileAsync(int studentId, CancellationToken cancellationToken = default);

        Task VerifyIntakeAsync(int studentId, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-003: annual parent re-confirmation stamp for the working year.</summary>
        Task ReconfirmAsync(int studentId, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-003: students whose file has not been re-confirmed for the working year (the re-registration nag list).</summary>
        Task<IReadOnlyList<int>> StaleReconfirmationsAsync(CancellationToken cancellationToken = default);

        Task<Allergy> AddAllergyAsync(int studentId, string substance, AllergySeverity severity, string? notes = null, CancellationToken cancellationToken = default);

        Task<MedicalCondition> AddConditionAsync(int studentId, string name, bool isCritical, string? notes = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-002: nurse-curated banner text (never auto-extracted).</summary>
        Task SetEmergencyBannerAsync(int studentId, string? bannerAr, string? bannerEn, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-002: banner subset without opening the file — NOT read-audited by design (fixed display points).</summary>
        Task<EmergencyBanner?> GetEmergencyBannerAsync(int studentId, CancellationToken cancellationToken = default);

        Task<CarePlan> DefineCarePlanAsync(int studentId, string conditionName, string triggers, string responseSteps, DateTime reviewDueDate, string? emergencyContactsNote = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-007: annual review flag — plans whose review is due on/before <paramref name="asOf"/>.</summary>
        Task<IReadOnlyList<CarePlan>> CarePlansDueForReviewAsync(DateTime asOf, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-HLT-005: numbered visit. SentHome needs a verified pickup-authorized person (BR-PAR-008) or a documented exception
        /// (<see cref="Common.Exceptions.SentHomeWithoutVerifiedPickupException"/>) and notifies the parent (ClinicStudentSentHome);
        /// Emergency publishes the urgent protocol notification (SchoolEmergencyProtocol).
        /// </summary>
        Task<ClinicVisit> RecordVisitAsync(
            int studentId, int nurseUserId, string reason, ClinicVisitOutcome outcome, string? triageNotes = null,
            decimal? temperatureC = null, int? pulseBpm = null, string? bloodPressure = null,
            string? pickupByName = null, string? pickupExceptionNote = null, CancellationToken cancellationToken = default);

        Task<MedicationAuthorization> AuthorizeMedicationAsync(
            int studentId, string medicationName, decimal dosePerAdministration, string doseUnit, string scheduleTimes, DateTime startDate, DateTime endDate,
            int authorizedByParentId, bool isControlled = false, int? physicianNoteAttachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-006: logs against the authorization; outside window/schedule/dosage = deviation → reason mandatory (<see cref="Common.Exceptions.MedicationDeviationReasonRequiredException"/>); parent notified (MedicationAdministered).</summary>
        Task<AdministrationLog> LogAdministrationAsync(int medicationAuthorizationId, int nurseUserId, decimal doseGiven, AdministrationStatus status, string? deviationReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-006: the controlled-storage list — active controlled authorizations as of now.</summary>
        Task<IReadOnlyList<MedicationAuthorization>> ControlledStorageListAsync(CancellationToken cancellationToken = default);

        Task DefineVaccinationScheduleAsync(IReadOnlyList<(string VaccineCode, int DoseNumber, int DueAgeMonths)> entries, CancellationToken cancellationToken = default);

        Task<VaccinationRecord> RecordExternalVaccinationAsync(int studentId, string vaccineCode, int doseNumber, DateTime givenOn, int? cardAttachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-004: due/overdue per the pack schedule and the student's age.</summary>
        Task<IReadOnlyList<VaccinationDueEvaluator.DoseStatus>> VaccinationStatusAsync(int studentId, CancellationToken cancellationToken = default);

        Task<VaccinationCampaign> DefineVaccinationCampaignAsync(string nameAr, string nameEn, string vaccineCode, int doseNumber, DateTime scheduledDate, CancellationToken cancellationToken = default);

        Task<ConsentRecord> RecordConsentAsync(int campaignId, int studentId, int parentId, bool isGranted, int? attachmentId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-004 / doc §9: campaign execution only for consented students — hard (<see cref="Common.Exceptions.VaccinationConsentMissingException"/>).</summary>
        Task<VaccinationRecord> AdministerCampaignDoseAsync(int campaignId, int studentId, DateTime givenOn, CancellationToken cancellationToken = default);

        Task<ScreeningCampaign> DefineScreeningCampaignAsync(ScreeningType type, DateTime date, int? gradeYearProfileId = null, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-008: abnormal results get a referral stamp (letter itself = Module 18 pattern, deferred) and enter the follow-up tracker.</summary>
        Task<ScreeningResult> RecordScreeningResultAsync(int campaignId, int studentId, bool isAbnormal, decimal? value1 = null, decimal? value2 = null, string? notes = null, CancellationToken cancellationToken = default);

        Task CompleteFollowUpAsync(int screeningResultId, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-008: anonymized aggregate — counts only.</summary>
        Task<ScreeningStatsCalculator.Stats> ScreeningStatsAsync(int campaignId, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-009: records the case; when <paramref name="preApproveAbsence"/> pre-captures MedicalLeave attendance for each working day of the window through E-301 (skips days without section membership or already captured).</summary>
        Task<InfectiousCase> RecordInfectiousCaseAsync(int studentId, string diseaseName, DateTime absenceFrom, DateTime absenceTo, bool preApproveAbsence, ISet<DayOfWeek> weekendDays, int recordedByUserId, CancellationToken cancellationToken = default);

        Task<ExposureNotice> DraftExposureNoticeAsync(int sectionId, string diseaseName, DateTime exposureFrom, DateTime exposureTo, CancellationToken cancellationToken = default);

        /// <summary>BR-HLT-009: Principal approval then send — anonymized (no student in the payload) to every parent of the section's current members (HealthExposureNotice).</summary>
        Task ApproveAndSendExposureNoticeAsync(int exposureNoticeId, int approvedByUserId, CancellationToken cancellationToken = default);
    }
}
