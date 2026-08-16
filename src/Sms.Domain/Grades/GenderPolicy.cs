namespace Sms.Domain.Grades
{
    /// <summary>BR-GRD-004: a stage's policy narrows at the grade (and later section) level, never widens.</summary>
    public enum GenderPolicy : short
    {
        Mixed = 1,
        Boys = 2,
        Girls = 3,
    }
}
