namespace Sms.Application.Classrooms
{
    /// <summary>Pure BR-ROM-002: exam capacity (spaced seating) never exceeds standard (teaching) capacity.</summary>
    public static class RoomCapacityValidator
    {
        public static bool IsValidCapacity(int standardCapacity, int examCapacity)
            => examCapacity <= standardCapacity;
    }
}
