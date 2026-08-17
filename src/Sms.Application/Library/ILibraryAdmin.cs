using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Library;

namespace Sms.Application.Library
{
    public sealed record ClassVisitIssue(int StudentId, string Barcode);

    /// <summary>
    /// doc/Modules/26 §8 Catalog / Circulation desk / Reservations /
    /// Fines batch / Stocktake / Class-visit screens backing (screens
    /// deferred, operations are core). Members are Student/Employee ids
    /// directly (BR-LIB-002, no registry).
    /// </summary>
    public interface ILibraryAdmin
    {
        Task<Title> AddTitleAsync(string? titleAr, string? titleEn, string? author = null, string? isbn = null, string? deweyClass = null, string? subjectTags = null, int? minStageSequence = null, string? transliteration = null, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-001 / doc §9: barcode unique (<see cref="Common.Exceptions.DuplicateBarcodeException"/>).</summary>
        Task<Copy> AddCopyAsync(int titleId, string barcode, decimal? cost = null, DateTime? acquiredOn = null, string? shelfLocation = null, CancellationToken cancellationToken = default);

        Task<MemberPolicy> DefinePolicyAsync(MemberKind memberKind, int? stageId, int maxConcurrentLoans, int loanDays, int maxRenewals, int maxReservations, bool finesEnabled = false, decimal finePerDay = 0m, decimal fineCap = 0m, int? lostAfterOverdueDays = null, int holdWindowDays = 2, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-003: available copy + within limits + no blocking flags (unpaid library fines / clearance hold) — <see cref="Common.Exceptions.CheckoutBlockedException"/> unless <paramref name="overrideReason"/> (librarian permission, logged); due date = policy days shifted off non-working days.</summary>
        Task<Loan> CheckoutAsync(string barcode, MemberKind memberKind, int memberId, int actorUserId, ISet<DayOfWeek> weekendDays, string? overrideReason = null, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-009: roster-based fast issue for a section's library period — one call, one loan per pair, failures reported per student instead of aborting the batch.</summary>
        Task<IReadOnlyList<(int StudentId, string Barcode, Loan? Loan, string? Error)>> ClassVisitCheckoutAsync(IReadOnlyList<ClassVisitIssue> issues, int actorUserId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-003: within policy unless the title is reserved by another member (<see cref="Common.Exceptions.RenewalNotAllowedException"/>).</summary>
        Task RenewAsync(int loanId, int actorUserId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-003/004: return updates status instantly; if a reservation queue exists the copy is Reserved and offered to the first in line with the hold window; a returned Lost copy triggers the found-flow (credit note if charged).</summary>
        Task ReturnAsync(string barcode, int actorUserId, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-004: queue per title within the member's reservation limit (<see cref="Common.Exceptions.ReservationLimitReachedException"/>).</summary>
        Task<Reservation> ReserveAsync(int titleId, MemberKind memberKind, int memberId, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-004: expired holds pass to the next in queue (or free the copy). Returns the number expired.</summary>
        Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-006: copy → Lost, replacement FineProposal (copy cost else policy price; <see cref="Common.Exceptions.ReplacementPriceUnknownException"/>).</summary>
        Task<FineProposal> DeclareLostAsync(int loanId, int actorUserId, decimal? policyPrice = null, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-005: proposes overdue fines for every open overdue loan whose policy has fines enabled (idempotent per loan); publishes LibraryOverdue notices for every overdue loan regardless.</summary>
        Task<IReadOnlyList<FineProposal>> ProposeOverdueFinesAsync(CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-005/006 batch-confirm: posts each proposal as a Module 19 misc charge (students only; staff proposals stay Proposed — no payer model).</summary>
        Task ConfirmFinesAsync(IReadOnlyList<int> fineProposalIds, int libraryFeeCategoryId, CancellationToken cancellationToken = default);

        Task WaiveFineAsync(int fineProposalId, string reason, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-007: clearance checklist item — open loans or unpaid library charges block.</summary>
        Task<(int OpenLoans, int UnpaidFines)> ClearanceStatusAsync(MemberKind memberKind, int memberId, CancellationToken cancellationToken = default);

        Task<StocktakeSession> OpenStocktakeAsync(CancellationToken cancellationToken = default);

        Task ScanAsync(int stocktakeSessionId, string barcode, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-008: computes findings for every catalog copy; returns the discrepancy lines.</summary>
        Task<IReadOnlyList<StocktakeLine>> ReconcileStocktakeAsync(int stocktakeSessionId, CancellationToken cancellationToken = default);

        /// <summary>Resolution actions: mark a missing copy Lost, or acknowledge with a note.</summary>
        Task ResolveStocktakeLineAsync(int stocktakeLineId, string resolution, bool markLost = false, CancellationToken cancellationToken = default);

        /// <summary>BR-LIB-008 / doc §9: close requires every discrepancy resolved (<see cref="Common.Exceptions.StocktakeUnresolvedException"/>).</summary>
        Task CloseStocktakeAsync(int stocktakeSessionId, int closedByUserId, CancellationToken cancellationToken = default);

        Task<ReadingLog> LogReadingAsync(int studentId, int titleId, DateTime date, string? note = null, CancellationToken cancellationToken = default);
    }
}
