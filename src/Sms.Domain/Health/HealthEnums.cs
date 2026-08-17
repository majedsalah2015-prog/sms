namespace Sms.Domain.Health
{
    /// <summary>BR-HLT-002 / doc §9: allergy severity is mandatory.</summary>
    public enum AllergySeverity : short
    {
        Mild = 1,
        Moderate = 2,
        Severe = 3,
    }

    /// <summary>BR-HLT-005 visit outcomes.</summary>
    public enum ClinicVisitOutcome : short
    {
        ReturnedToClass = 1,
        SentHome = 2,
        Referred = 3,
        Emergency = 4,
    }

    /// <summary>BR-HLT-004 record provenance.</summary>
    public enum VaccinationSource : short
    {
        SchoolAdministered = 1,
        External = 2,
    }

    /// <summary>BR-HLT-006 administration events — missed/refused doses are recorded too.</summary>
    public enum AdministrationStatus : short
    {
        Given = 1,
        Missed = 2,
        Refused = 3,
    }

    /// <summary>BR-HLT-008 screening kinds (BMI/growth per doc Q2 — proposed yes, parent-visible only).</summary>
    public enum ScreeningType : short
    {
        Vision = 1,
        Hearing = 2,
        Dental = 3,
        Growth = 4,
    }

    /// <summary>BR-HLT-009 exposure notice: Principal-approved send.</summary>
    public enum ExposureNoticeStatus : short
    {
        Draft = 1,
        Approved = 2,
        Sent = 3,
    }
}
