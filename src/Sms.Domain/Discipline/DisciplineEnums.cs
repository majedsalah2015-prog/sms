namespace Sms.Domain.Discipline
{
    /// <summary>BR-DCP-004: consequences come from the catalog only. Corporal punishment is not representable — there is deliberately no such kind (product stance).</summary>
    public enum ConsequenceKind : short
    {
        VerbalWarning = 1,
        WrittenWarning = 2,
        ParentSummons = 3,
        Detention = 4,
        CommunityService = 5,
        ActivityBan = 6,
        InSchoolSuspension = 7,
        ExternalSuspension = 8,
        BehaviorContract = 9,
    }

    /// <summary>BR-DCP-003 WF-11.</summary>
    public enum CaseStatus : short
    {
        Reported = 1,
        UnderInvestigation = 2,
        Decided = 3,
        ActionApplied = 4,
        AppealWindow = 5,
        Closed = 6,
    }

    public enum StatementKind : short
    {
        Student = 1,
        Parent = 2,
        Witness = 3,
        Staff = 4,
    }

    /// <summary>BR-DCP-006 appeal outcomes.</summary>
    public enum AppealOutcome : short
    {
        Pending = 1,
        Upheld = 2,
        Modified = 3,
        Dismissed = 4,
    }

    public enum PointSource : short
    {
        Violation = 1,
        Merit = 2,
    }

    /// <summary>BR-DCP-008 portal visibility policy levels (doc Q2 proposes decisions-only as default).</summary>
    public enum PortalVisibilityLevel : short
    {
        Full = 1,
        DecisionsOnly = 2,
        SummonsOnly = 3,
    }
}
