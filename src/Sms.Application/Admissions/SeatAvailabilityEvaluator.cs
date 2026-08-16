namespace Sms.Application.Admissions
{
    /// <summary>Pure BR-ADM-004: full grades accept to the waiting list only.</summary>
    public static class SeatAvailabilityEvaluator
    {
        public static int RemainingSeats(int plannedSeats, int activeEnrollmentCount)
            => plannedSeats - activeEnrollmentCount;

        public static bool HasSeat(int plannedSeats, int activeEnrollmentCount)
            => RemainingSeats(plannedSeats, activeEnrollmentCount) > 0;
    }
}
