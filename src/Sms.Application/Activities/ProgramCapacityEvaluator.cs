namespace Sms.Application.Activities
{
    /// <summary>Pure BR-ACT-002: waitlist once capacity is reached (BR-ADM-006 pattern reused).</summary>
    public static class ProgramCapacityEvaluator
    {
        public static bool HasCapacity(int currentActiveCount, int capacity) => currentActiveCount < capacity;
    }
}
