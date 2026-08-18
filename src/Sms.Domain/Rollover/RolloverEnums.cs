namespace Sms.Domain.Rollover
{
    /// <summary>Lifecycle of one year-end rollover batch (doc/Modules/03 §4, WF-02 family).</summary>
    public enum RolloverBatchStatus : short
    {
        /// <summary>Steps 1–5 in progress: proposals, decisions, re-registration, section assignment.</summary>
        Open = 1,

        /// <summary>Step 3 batch approved by the Principal (P3) — no Undecided student remains.</summary>
        PromotionsApproved = 2,

        /// <summary>Step 6 done: target year is Active, enrollments materialized, source year is Closing.</summary>
        Activated = 3,

        /// <summary>Step 7 done: source year Closed after carry-forward + closing checklist.</summary>
        Closed = 4,
    }

    /// <summary>How a <see cref="RolloverStudentState"/>'s current decision was set.</summary>
    public enum PromotionDecisionSource : short
    {
        /// <summary>No proposal has been computed yet.</summary>
        None = 1,

        /// <summary>Auto-proposed from Module 17's YearResult (BR-GRA-006 → BR-AYR-008 step 3).</summary>
        Auto = 2,

        /// <summary>Registrar/Principal override with a recorded reason.</summary>
        Manual = 3,
    }

    /// <summary>Step 4 re-registration status per student (BR-AYR §4).</summary>
    public enum ReRegistrationStatus : short
    {
        Pending = 1,

        /// <summary>Parent confirmed (portal or counter); seat reserved; fee posted when configured.</summary>
        Confirmed = 2,

        /// <summary>"Not Re-registering" — feeds withdrawal WF-03.</summary>
        Declined = 3,

        /// <summary>Graduating students don't re-register.</summary>
        NotApplicable = 4,
    }
}
