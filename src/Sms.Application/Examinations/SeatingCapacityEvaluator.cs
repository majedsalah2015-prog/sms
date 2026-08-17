namespace Sms.Application.Examinations
{
    /// <summary>Pure BR-EXM-004: a sitting's allocated student count must not exceed the room's exam (spaced-seating) capacity, BR-ROM-002.</summary>
    public static class SeatingCapacityEvaluator
    {
        public static bool HasCapacity(int currentlyAllocated, int examCapacity) => currentlyAllocated < examCapacity;
    }
}
