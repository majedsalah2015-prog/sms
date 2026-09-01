using System;
using Sms.Application.Common.Guards;
using Sms.Domain.Schools;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>BR-SCH-005: the requested status pair isn't a legal move (e.g. Closed is terminal).</summary>
    public class InvalidSchoolStatusTransitionException : InvalidOperationException
    {
        public InvalidSchoolStatusTransitionException(SchoolStatus from, SchoolStatus to)
            : base($"School status cannot move from '{from}' to '{to}' (BR-SCH-005).")
        {
        }
    }

    /// <summary>BR-AYR-002: the requested status pair isn't a legal move (e.g. Preparation → Closed skips Active).</summary>
    public class InvalidAcademicYearStatusTransitionException : InvalidOperationException
    {
        public InvalidAcademicYearStatusTransitionException(AcademicYearStatus from, AcademicYearStatus to)
            : base($"Academic year status cannot move from '{from}' to '{to}' (BR-AYR-002).")
        {
        }
    }

    /// <summary>The two ways a year's dates break BR-AYR-001.</summary>
    public enum AcademicYearDateFault
    {
        /// <summary>The end is not after the start, or the span is outside the 6–14 months a school year runs.</summary>
        SpanOutOfRange = 1,

        /// <summary>The dates overlap a year this school already has.</summary>
        OverlapsAnotherYear = 2,
    }

    /// <summary>BR-AYR-001: date span (6–14 months) or overlap-with-another-year violation.</summary>
    public class InvalidAcademicYearDatesException : InvalidOperationException
    {
        public InvalidAcademicYearDatesException(AcademicYearDateFault fault)
            : base($"Academic year dates are invalid: {(fault == AcademicYearDateFault.SpanOutOfRange ? "span must be 6-14 months with an end date after the start date" : "overlaps an existing academic year for this school")} (BR-AYR-001).")
        {
            Fault = fault;
        }

        public AcademicYearDateFault Fault { get; }
    }

    /// <summary>BR-AYR-002: at most one Preparation year per school.</summary>
    public class DuplicatePreparationYearException : InvalidOperationException
    {
        public DuplicatePreparationYearException()
            : base("A Preparation-status academic year already exists for this school (BR-AYR-002).")
        {
        }
    }

    /// <summary>
    /// Editing/deleting an academic year is only allowed while no student is
    /// enrolled in it (and, for delete, while no other module data references it).
    /// </summary>
    /// <remarks>
    /// What still points at the year travels as a bilingual <see cref="UsageReport"/>, so the
    /// screen can list it in the reader's language rather than handing an Arabic registrar an
    /// English clause about "grade profiles or sections".
    /// </remarks>
    public class AcademicYearInUseException : InvalidOperationException
    {
        public AcademicYearInUseException(UsageReport usage)
            : base($"Academic year is in use: {usage.Describe(arabic: false)}.")
        {
            Usage = usage;
        }

        /// <summary>
        /// The overload for a database refusal on a foreign key nothing checked in advance. The
        /// provider's message is kept as the inner exception — it belongs in the log, where it names
        /// the constraint — while <see cref="Usage"/> carries what the reader is told.
        /// </summary>
        public AcademicYearInUseException(UsageReport usage, Exception inner)
            : base($"Academic year is in use: {usage.Describe(arabic: false)}.", inner)
        {
            Usage = usage;
        }

        /// <summary>Everything that still depends on the year.</summary>
        public UsageReport Usage { get; }
    }
}

namespace Sms.Application.Common.Exceptions
{
    /// <summary>Which part of a term or semester's dates is wrong (BR-AYR-007).</summary>
    public enum PeriodDateFault
    {
        /// <summary>The end date is not after the start date.</summary>
        EndsBeforeItStarts = 1,

        /// <summary>The period sticks out of the semester or year it belongs to.</summary>
        NotInsideItsParent = 2,

        /// <summary>The period overlaps another of the same kind.</summary>
        OverlapsASibling = 3,
    }

    /// <summary>What kind of period the dates belong to — a term, or the semester holding it.</summary>
    public enum SchoolPeriodKind
    {
        Semester = 1,
        Term = 2,
    }

    /// <summary>BR-AYR-007 / doc/Modules/03 §9: term dates must nest within their semester, semesters within the year, and siblings must not overlap.</summary>
    public class InvalidPeriodDatesException : System.InvalidOperationException
    {
        public InvalidPeriodDatesException(PeriodDateFault fault, SchoolPeriodKind kind)
            : base($"Period dates are invalid: {Describe(fault, kind)} (BR-AYR-007).")
        {
            Fault = fault;
            Kind = kind;
        }

        public PeriodDateFault Fault { get; }

        /// <summary>Which period was refused, so the message can name it instead of saying "period".</summary>
        public SchoolPeriodKind Kind { get; }

        private static string Describe(PeriodDateFault fault, SchoolPeriodKind kind) => fault switch
        {
            PeriodDateFault.EndsBeforeItStarts => $"the {kind.ToString().ToLowerInvariant()}'s end date must be after its start date",
            PeriodDateFault.NotInsideItsParent => $"the {kind.ToString().ToLowerInvariant()} must lie within the period that holds it",
            _ => $"the {kind.ToString().ToLowerInvariant()} overlaps a sibling {kind.ToString().ToLowerInvariant()}",
        };
    }
}
