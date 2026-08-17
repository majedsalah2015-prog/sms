using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Fees;
using Sms.Application.Installments;
using Sms.Application.Library;
using Sms.Application.Notifications;
using Sms.Domain.Calendar;
using Sms.Domain.Fees;
using Sms.Domain.Library;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Library
{
    /// <summary>Standalone — saves itself. Circulation is P1 (desk speed): every method is a short unit of work.</summary>
    public class LibraryAdmin : ILibraryAdmin
    {
        public const string OverdueEventCode = "LibraryOverdue";
        public const string ReservationReadyEventCode = "LibraryReservationReady";

        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IFeeAdmin _feeAdmin;
        private readonly INotificationPublisher _notifications;

        public LibraryAdmin(AppDbContext db, IClock clock, IAuditContext audit, IWorkingYearContext workingYear, IFeeAdmin feeAdmin, INotificationPublisher notifications)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _feeAdmin = feeAdmin;
            _notifications = notifications;
        }

        // ------------------------------------------------------------------ catalog + policy

        public async Task<Title> AddTitleAsync(string? titleAr, string? titleEn, string? author = null, string? isbn = null, string? deweyClass = null, string? subjectTags = null, int? minStageSequence = null, string? transliteration = null, CancellationToken cancellationToken = default)
        {
            var title = new Title { TitleAr = titleAr, TitleEn = titleEn, Author = author, Isbn = isbn, DeweyClass = deweyClass, SubjectTags = subjectTags, MinStageSequence = minStageSequence, Transliteration = transliteration };
            _db.Titles.Add(title);
            await _db.SaveChangesAsync(cancellationToken);
            return title;
        }

        public async Task<Copy> AddCopyAsync(int titleId, string barcode, decimal? cost = null, DateTime? acquiredOn = null, string? shelfLocation = null, CancellationToken cancellationToken = default)
        {
            if (await _db.Copies.AnyAsync(c => c.Barcode == barcode, cancellationToken))
            {
                throw new DuplicateBarcodeException(barcode);
            }

            var copy = new Copy { TitleId = titleId, Barcode = barcode, Cost = cost, AcquiredOn = acquiredOn, ShelfLocation = shelfLocation };
            _db.Copies.Add(copy);
            await _db.SaveChangesAsync(cancellationToken);
            return copy;
        }

        public async Task<MemberPolicy> DefinePolicyAsync(MemberKind memberKind, int? stageId, int maxConcurrentLoans, int loanDays, int maxRenewals, int maxReservations, bool finesEnabled = false, decimal finePerDay = 0m, decimal fineCap = 0m, int? lostAfterOverdueDays = null, int holdWindowDays = 2, CancellationToken cancellationToken = default)
        {
            var policy = await _db.MemberPolicies.SingleOrDefaultAsync(p => p.MemberKind == memberKind && p.StageId == stageId, cancellationToken)
                         ?? _db.MemberPolicies.Add(new MemberPolicy { MemberKind = memberKind, StageId = stageId }).Entity;
            policy.MaxConcurrentLoans = maxConcurrentLoans;
            policy.LoanDays = loanDays;
            policy.MaxRenewals = maxRenewals;
            policy.MaxReservations = maxReservations;
            policy.FinesEnabled = finesEnabled;
            policy.FinePerDay = finePerDay;
            policy.FineCap = fineCap;
            policy.LostAfterOverdueDays = lostAfterOverdueDays;
            policy.HoldWindowDays = holdWindowDays;
            await _db.SaveChangesAsync(cancellationToken);
            return policy;
        }

        private async Task<MemberPolicy> ResolvePolicyAsync(MemberKind kind, int memberId, CancellationToken cancellationToken)
        {
            int? stageId = null;
            if (kind == MemberKind.Student)
            {
                stageId = await (
                    from e in _db.Enrollments
                    join p in _db.GradeYearProfiles on e.GradeYearProfileId equals p.Id
                    join g in _db.GradeLevels on p.GradeLevelId equals g.Id
                    where e.StudentId == memberId && e.AcademicYearId == _workingYear.AcademicYearId
                    select (int?)g.StageId).FirstOrDefaultAsync(cancellationToken);
            }

            return await _db.MemberPolicies.SingleOrDefaultAsync(p => p.MemberKind == kind && p.StageId == stageId, cancellationToken)
                   ?? await _db.MemberPolicies.SingleOrDefaultAsync(p => p.MemberKind == kind && p.StageId == null, cancellationToken)
                   ?? new MemberPolicy { MemberKind = kind };
        }

        // ------------------------------------------------------------------ circulation

        private async Task<Func<DateTime, bool>> WorkingDayAsync(ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken)
        {
            var overrides = await _db.CalendarDays.Where(d => d.AcademicYearId == _workingYear.AcademicYearId).ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);
            return date => CalendarDayResolver.Resolve(date, weekendDays, overrides) == DayType.Working;
        }

        private async Task<int> UnpaidFineCountAsync(MemberKind kind, int memberId, CancellationToken cancellationToken)
        {
            var chargeIds = await _db.FineProposals
                .Where(f => f.MemberKind == kind && f.MemberId == memberId && f.Status == FineProposalStatus.Confirmed && f.ChargeId != null)
                .Select(f => f.ChargeId!.Value).ToListAsync(cancellationToken);
            if (chargeIds.Count == 0)
            {
                return 0;
            }

            var charges = await _db.Charges.Where(c => chargeIds.Contains(c.Id) && c.Status == ChargeStatus.Posted).Select(c => new { c.Id, c.GrossAmount }).ToListAsync(cancellationToken);
            var credited = (await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounted = (await _db.DiscountDocuments.Where(d => chargeIds.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.Where(a => chargeIds.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(cancellationToken)).GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            return charges.Count(c => c.GrossAmount
                                      - (credited.TryGetValue(c.Id, out var cr) ? cr : 0m)
                                      - (discounted.TryGetValue(c.Id, out var ds) ? ds : 0m)
                                      - (allocated.TryGetValue(c.Id, out var al) ? al : 0m) > 0m);
        }

        public async Task<Loan> CheckoutAsync(string barcode, MemberKind memberKind, int memberId, int actorUserId, ISet<DayOfWeek> weekendDays, string? overrideReason = null, CancellationToken cancellationToken = default)
        {
            var copy = await _db.Copies.SingleAsync(c => c.Barcode == barcode, cancellationToken);
            var policy = await ResolvePolicyAsync(memberKind, memberId, cancellationToken);
            var activeLoans = await _db.Loans.CountAsync(l => l.MemberKind == memberKind && l.MemberId == memberId && l.ReturnedAtUtc == null, cancellationToken);
            var unpaidFines = await UnpaidFineCountAsync(memberKind, memberId, cancellationToken);
            // A copy Reserved for THIS member counts as available to them (BR-LIB-004 hold pickup).
            var heldForMember = copy.Status == CopyStatus.Reserved && await _db.Reservations.AnyAsync(r => r.HeldCopyId == copy.Id && r.Status == ReservationStatus.Offered && r.MemberKind == memberKind && r.MemberId == memberId, cancellationToken);
            var verdict = CheckoutPolicy.Evaluate(copy.Status == CopyStatus.Available || heldForMember, activeLoans, policy.MaxConcurrentLoans, unpaidFines > 0, hasClearanceHold: false);
            var isOverride = false;
            if (!verdict.Allowed)
            {
                // Copy availability is physical - no override can lend a copy that isn't on the shelf.
                if (!verdict.CopyAvailable || string.IsNullOrWhiteSpace(overrideReason))
                {
                    throw new CheckoutBlockedException(barcode, !verdict.CopyAvailable ? $"copy is {copy.Status}" : !verdict.WithinLoanLimit ? "loan limit reached" : "unpaid fines / clearance hold");
                }

                isOverride = true;
            }

            var isWorkingDay = await WorkingDayAsync(weekendDays, cancellationToken);
            var loan = new Loan
            {
                CopyId = copy.Id, MemberKind = memberKind, MemberId = memberId, IssuedAtUtc = _clock.UtcNow,
                DueDate = DueDateShifter.ShiftToWorkingDay(_clock.UtcNow.Date.AddDays(policy.LoanDays), isWorkingDay), WasOverrideCheckout = isOverride,
            };
            _db.Loans.Add(loan);
            copy.Status = CopyStatus.Loaned;
            if (heldForMember)
            {
                var reservation = await _db.Reservations.SingleAsync(r => r.HeldCopyId == copy.Id && r.Status == ReservationStatus.Offered, cancellationToken);
                reservation.Status = ReservationStatus.Fulfilled;
            }

            await _db.SaveChangesAsync(cancellationToken);
            _db.CirculationEvents.Add(new CirculationEvent { LoanId = loan.Id, Kind = isOverride ? CirculationEventKind.OverrideCheckout : CirculationEventKind.Checkout, ActorUserId = actorUserId, AtUtc = _clock.UtcNow, Note = overrideReason });
            await _db.SaveChangesAsync(cancellationToken);
            return loan;
        }

        public async Task<IReadOnlyList<(int StudentId, string Barcode, Loan? Loan, string? Error)>> ClassVisitCheckoutAsync(IReadOnlyList<ClassVisitIssue> issues, int actorUserId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default)
        {
            var results = new List<(int, string, Loan?, string?)>();
            foreach (var issue in issues)
            {
                try
                {
                    var loan = await CheckoutAsync(issue.Barcode, MemberKind.Student, issue.StudentId, actorUserId, weekendDays, null, cancellationToken);
                    loan.IsClassVisit = true;
                    await _db.SaveChangesAsync(cancellationToken);
                    results.Add((issue.StudentId, issue.Barcode, loan, null));
                }
                catch (InvalidOperationException ex)
                {
                    results.Add((issue.StudentId, issue.Barcode, null, ex.Message));
                }
            }

            return results;
        }

        public async Task RenewAsync(int loanId, int actorUserId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken = default)
        {
            var loan = await _db.Loans.SingleAsync(l => l.Id == loanId, cancellationToken);
            if (loan.ReturnedAtUtc != null)
            {
                throw new LoanNotOpenException(loanId);
            }

            var copy = await _db.Copies.SingleAsync(c => c.Id == loan.CopyId, cancellationToken);
            var policy = await ResolvePolicyAsync(loan.MemberKind, loan.MemberId, cancellationToken);
            var reservedByAnother = await _db.Reservations.AnyAsync(r => r.TitleId == copy.TitleId && r.Status == ReservationStatus.Queued && !(r.MemberKind == loan.MemberKind && r.MemberId == loan.MemberId), cancellationToken);
            if (!RenewalPolicy.CanRenew(loan.RenewalCount, policy.MaxRenewals, reservedByAnother))
            {
                throw new RenewalNotAllowedException(loanId);
            }

            var isWorkingDay = await WorkingDayAsync(weekendDays, cancellationToken);
            loan.RenewalCount++;
            loan.DueDate = DueDateShifter.ShiftToWorkingDay(loan.DueDate.AddDays(policy.LoanDays), isWorkingDay);
            _db.CirculationEvents.Add(new CirculationEvent { LoanId = loan.Id, Kind = CirculationEventKind.Renewal, ActorUserId = actorUserId, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ReturnAsync(string barcode, int actorUserId, CancellationToken cancellationToken = default)
        {
            var copy = await _db.Copies.SingleAsync(c => c.Barcode == barcode, cancellationToken);
            var loan = await _db.Loans.Where(l => l.CopyId == copy.Id && l.ReturnedAtUtc == null).OrderByDescending(l => l.Id).FirstOrDefaultAsync(cancellationToken);
            var wasLost = copy.Status == CopyStatus.Lost;
            if (loan != null)
            {
                loan.ReturnedAtUtc = _clock.UtcNow;
                _db.CirculationEvents.Add(new CirculationEvent { LoanId = loan.Id, Kind = wasLost ? CirculationEventKind.Found : CirculationEventKind.Return, ActorUserId = actorUserId, AtUtc = _clock.UtcNow });
            }

            if (wasLost)
            {
                // BR-LIB-006 found-later: reverse the replacement charge per finance rules (credit note if charged).
                var replacement = loan == null ? null : await _db.FineProposals.SingleOrDefaultAsync(f => f.LoanId == loan.Id && f.Kind == FineKind.Replacement && f.Status != FineProposalStatus.Waived, cancellationToken);
                if (replacement != null)
                {
                    if (replacement.ChargeId.HasValue && replacement.CreditNoteId == null)
                    {
                        var creditNote = await _feeAdmin.IssueCreditNoteAsync(replacement.ChargeId.Value, replacement.Amount, "library item found (BR-LIB-006)", cancellationToken);
                        replacement.CreditNoteId = creditNote.Id;
                    }

                    _audit.Reason = "item found";
                    replacement.Status = FineProposalStatus.Waived;
                }
            }

            copy.Status = CopyStatus.Available;
            await _db.SaveChangesAsync(cancellationToken);
            await OfferToQueueAsync(copy, cancellationToken);
        }

        // ------------------------------------------------------------------ reservations

        private async Task OfferToQueueAsync(Copy copy, CancellationToken cancellationToken)
        {
            var queued = await _db.Reservations.Where(r => r.TitleId == copy.TitleId && r.Status == ReservationStatus.Queued).Select(r => new ReservationQueuePolicy.Queued(r.Id, r.QueuedAtUtc)).ToListAsync(cancellationToken);
            var nextId = ReservationQueuePolicy.NextToOffer(queued);
            if (nextId == null)
            {
                return;
            }

            var reservation = await _db.Reservations.SingleAsync(r => r.Id == nextId.Value, cancellationToken);
            var policy = await ResolvePolicyAsync(reservation.MemberKind, reservation.MemberId, cancellationToken);
            reservation.Status = ReservationStatus.Offered;
            reservation.HeldCopyId = copy.Id;
            reservation.HoldExpiresAtUtc = _clock.UtcNow.AddDays(policy.HoldWindowDays);
            copy.Status = CopyStatus.Reserved;
            await _db.SaveChangesAsync(cancellationToken);

            if (reservation.MemberKind == MemberKind.Student)
            {
                await NotifyGuardiansAsync(reservation.MemberId, ReservationReadyEventCode, new Dictionary<string, string> { ["Barcode"] = copy.Barcode, ["HoldUntil"] = reservation.HoldExpiresAtUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, cancellationToken);
            }
        }

        public async Task<Reservation> ReserveAsync(int titleId, MemberKind memberKind, int memberId, CancellationToken cancellationToken = default)
        {
            var policy = await ResolvePolicyAsync(memberKind, memberId, cancellationToken);
            var open = await _db.Reservations.CountAsync(r => r.MemberKind == memberKind && r.MemberId == memberId && (r.Status == ReservationStatus.Queued || r.Status == ReservationStatus.Offered), cancellationToken);
            if (open >= policy.MaxReservations)
            {
                throw new ReservationLimitReachedException(memberId);
            }

            var reservation = new Reservation { TitleId = titleId, MemberKind = memberKind, MemberId = memberId, QueuedAtUtc = _clock.UtcNow };
            _db.Reservations.Add(reservation);
            await _db.SaveChangesAsync(cancellationToken);

            var available = await _db.Copies.FirstOrDefaultAsync(c => c.TitleId == titleId && c.Status == CopyStatus.Available, cancellationToken);
            if (available != null)
            {
                await OfferToQueueAsync(available, cancellationToken);
            }

            return reservation;
        }

        public async Task<int> ExpireHoldsAsync(CancellationToken cancellationToken = default)
        {
            var now = _clock.UtcNow;
            var expired = await _db.Reservations.Where(r => r.Status == ReservationStatus.Offered && r.HoldExpiresAtUtc < now).ToListAsync(cancellationToken);
            foreach (var reservation in expired)
            {
                reservation.Status = ReservationStatus.Expired;
                var copy = await _db.Copies.SingleAsync(c => c.Id == reservation.HeldCopyId!.Value, cancellationToken);
                copy.Status = CopyStatus.Available;
                await _db.SaveChangesAsync(cancellationToken);
                await OfferToQueueAsync(copy, cancellationToken);
            }

            return expired.Count;
        }

        // ------------------------------------------------------------------ fines + lost

        public async Task<FineProposal> DeclareLostAsync(int loanId, int actorUserId, decimal? policyPrice = null, CancellationToken cancellationToken = default)
        {
            var loan = await _db.Loans.SingleAsync(l => l.Id == loanId, cancellationToken);
            if (loan.ReturnedAtUtc != null)
            {
                throw new LoanNotOpenException(loanId);
            }

            var copy = await _db.Copies.SingleAsync(c => c.Id == loan.CopyId, cancellationToken);
            var amount = ReplacementChargePolicy.Amount(copy.Cost, policyPrice) ?? throw new ReplacementPriceUnknownException(copy.Id);
            copy.Status = CopyStatus.Lost;
            var proposal = new FineProposal { LoanId = loanId, MemberKind = loan.MemberKind, MemberId = loan.MemberId, Kind = FineKind.Replacement, Amount = amount, ProposedAtUtc = _clock.UtcNow };
            _db.FineProposals.Add(proposal);
            _db.CirculationEvents.Add(new CirculationEvent { LoanId = loanId, Kind = CirculationEventKind.DeclaredLost, ActorUserId = actorUserId, AtUtc = _clock.UtcNow });
            await _db.SaveChangesAsync(cancellationToken);
            return proposal;
        }

        public async Task<IReadOnlyList<FineProposal>> ProposeOverdueFinesAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcNow.Date;
            var overdue = await _db.Loans.Where(l => l.ReturnedAtUtc == null && l.DueDate < today).ToListAsync(cancellationToken);
            var proposed = new List<FineProposal>();
            foreach (var loan in overdue)
            {
                var copy = await _db.Copies.SingleAsync(c => c.Id == loan.CopyId, cancellationToken);
                if (copy.Status == CopyStatus.Lost)
                {
                    continue;
                }

                var policy = await ResolvePolicyAsync(loan.MemberKind, loan.MemberId, cancellationToken);
                var days = FineCalculator.OverdueDays(loan.DueDate, today);
                if (loan.MemberKind == MemberKind.Student)
                {
                    await NotifyGuardiansAsync(loan.MemberId, OverdueEventCode, new Dictionary<string, string> { ["Barcode"] = copy.Barcode, ["DaysOverdue"] = days.ToString(CultureInfo.InvariantCulture) }, cancellationToken);
                }

                var amount = FineCalculator.Compute(days, policy.FinesEnabled, policy.FinePerDay, policy.FineCap);
                if (amount <= 0m || await _db.FineProposals.AnyAsync(f => f.LoanId == loan.Id && f.Kind == FineKind.Overdue && f.Status != FineProposalStatus.Waived, cancellationToken))
                {
                    continue;
                }

                var proposal = new FineProposal { LoanId = loan.Id, MemberKind = loan.MemberKind, MemberId = loan.MemberId, Kind = FineKind.Overdue, Amount = amount, ProposedAtUtc = _clock.UtcNow };
                _db.FineProposals.Add(proposal);
                proposed.Add(proposal);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return proposed;
        }

        public async Task ConfirmFinesAsync(IReadOnlyList<int> fineProposalIds, int libraryFeeCategoryId, CancellationToken cancellationToken = default)
        {
            foreach (var id in fineProposalIds)
            {
                var proposal = await _db.FineProposals.SingleAsync(f => f.Id == id, cancellationToken);
                if (proposal.Status != FineProposalStatus.Proposed || proposal.MemberKind != MemberKind.Student)
                {
                    continue; // staff fines have no payer model - flagged, stays Proposed
                }

                var payerId = await ResolveStudentPayerAsync(proposal.MemberId, cancellationToken);
                if (payerId == null)
                {
                    continue;
                }

                var charge = await _feeAdmin.PostManualChargeAsync(proposal.MemberId, payerId.Value, libraryFeeCategoryId, proposal.Amount, cancellationToken);
                proposal.ChargeId = charge.Id;
                proposal.Status = FineProposalStatus.Confirmed;
                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        public async Task WaiveFineAsync(int fineProposalId, string reason, CancellationToken cancellationToken = default)
        {
            var proposal = await _db.FineProposals.SingleAsync(f => f.Id == fineProposalId, cancellationToken);
            _audit.Reason = reason;
            proposal.Status = FineProposalStatus.Waived;
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task<int?> ResolveStudentPayerAsync(int studentId, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.IsFinanciallyResponsible && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            return await _db.Payers.Where(p => p.ParentId != null && parentIds.Contains(p.ParentId.Value)).OrderBy(p => p.Id).Select(p => (int?)p.Id).FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<(int OpenLoans, int UnpaidFines)> ClearanceStatusAsync(MemberKind memberKind, int memberId, CancellationToken cancellationToken = default)
        {
            var openLoans = await _db.Loans.CountAsync(l => l.MemberKind == memberKind && l.MemberId == memberId && l.ReturnedAtUtc == null, cancellationToken);
            return (openLoans, await UnpaidFineCountAsync(memberKind, memberId, cancellationToken));
        }

        // ------------------------------------------------------------------ stocktake

        public async Task<StocktakeSession> OpenStocktakeAsync(CancellationToken cancellationToken = default)
        {
            var session = new StocktakeSession { OpenedAtUtc = _clock.UtcNow };
            _db.StocktakeSessions.Add(session);
            await _db.SaveChangesAsync(cancellationToken);
            return session;
        }

        public async Task ScanAsync(int stocktakeSessionId, string barcode, CancellationToken cancellationToken = default)
        {
            var copy = await _db.Copies.SingleAsync(c => c.Barcode == barcode, cancellationToken);
            var line = await _db.StocktakeLines.SingleOrDefaultAsync(l => l.StocktakeSessionId == stocktakeSessionId && l.CopyId == copy.Id, cancellationToken)
                       ?? _db.StocktakeLines.Add(new StocktakeLine { StocktakeSessionId = stocktakeSessionId, CopyId = copy.Id, ExpectedStatus = copy.Status }).Entity;
            line.WasScanned = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<StocktakeLine>> ReconcileStocktakeAsync(int stocktakeSessionId, CancellationToken cancellationToken = default)
        {
            var copies = await _db.Copies.ToListAsync(cancellationToken);
            var lines = await _db.StocktakeLines.Where(l => l.StocktakeSessionId == stocktakeSessionId).ToDictionaryAsync(l => l.CopyId, cancellationToken);
            foreach (var copy in copies)
            {
                if (!lines.TryGetValue(copy.Id, out var line))
                {
                    line = new StocktakeLine { StocktakeSessionId = stocktakeSessionId, CopyId = copy.Id, ExpectedStatus = copy.Status };
                    _db.StocktakeLines.Add(line);
                    lines[copy.Id] = line;
                }

                line.ExpectedStatus = copy.Status;
                line.Finding = StocktakeFindingEvaluator.Evaluate(copy.Status, line.WasScanned);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return lines.Values.Where(l => l.Finding != StocktakeFinding.Ok).OrderBy(l => l.CopyId).ToList();
        }

        public async Task ResolveStocktakeLineAsync(int stocktakeLineId, string resolution, bool markLost = false, CancellationToken cancellationToken = default)
        {
            var line = await _db.StocktakeLines.SingleAsync(l => l.Id == stocktakeLineId, cancellationToken);
            line.Resolution = resolution;
            if (markLost)
            {
                var copy = await _db.Copies.SingleAsync(c => c.Id == line.CopyId, cancellationToken);
                copy.Status = CopyStatus.Lost;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CloseStocktakeAsync(int stocktakeSessionId, int closedByUserId, CancellationToken cancellationToken = default)
        {
            var session = await _db.StocktakeSessions.SingleAsync(s => s.Id == stocktakeSessionId, cancellationToken);
            var unresolved = await _db.StocktakeLines.CountAsync(l => l.StocktakeSessionId == stocktakeSessionId && l.Finding != StocktakeFinding.Ok && l.Resolution == null, cancellationToken);
            if (unresolved > 0)
            {
                throw new StocktakeUnresolvedException(stocktakeSessionId, unresolved);
            }

            session.Status = StocktakeStatus.Closed;
            session.ClosedAtUtc = _clock.UtcNow;
            session.ClosedByUserId = closedByUserId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ReadingLog> LogReadingAsync(int studentId, int titleId, DateTime date, string? note = null, CancellationToken cancellationToken = default)
        {
            var log = new ReadingLog { StudentId = studentId, TitleId = titleId, Date = date.Date, Note = note };
            _db.ReadingLogs.Add(log);
            await _db.SaveChangesAsync(cancellationToken);
            return log;
        }

        private async Task NotifyGuardiansAsync(int studentId, string eventCode, IReadOnlyDictionary<string, string> payload, CancellationToken cancellationToken)
        {
            var parentIds = await _db.StudentGuardianLinks.Where(l => l.StudentId == studentId && l.EffectiveToUtc == null).Select(l => l.ParentId).ToListAsync(cancellationToken);
            var parents = await _db.Parents.Where(p => parentIds.Contains(p.Id) && p.UserAccountId != null).Select(p => new { p.UserAccountId, p.PreferredLanguage }).ToListAsync(cancellationToken);
            await _notifications.PublishAsync(eventCode, parents.Select(p => new NotificationRecipient(p.UserAccountId!.Value, p.PreferredLanguage)).ToList(), payload, cancellationToken);
        }
    }
}
