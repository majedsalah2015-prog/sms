namespace Sms.Domain.Discounts
{
    /// <summary>BR-DIS-001 basis: percentage of the applicable category charges, or a fixed amount.</summary>
    public enum DiscountBasis : short
    {
        Percentage = 1,
        FixedAmount = 2,
    }

    /// <summary>BR-DIS-001 computation stage (pack-driven). Only matters for FixedAmount — a percentage of net×(1+VAT) equals the same percentage of gross.</summary>
    public enum DiscountComputationStage : short
    {
        BeforeVat = 1,
        AfterVat = 2,
    }

    /// <summary>BR-DIS-001 eligibility mode.</summary>
    public enum DiscountEligibilityMode : short
    {
        Automatic = 1,
        Manual = 2,
        Scholarship = 3,
    }

    /// <summary>BR-DIS-001/007 renewal mode: automatic types re-evaluate at rollover; manual/scholarship types go through the renewal review queue.</summary>
    public enum DiscountRenewalMode : short
    {
        AutoReevaluate = 1,
        ManualRegrant = 2,
    }

    /// <summary>BR-DIS-002 automatic rule kinds this slice evaluates for real.</summary>
    public enum EligibilityRuleKind : short
    {
        SiblingLadder = 1,
        Staff = 2,
    }

    /// <summary>Where a grant came from — the register's "who got what, why".</summary>
    public enum DiscountGrantSource : short
    {
        Automatic = 1,
        Manual = 2,
        Scholarship = 3,
        Renewal = 4,
    }

    /// <summary>WF-04 state of a grant. Application (discount documents) happens on Approved.</summary>
    public enum DiscountGrantStatus : short
    {
        Proposed = 1,
        Approved = 2,
        Rejected = 3,
        Revoked = 4,
    }

    /// <summary>BR-DIS-003 threshold-routed chain (P4) + BR-DIS-004 committee — recorded on the grant, not routed (status-only workflow substitution).</summary>
    public enum ApprovalTier : short
    {
        FinanceManager = 1,
        Principal = 2,
        Owner = 3,
        Committee = 4,
    }

    /// <summary>BR-DIS-006 waiver kinds — operational forgiveness, reported separately from pricing-policy discounts.</summary>
    public enum WaiverKind : short
    {
        LateFee = 1,
        BounceFee = 2,
        Misc = 3,
    }

    public enum WaiverStatus : short
    {
        Proposed = 1,
        Approved = 2,
        Rejected = 3,
    }

    /// <summary>BR-DIS-007 renewal review outcome.</summary>
    public enum RenewalDecision : short
    {
        Pending = 1,
        Approved = 2,
        Adjusted = 3,
        Dropped = 4,
    }
}
