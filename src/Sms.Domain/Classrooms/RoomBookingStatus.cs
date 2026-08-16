namespace Sms.Domain.Classrooms
{
    /// <summary>doc/Modules/08 §4: free-slot bookings go direct to Approved; a booking displacing teaching sessions needs P2 (VP) — that approval workflow isn't wired yet (see RoomBookingAdmin doc comment).</summary>
    public enum RoomBookingStatus : short
    {
        Requested = 1,
        Approved = 2,
        Rejected = 3,
    }
}
