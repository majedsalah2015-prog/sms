namespace Sms.Domain.Library
{
    /// <summary>BR-LIB-001 copy status.</summary>
    public enum CopyStatus : short
    {
        Available = 1,
        Loaned = 2,
        Reserved = 3,
        Repair = 4,
        Lost = 5,
        Withdrawn = 6,
    }

    /// <summary>BR-LIB-002: members resolve to Student/Employee refs directly — no separate registry.</summary>
    public enum MemberKind : short
    {
        Student = 1,
        Employee = 2,
    }

    /// <summary>BR-LIB-004 reservation queue states (BR-ADM-006 offer pattern).</summary>
    public enum ReservationStatus : short
    {
        Queued = 1,
        Offered = 2,
        Fulfilled = 3,
        Expired = 4,
        Cancelled = 5,
    }

    /// <summary>BR-LIB-005/006: librarian proposes, finance-visible confirm posts the misc charge.</summary>
    public enum FineProposalStatus : short
    {
        Proposed = 1,
        Confirmed = 2,
        Waived = 3,
    }

    public enum FineKind : short
    {
        Overdue = 1,
        Replacement = 2,
    }

    /// <summary>BR-LIB-003: every circulation event logged.</summary>
    public enum CirculationEventKind : short
    {
        Checkout = 1,
        Renewal = 2,
        Return = 3,
        DeclaredLost = 4,
        Found = 5,
        OverrideCheckout = 6,
    }

    public enum StocktakeStatus : short
    {
        Open = 1,
        Closed = 2,
    }

    public enum StocktakeFinding : short
    {
        Ok = 1,
        Missing = 2,
        Misplaced = 3,
    }
}
