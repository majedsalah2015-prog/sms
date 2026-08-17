namespace Sms.Domain.Reports
{
    /// <summary>BR-RPT-001/003.</summary>
    public enum ReportSensitivity : short
    {
        Normal = 1,
        PersonalData = 2,
        Restricted = 3,
    }
}
