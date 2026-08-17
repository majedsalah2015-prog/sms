using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Health
{
    /// <summary>svc.ScreeningCampaign (BR-HLT-008): per grade-year profile (null = whole school), one screening type.</summary>
    [Audited(AuditTier.T2)]
    public class ScreeningCampaign : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public ScreeningType Type { get; set; }

        public int? GradeYearProfileId { get; set; }

        public DateTime Date { get; set; }
    }

    /// <summary>svc.ScreeningResult (BR-HLT-008): structured fields per type (Value1/Value2 carry e.g. left/right acuity, height/weight); abnormal → referral + follow-up tracker.</summary>
    [Audited(AuditTier.T1)]
    public class ScreeningResult : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int ScreeningCampaignId { get; set; }

        public int StudentId { get; set; }

        public decimal? Value1 { get; set; }

        public decimal? Value2 { get; set; }

        public string? Notes { get; set; }

        public bool IsAbnormal { get; set; }

        public DateTime? ReferralIssuedAtUtc { get; set; }

        public DateTime? FollowUpCompletedAtUtc { get; set; }
    }

    /// <summary>BR-HLT-009: an infectious-disease case with its expected-absence window (fed to Module 14 as pre-approved medical leave).</summary>
    [Audited(AuditTier.T1)]
    public class InfectiousCase : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public int StudentId { get; set; }

        public string DiseaseName { get; set; } = string.Empty;

        public DateTime AbsenceFrom { get; set; }

        public DateTime AbsenceTo { get; set; }
    }

    /// <summary>svc.ExposureNotice (BR-HLT-009): anonymized notice to a section's parents, Principal-approved before send.</summary>
    [Audited(AuditTier.T1)]
    public class ExposureNotice : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int SectionId { get; set; }

        public string DiseaseName { get; set; } = string.Empty;

        public DateTime ExposureFrom { get; set; }

        public DateTime ExposureTo { get; set; }

        public ExposureNoticeStatus Status { get; set; } = ExposureNoticeStatus.Draft;

        public int? ApprovedByUserId { get; set; }

        public DateTime? SentAtUtc { get; set; }
    }
}
