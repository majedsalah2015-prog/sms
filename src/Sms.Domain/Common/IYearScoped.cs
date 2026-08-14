namespace Sms.Domain.Common
{
    /// <summary>
    /// Every transactional entity belongs to exactly one academic year
    /// (ADR-3, BR-GLB-020). Year is a scoping dimension, not a filter convenience.
    /// </summary>
    public interface IYearScoped
    {
        int AcademicYearId { get; set; }
    }
}
