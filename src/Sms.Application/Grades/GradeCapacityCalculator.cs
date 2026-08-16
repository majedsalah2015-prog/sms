namespace Sms.Application.Grades
{
    /// <summary>Pure BR-GRD-006: planned seats = target sections × target section size.</summary>
    public static class GradeCapacityCalculator
    {
        public static int PlannedSeats(int targetSections, int targetSectionSize)
            => targetSections * targetSectionSize;
    }
}
