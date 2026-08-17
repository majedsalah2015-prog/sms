using System;
using System.Collections.Generic;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Library
{
    /// <summary>svc.Title (doc/Modules/26 §7, BR-LIB-001): bilingual-capable biblio record; Dewey by default (doc Q2: Dewey-with-Arabic-labels in v1).</summary>
    [Audited(AuditTier.T3)]
    public class Title : AuditableEntity, ISchoolScoped, ISoftActiveFiltered
    {
        public int SchoolId { get; set; }

        public string? TitleAr { get; set; }

        public string? TitleEn { get; set; }

        public string? Transliteration { get; set; }

        public string? Author { get; set; }

        public string? Isbn { get; set; }

        public string? DeweyClass { get; set; }

        public string? SubjectTags { get; set; }

        /// <summary>BR-LIB-001 age/stage suitability: minimum Stage sequence order; null = all.</summary>
        public int? MinStageSequence { get; set; }

        public bool IsActive { get; set; } = true;

        public List<Copy> Copies { get; set; } = new();
    }

    /// <summary>svc.Copy: barcoded unit; Cost drives replacement charges (BR-LIB-006). T2 — status changes are audit-relevant (BR-LIB-008 stocktake clarity).</summary>
    [Audited(AuditTier.T2)]
    public class Copy : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TitleId { get; set; }

        public string Barcode { get; set; } = string.Empty;

        public CopyStatus Status { get; set; } = CopyStatus.Available;

        public decimal? Cost { get; set; }

        public DateTime? AcquiredOn { get; set; }

        public string? ShelfLocation { get; set; }
    }

    /// <summary>svc.MemberPolicy (BR-LIB-002): per member class × stage (null stage = default for the class).</summary>
    [Audited(AuditTier.T2)]
    public class MemberPolicy : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public MemberKind MemberKind { get; set; }

        public int? StageId { get; set; }

        public int MaxConcurrentLoans { get; set; } = 2;

        public int LoanDays { get; set; } = 14;

        public int MaxRenewals { get; set; } = 1;

        public int MaxReservations { get; set; } = 2;

        /// <summary>BR-LIB-005: fines optional per school — doc Q1 proposes off.</summary>
        public bool FinesEnabled { get; set; }

        public decimal FinePerDay { get; set; }

        public decimal FineCap { get; set; }

        /// <summary>BR-LIB-006: auto-declare lost after N overdue days (null = manual only).</summary>
        public int? LostAfterOverdueDays { get; set; }

        /// <summary>BR-LIB-004: available-copy hold window in days.</summary>
        public int HoldWindowDays { get; set; } = 2;
    }

    /// <summary>svc.Loan (BR-LIB-003): member × copy with dates and renewals; ReturnedAtUtc null = open.</summary>
    [Audited(AuditTier.T2)]
    public class Loan : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int CopyId { get; set; }

        public MemberKind MemberKind { get; set; }

        public int MemberId { get; set; }

        public DateTime IssuedAtUtc { get; set; }

        public DateTime DueDate { get; set; }

        public DateTime? ReturnedAtUtc { get; set; }

        public int RenewalCount { get; set; }

        /// <summary>BR-LIB-009: issued through the section batch (class-visit mode).</summary>
        public bool IsClassVisit { get; set; }

        public bool WasOverrideCheckout { get; set; }
    }

    /// <summary>BR-LIB-003 "every event logged (member, copy, actor, time)" — append-only, not [Audited].</summary>
    public class CirculationEvent : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int LoanId { get; set; }

        public CirculationEventKind Kind { get; set; }

        public int ActorUserId { get; set; }

        public DateTime AtUtc { get; set; }

        public string? Note { get; set; }
    }

    /// <summary>svc.Reservation (BR-LIB-004): queue per title; offered copies hold for the policy window then pass on.</summary>
    [Audited(AuditTier.T3)]
    public class Reservation : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int TitleId { get; set; }

        public MemberKind MemberKind { get; set; }

        public int MemberId { get; set; }

        public DateTime QueuedAtUtc { get; set; }

        public ReservationStatus Status { get; set; } = ReservationStatus.Queued;

        public int? HeldCopyId { get; set; }

        public DateTime? HoldExpiresAtUtc { get; set; }
    }

    /// <summary>svc.FineProposal (BR-LIB-005/006): librarian proposal → confirm posts a Module 19 misc charge (students only — staff fines have no payer model yet).</summary>
    [Audited(AuditTier.T1)]
    public class FineProposal : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int LoanId { get; set; }

        public MemberKind MemberKind { get; set; }

        public int MemberId { get; set; }

        public FineKind Kind { get; set; }

        public decimal Amount { get; set; }

        public FineProposalStatus Status { get; set; } = FineProposalStatus.Proposed;

        public int? ChargeId { get; set; }

        public int? CreditNoteId { get; set; }

        public DateTime ProposedAtUtc { get; set; }
    }

    /// <summary>svc.StocktakeSession (BR-LIB-008): open → scan → resolve → close; the catalog is never bulk-corrected outside one.</summary>
    [Audited(AuditTier.T2)]
    public class StocktakeSession : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public StocktakeStatus Status { get; set; } = StocktakeStatus.Open;

        public DateTime OpenedAtUtc { get; set; }

        public DateTime? ClosedAtUtc { get; set; }

        public int? ClosedByUserId { get; set; }

        public List<StocktakeLine> Lines { get; set; } = new();
    }

    public class StocktakeLine : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StocktakeSessionId { get; set; }

        public int CopyId { get; set; }

        public CopyStatus ExpectedStatus { get; set; }

        public bool WasScanned { get; set; }

        public StocktakeFinding Finding { get; set; } = StocktakeFinding.Ok;

        public string? Resolution { get; set; }
    }

    /// <summary>svc.ReadingLog (light, class programs).</summary>
    public class ReadingLog : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int StudentId { get; set; }

        public int TitleId { get; set; }

        public DateTime Date { get; set; }

        public string? Note { get; set; }
    }
}
