namespace Sms.Domain.Attendance
{
    /// <summary>BR-ATD-002 product-fixed taxonomy core. Absences start Unexcused until a justification is accepted (BR-ATD-005).</summary>
    public enum AttendanceStatus : short
    {
        Present = 1,
        Late = 2,
        AbsentExcused = 3,
        AbsentUnexcused = 4,
        MedicalLeave = 5,
        Permission = 6,
        EarlyLeave = 7,
        Exempted = 8,
    }
}
