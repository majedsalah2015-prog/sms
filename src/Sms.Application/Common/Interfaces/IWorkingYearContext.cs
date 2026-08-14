namespace Sms.Application.Common.Interfaces
{
    /// <summary>
    /// The user's working academic year (doc 02 §5): switchable in the UI shell,
    /// displayed on every screen, scopes daily work (BR-GLB-020/021).
    /// </summary>
    public interface IWorkingYearContext
    {
        int AcademicYearId { get; }
    }
}
