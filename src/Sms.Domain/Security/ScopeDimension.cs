namespace Sms.Domain.Security
{
    /// <summary>Scope dimensions of doc 06 §4.2; compound per BR-GLB-071.</summary>
    public enum ScopeDimension : short
    {
        School = 1,
        AcademicYear = 2,
        Grade = 3,
        Section = 4,
        OwnRecordsOnly = 5,
    }
}
