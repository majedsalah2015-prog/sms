namespace Sms.Domain.Transport
{
    /// <summary>BR-TRN-001 bus class; drives the driver licence class the bus needs (BR-TRN-002 "class-validated").</summary>
    public enum BusType : short
    {
        Minibus = 1,
        Standard = 2,
        Large = 3,
    }

    /// <summary>Ordered licence classes — a driver may drive any bus whose required class is ≤ theirs.</summary>
    public enum LicenseClass : short
    {
        Light = 1,
        Medium = 2,
        Heavy = 3,
    }

    /// <summary>BR-TRN-001 mandatory expiry-tracked bus documents.</summary>
    public enum BusDocumentKind : short
    {
        Registration = 1,
        Insurance = 2,
        SafetyInspection = 3,
    }

    public enum TransportStaffKind : short
    {
        Driver = 1,
        Attendant = 2,
    }

    /// <summary>BR-TRN-003 route direction: AM pickup / PM drop.</summary>
    public enum RouteDirection : short
    {
        Am = 1,
        Pm = 2,
    }

    /// <summary>BR-TRN-004/008 subscription lifecycle.</summary>
    public enum TransportSubscriptionStatus : short
    {
        Active = 1,
        Ended = 2,
        Suspended = 3,
        Waitlisted = 4,
    }

    /// <summary>BR-TRN-005: unclosed trips escalate (BR-ATD-007 pattern).</summary>
    public enum TripStatus : short
    {
        InProgress = 1,
        Closed = 2,
        Escalated = 3,
    }

    /// <summary>BR-TRN-005 per-student trip events; every roster student must end resolved (alighted / absent-declared / escalated) before close.</summary>
    public enum TripLogEvent : short
    {
        Boarded = 1,
        Alighted = 2,
        AbsentDeclared = 3,
        NotBoarded = 4,
        NotCollected = 5,
    }

    /// <summary>BR-TRN-005/006 safety events — T1.</summary>
    public enum SafetyEventKind : short
    {
        NotBoardedAm = 1,
        NotCollectedPm = 2,
        UnclosedTrip = 3,
        UnauthorizedHandover = 4,
        UnroadworthyOverride = 5,
    }

    public enum SafetyEventState : short
    {
        Open = 1,
        Escalated = 2,
        Resolved = 3,
    }
}
