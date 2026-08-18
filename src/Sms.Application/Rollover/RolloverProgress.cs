namespace Sms.Application.Rollover
{
    /// <summary>doc/Modules/03 §4 "Progress dashboard per step" counts for one batch (the rollover cockpit's data).</summary>
    public sealed class RolloverProgress
    {
        public int TotalStudents { get; set; }

        // Step 3
        public int Decided { get; set; }

        public int Undecided { get; set; }

        public int ProposedGraduates { get; set; }

        public int ManualOverrides { get; set; }

        // Step 4
        public int Confirmed { get; set; }

        public int Declined { get; set; }

        public int PendingReRegistration { get; set; }

        // Step 5
        public int Assigned { get; set; }

        public int ConfirmedUnassigned { get; set; }

        // Step 6
        public int Enrolled { get; set; }

        public int Processed { get; set; }

        // Step 7
        public int CarriedForward { get; set; }

        public decimal CarryForwardTotal { get; set; }
    }
}
