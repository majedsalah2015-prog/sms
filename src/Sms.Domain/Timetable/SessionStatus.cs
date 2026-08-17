namespace Sms.Domain.Timetable
{
    public enum SessionStatus : short
    {
        Held = 1,
        Substituted = 2,
        RoomChanged = 3,
        Cancelled = 4,
    }
}
