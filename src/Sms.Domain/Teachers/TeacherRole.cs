namespace Sms.Domain.Teachers
{
    /// <summary>BR-TCH-005: one primary per offering×section, optional co-teachers.</summary>
    public enum TeacherRole : short
    {
        Primary = 1,
        CoTeacher = 2,
    }
}
