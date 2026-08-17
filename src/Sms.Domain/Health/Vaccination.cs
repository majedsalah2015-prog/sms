using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Health
{
    /// <summary>
    /// The country-pack vaccination schedule (BR-HLT-004): vaccine × dose
    /// due at an age in months. No CountryPack entity exists (E-101 never
    /// started), so the schedule is a per-school table the pack seeder
    /// would fill — same stand-in as E-403's withholding policy and
    /// E-305's KSA content pack.
    /// </summary>
    public class VaccinationScheduleEntry : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string VaccineCode { get; set; } = string.Empty;

        public int DoseNumber { get; set; }

        public int DueAgeMonths { get; set; }

        public bool IsActive { get; set; } = true;
    }

    /// <summary>svc.VaccinationRecord (BR-HLT-004): a dose given — school-administered under a consented campaign, or an external card upload.</summary>
    [Audited(AuditTier.T2)]
    public class VaccinationRecord : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int MedicalFileId { get; set; }

        public string VaccineCode { get; set; } = string.Empty;

        public int DoseNumber { get; set; }

        public DateTime GivenOn { get; set; }

        public VaccinationSource Source { get; set; }

        public int? VaccinationCampaignId { get; set; }

        public int? ExternalCardAttachmentId { get; set; }
    }

    /// <summary>BR-HLT-004 school-administered campaign (where legal — doc Q1): per-campaign parent consent is a hard gate.</summary>
    [Audited(AuditTier.T2)]
    public class VaccinationCampaign : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;

        public string VaccineCode { get; set; } = string.Empty;

        public int DoseNumber { get; set; }

        public DateTime ScheduledDate { get; set; }
    }

    /// <summary>svc.ConsentRecord (campaign × student): portal-captured parent consent; consent document via doc 10 (AttachmentId optional).</summary>
    [Audited(AuditTier.T1)]
    public class ConsentRecord : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int VaccinationCampaignId { get; set; }

        public int StudentId { get; set; }

        public int ConsentedByParentId { get; set; }

        public bool IsGranted { get; set; }

        public DateTime RecordedAtUtc { get; set; }

        public int? AttachmentId { get; set; }
    }
}
