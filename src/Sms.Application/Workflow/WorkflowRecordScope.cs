namespace Sms.Application.Workflow
{
    /// <summary>
    /// The record's scope coordinates for BR-WF-004: the approver's data scopes
    /// must cover these. School/year default from the instance; grade/section
    /// are supplied by the module when the record has them.
    /// </summary>
    public sealed record WorkflowRecordScope(int SchoolId, int? AcademicYearId = null, int? GradeId = null, int? SectionId = null);
}
