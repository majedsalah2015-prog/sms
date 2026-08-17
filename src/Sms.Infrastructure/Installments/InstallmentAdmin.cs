using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Calendar;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Installments;
using Sms.Application.Notifications;
using Sms.Domain.Calendar;
using Sms.Domain.Fees;
using Sms.Domain.Installments;
using Sms.Domain.Payments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Installments
{
    /// <summary>
    /// Standalone admin operations — save themselves. Installment status
    /// is never stored: every read derives it from Module 21 allocations
    /// (BR-INS-007) via <see cref="LoadScheduleAsync"/>.
    /// </summary>
    public class InstallmentAdmin : IInstallmentAdmin
    {
        public const string DueSoonEventCode = "InstallmentDueSoon";
        public const string OverdueEventCode = "InstallmentOverdue";

        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly INotificationPublisher _notifications;

        public InstallmentAdmin(AppDbContext db, IClock clock, IAuditContext audit, IWorkingYearContext workingYear, INotificationPublisher notifications)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _notifications = notifications;
        }

        // ------------------------------------------------------------------ templates

        public async Task<PlanTemplate> DefineTemplateAsync(
            int academicYearId, string nameAr, string nameEn, IReadOnlyList<TemplateSplit> splits,
            int? feeCategoryId = null, decimal downPaymentPercent = 0m, int graceDays = 0, CancellationToken cancellationToken = default)
        {
            if (splits.Count == 0 || !InstallmentScheduleBuilder.SplitsSumToHundred(splits.Select(s => s.Percent).ToList()))
            {
                throw new InvalidTemplateSplitException("splits must sum to 100%");
            }

            if (splits.Any(s => s.DueDate == null && s.OffsetDaysFromYearStart == null))
            {
                throw new InvalidTemplateSplitException("every split needs a due date or an offset from year start");
            }

            var template = new PlanTemplate
            {
                AcademicYearId = academicYearId, NameAr = nameAr, NameEn = nameEn, FeeCategoryId = feeCategoryId,
                DownPaymentPercent = downPaymentPercent, GraceDays = graceDays,
            };
            for (var i = 0; i < splits.Count; i++)
            {
                template.Installments.Add(new TemplateInstallment
                {
                    SequenceNumber = i + 1, SplitPercent = splits[i].Percent, DueDate = splits[i].DueDate?.Date,
                    OffsetDaysFromYearStart = splits[i].OffsetDaysFromYearStart,
                });
            }

            _db.PlanTemplates.Add(template);
            await _db.SaveChangesAsync(cancellationToken);
            return template;
        }

        public async Task ApproveTemplateAsync(int planTemplateId, CancellationToken cancellationToken = default)
        {
            var template = await _db.PlanTemplates.SingleAsync(t => t.Id == planTemplateId, cancellationToken);
            template.Status = PlanTemplateStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ assignment + schedule generation

        private async Task<Func<DateTime, bool>> BuildWorkingDayPredicateAsync(int academicYearId, ISet<DayOfWeek> weekendDays, CancellationToken cancellationToken)
        {
            var overrides = await _db.CalendarDays
                .Where(d => d.AcademicYearId == academicYearId)
                .ToDictionaryAsync(d => d.Date.Date, d => d.DayType, cancellationToken);
            return date => CalendarDayResolver.Resolve(date, weekendDays, overrides) == DayType.Working;
        }

        private async Task<IReadOnlyList<InstallmentScheduleBuilder.ChargePortion>> LoadNetChargePortionsAsync(IReadOnlyList<int> chargeIds, CancellationToken cancellationToken)
        {
            var charges = await _db.Charges.Where(c => chargeIds.Contains(c.Id)).OrderBy(c => c.PostedAtUtc).ThenBy(c => c.Id).ToListAsync(cancellationToken);
            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize then sum in memory.
            var creditRows = await _db.CreditNotes.Where(n => chargeIds.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken);
            var creditedByCharge = creditRows.GroupBy(r => r.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            return charges
                .Select(c => new InstallmentScheduleBuilder.ChargePortion(c.Id, c.GrossAmount - (creditedByCharge.TryGetValue(c.Id, out var cr) ? cr : 0m)))
                .Where(p => p.Amount > 0m)
                .ToList();
        }

        public async Task<PlanAssignment> AssignPlanAsync(
            int studentId, int payerId, int planTemplateId, ISet<DayOfWeek> weekendDays,
            bool isException = false, string? exceptionReason = null, CancellationToken cancellationToken = default)
        {
            var template = await _db.PlanTemplates.Include(t => t.Installments).SingleAsync(t => t.Id == planTemplateId, cancellationToken);
            if (template.Status != PlanTemplateStatus.Approved)
            {
                throw new PlanTemplateNotApprovedException(planTemplateId);
            }

            if (isException && string.IsNullOrWhiteSpace(exceptionReason))
            {
                throw new ExceptionAssignmentReasonRequiredException();
            }

            var yearId = _workingYear.AcademicYearId;
            var exists = await _db.PlanAssignments.AnyAsync(
                a => a.StudentId == studentId && a.AcademicYearId == yearId && a.FeeCategoryId == template.FeeCategoryId, cancellationToken);
            if (exists)
            {
                throw new PlanAssignmentExistsException(studentId);
            }

            var chargeIds = await _db.Charges
                .Where(c => c.StudentId == studentId && c.PayerId == payerId && c.AcademicYearId == yearId && c.Status == ChargeStatus.Posted)
                .Where(c => template.FeeCategoryId == null || c.FeeCategoryId == template.FeeCategoryId)
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
            var portions = await LoadNetChargePortionsAsync(chargeIds, cancellationToken);
            if (portions.Count == 0)
            {
                throw new NoChargesToScheduleException(studentId);
            }

            var year = await _db.AcademicYears.SingleAsync(y => y.Id == yearId, cancellationToken);
            var isWorkingDay = await BuildWorkingDayPredicateAsync(yearId, weekendDays, cancellationToken);
            var splits = template.Installments.OrderBy(i => i.SequenceNumber).ToList();
            var dueDates = splits
                .Select(s => s.DueDate ?? year.StartDate.AddDays(s.OffsetDaysFromYearStart!.Value))
                .Select(d => DueDateShifter.ShiftToWorkingDay(d, isWorkingDay))
                .ToList();

            var total = portions.Sum(p => p.Amount);
            var scheduled = InstallmentScheduleBuilder.Build(total, splits.Select(s => s.SplitPercent).ToList(), dueDates);
            var lines = InstallmentScheduleBuilder.MapChargesToInstallments(portions, scheduled.Select(s => s.Amount).ToList());

            var assignment = new PlanAssignment
            {
                AcademicYearId = yearId, StudentId = studentId, PayerId = payerId, PlanTemplateId = planTemplateId,
                FeeCategoryId = template.FeeCategoryId, IsException = isException, ExceptionReason = exceptionReason,
            };
            foreach (var s in scheduled)
            {
                var installment = new Installment { SequenceNumber = s.SequenceNumber, DueDate = s.DueDate, Amount = s.Amount };
                foreach (var line in lines.Where(l => l.InstallmentIndex == s.SequenceNumber - 1))
                {
                    installment.ChargeLines.Add(new InstallmentChargeLine { ChargeId = line.ChargeId, Amount = line.Amount });
                }

                assignment.Installments.Add(installment);
            }

            _db.PlanAssignments.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);

            await LogRevisionAsync(assignment.Id, ScheduleRevisionCause.Generated, null, "[]", cancellationToken);
            return assignment;
        }

        // ------------------------------------------------------------------ derived schedule

        private sealed record LoadedInstallment(Installment Row, decimal Paid, InstallmentStatus Status, bool IsCollectible);

        private sealed record LoadedSchedule(PlanAssignment Assignment, PlanTemplate Template, IReadOnlyList<LoadedInstallment> Installments, IReadOnlyList<InstallmentChargeLine> Lines);

        private async Task<LoadedSchedule> LoadScheduleAsync(int planAssignmentId, CancellationToken cancellationToken)
        {
            var assignment = await _db.PlanAssignments.SingleAsync(a => a.Id == planAssignmentId, cancellationToken);
            var template = await _db.PlanTemplates.SingleAsync(t => t.Id == assignment.PlanTemplateId, cancellationToken);
            var rows = await _db.Installments
                .Where(i => i.PlanAssignmentId == planAssignmentId)
                .OrderBy(i => i.DueDate).ThenBy(i => i.SequenceNumber)
                .ToListAsync(cancellationToken);
            var ids = rows.Select(r => r.Id).ToList();
            var lines = await _db.InstallmentChargeLines.Where(l => ids.Contains(l.InstallmentId)).ToListAsync(cancellationToken);
            var chargeIds = lines.Select(l => l.ChargeId).Distinct().ToList();

            var allocationRows = await _db.PaymentAllocations
                .Where(a => chargeIds.Contains(a.ChargeId))
                .Select(a => new { a.ChargeId, a.AllocatedAmount })
                .ToListAsync(cancellationToken);
            var allocatedByCharge = allocationRows.GroupBy(a => a.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));
            var scheduledByCharge = lines.GroupBy(l => l.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));

            // Module 21 pays charges; the schedule's collected total is what has been allocated to its charges,
            // capped at what this schedule actually scheduled of each charge.
            var totalPaid = scheduledByCharge.Sum(kv => Math.Min(kv.Value, allocatedByCharge.TryGetValue(kv.Key, out var a) ? a : 0m));

            var collectible = rows.Where(r => !r.IsSuperseded && !r.IsWrittenOff).ToList();
            var paidPerCollectible = InstallmentPaymentWaterfall.Apply(collectible.Select(r => r.Amount).ToList(), totalPaid);
            var paidById = collectible.Select((r, i) => (r.Id, paidPerCollectible[i])).ToDictionary(x => x.Id, x => x.Item2);

            var today = _clock.UtcNow;
            var loaded = rows.Select(r =>
            {
                var paid = paidById.TryGetValue(r.Id, out var p) ? p : 0m;
                var status = InstallmentStatusDeriver.Derive(r.Amount, paid, r.DueDate, template.GraceDays, today, r.IsSuperseded, r.IsWrittenOff);
                return new LoadedInstallment(r, paid, status, !r.IsSuperseded && !r.IsWrittenOff);
            }).ToList();

            return new LoadedSchedule(assignment, template, loaded, lines);
        }

        public async Task<IReadOnlyList<InstallmentView>> GetScheduleAsync(int planAssignmentId, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            return schedule.Installments
                .Select(i => new InstallmentView(i.Row.Id, i.Row.SequenceNumber, i.Row.DueDate, i.Row.Amount, i.Paid, i.Status, i.Row.CoveringPdcId != null))
                .ToList();
        }

        private static string Snapshot(IEnumerable<Installment> rows) => JsonSerializer.Serialize(rows
            .OrderBy(r => r.SequenceNumber)
            .Select(r => new { r.SequenceNumber, DueDate = r.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), r.Amount, r.IsSuperseded, r.IsWrittenOff }));

        private async Task LogRevisionAsync(int planAssignmentId, ScheduleRevisionCause cause, string? reason, string beforeJson, CancellationToken cancellationToken)
        {
            var rows = await _db.Installments.Where(i => i.PlanAssignmentId == planAssignmentId).ToListAsync(cancellationToken);
            _db.ScheduleRevisions.Add(new ScheduleRevision
            {
                PlanAssignmentId = planAssignmentId, Cause = cause, Reason = reason, BeforeJson = beforeJson, AfterJson = Snapshot(rows), OccurredAtUtc = _clock.UtcNow,
            });
            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>Takes <paramref name="amount"/> off an installment's charge lines, last line first; returns what was freed per charge.</summary>
        private static List<InstallmentScheduleBuilder.ChargePortion> FreeFromLines(List<InstallmentChargeLine> lines, decimal amount, AppDbContext db)
        {
            var freed = new List<InstallmentScheduleBuilder.ChargePortion>();
            var remaining = amount;
            foreach (var line in lines.OrderByDescending(l => l.Id).ToList())
            {
                if (remaining <= 0m)
                {
                    break;
                }

                var take = Math.Min(line.Amount, remaining);
                line.Amount -= take;
                remaining -= take;
                freed.Add(new InstallmentScheduleBuilder.ChargePortion(line.ChargeId, take));
                if (line.Amount == 0m)
                {
                    db.InstallmentChargeLines.Remove(line);
                }
            }

            return freed;
        }

        // ------------------------------------------------------------------ BR-INS-003 controlled recomputation

        public async Task AppendChargeAsync(int planAssignmentId, int chargeId, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var charge = await _db.Charges.SingleAsync(c => c.Id == chargeId, cancellationToken);
            if (charge.Status != ChargeStatus.Posted || charge.StudentId != schedule.Assignment.StudentId)
            {
                throw new ChargeNotPostedException(chargeId);
            }

            if (schedule.Lines.Any(l => l.ChargeId == chargeId))
            {
                return; // already scheduled — idempotent
            }

            var portion = (await LoadNetChargePortionsAsync(new[] { chargeId }, cancellationToken)).SingleOrDefault();
            if (portion == null)
            {
                return;
            }

            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var open = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).Select(i => i.Row).ToList();
            if (open.Count == 0)
            {
                var lastSeq = schedule.Installments.Select(i => i.Row.SequenceNumber).DefaultIfEmpty(0).Max();
                var due = schedule.Installments.Select(i => i.Row.DueDate).DefaultIfEmpty(_clock.UtcNow.Date).Max();
                var single = new Installment { PlanAssignmentId = planAssignmentId, SequenceNumber = lastSeq + 1, DueDate = due < _clock.UtcNow.Date ? _clock.UtcNow.Date : due, Amount = portion.Amount };
                single.ChargeLines.Add(new InstallmentChargeLine { ChargeId = chargeId, Amount = portion.Amount });
                _db.Installments.Add(single);
            }
            else
            {
                var spread = InstallmentScheduleBuilder.SpreadEvenly(portion.Amount, open.Count);
                for (var i = 0; i < open.Count; i++)
                {
                    open[i].Amount += spread[i];
                    _db.InstallmentChargeLines.Add(new InstallmentChargeLine { InstallmentId = open[i].Id, ChargeId = chargeId, Amount = spread[i] });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(planAssignmentId, ScheduleRevisionCause.ChargeAppended, $"charge {charge.ChargeNo}", before, cancellationToken);
        }

        public async Task ReduceScheduleAsync(int planAssignmentId, decimal reduction, string reason, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var open = schedule.Installments
                .Where(i => i.IsCollectible)
                .Select(i => new ScheduleReductionAllocator.OpenInstallment(i.Row.Id, i.Row.DueDate, i.Row.Amount, i.Paid))
                .ToList();

            var changes = ScheduleReductionAllocator.Reduce(open, reduction, _clock.UtcNow);
            _audit.Reason = reason;
            foreach (var (installmentId, newAmount) in changes)
            {
                var row = schedule.Installments.Single(i => i.Row.Id == installmentId).Row;
                var freed = row.Amount - newAmount;
                row.Amount = newAmount;
                FreeFromLines(schedule.Lines.Where(l => l.InstallmentId == installmentId).ToList(), freed, _db);
            }

            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(planAssignmentId, ScheduleRevisionCause.Reduced, reason, before, cancellationToken);
        }

        // ------------------------------------------------------------------ BR-INS-005 rescheduling

        private sealed record ProposedLine(string DueDate, decimal Amount);

        public async Task<RescheduleCase> ProposeRescheduleAsync(
            int planAssignmentId, int proposedByUserId, string reason, IReadOnlyList<ProposedInstallment> proposal,
            ISet<DayOfWeek> weekendDays, int maxExtensionMonths = 3, CancellationToken cancellationToken = default)
        {
            var schedule = await LoadScheduleAsync(planAssignmentId, cancellationToken);
            var unpaid = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).ToList();
            var remainder = unpaid.Sum(i => i.Row.Amount - i.Paid);
            var proposed = proposal.Sum(p => p.Amount);
            if (proposal.Count == 0 || proposed != remainder)
            {
                throw new RescheduleRemainderMismatchException(remainder, proposed);
            }

            var isWorkingDay = await BuildWorkingDayPredicateAsync(schedule.Assignment.AcademicYearId, weekendDays, cancellationToken);
            var shifted = proposal.Select(p => new ProposedInstallment(DueDateShifter.ShiftToWorkingDay(p.DueDate, isWorkingDay), p.Amount)).ToList();

            var year = await _db.AcademicYears.SingleAsync(y => y.Id == schedule.Assignment.AcademicYearId, cancellationToken);
            var originalLast = unpaid.Max(i => i.Row.DueDate);
            var proposedLast = shifted.Max(p => p.DueDate);

            var rescheduleCase = new RescheduleCase
            {
                PlanAssignmentId = planAssignmentId, ProposedByUserId = proposedByUserId, Reason = reason,
                ProposedScheduleJson = JsonSerializer.Serialize(shifted.Select(p => new ProposedLine(p.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), p.Amount))),
                RemainderAmount = remainder,
                RequiresPrincipal = RescheduleApprovalRouter.RequiresPrincipal(originalLast, proposedLast, year.EndDate, maxExtensionMonths),
                ProposedAtUtc = _clock.UtcNow,
            };
            _db.RescheduleCases.Add(rescheduleCase);
            await _db.SaveChangesAsync(cancellationToken);
            return rescheduleCase;
        }

        public async Task DecideRescheduleAsync(int rescheduleCaseId, bool approve, string? decisionReason = null, CancellationToken cancellationToken = default)
        {
            var rescheduleCase = await _db.RescheduleCases.SingleAsync(c => c.Id == rescheduleCaseId, cancellationToken);
            if (rescheduleCase.Status != RescheduleCaseStatus.Proposed)
            {
                throw new RescheduleCaseNotPendingException(rescheduleCaseId);
            }

            rescheduleCase.DecidedAtUtc = _clock.UtcNow;
            rescheduleCase.DecisionReason = decisionReason;
            if (!approve)
            {
                rescheduleCase.Status = RescheduleCaseStatus.Rejected;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var schedule = await LoadScheduleAsync(rescheduleCase.PlanAssignmentId, cancellationToken);
            var unpaid = schedule.Installments.Where(i => i.IsCollectible && i.Paid < i.Row.Amount).ToList();
            var remainder = unpaid.Sum(i => i.Row.Amount - i.Paid);
            if (remainder != rescheduleCase.RemainderAmount)
            {
                // Money moved between proposal and decision — the proposal no longer covers the remainder exactly.
                throw new RescheduleRemainderMismatchException(remainder, rescheduleCase.RemainderAmount);
            }

            var before = Snapshot(schedule.Installments.Select(i => i.Row));
            var freed = new List<InstallmentScheduleBuilder.ChargePortion>();
            foreach (var item in unpaid)
            {
                var lines = schedule.Lines.Where(l => l.InstallmentId == item.Row.Id).ToList();
                if (item.Paid == 0m)
                {
                    // Wholly unpaid: superseded, kept in history; its charge lines move to the new schedule.
                    item.Row.IsSuperseded = true;
                    freed.AddRange(FreeFromLines(lines, item.Row.Amount, _db));
                }
                else
                {
                    // Partially paid: the paid part stays (derives to Paid), only the unpaid remainder moves.
                    var moving = item.Row.Amount - item.Paid;
                    item.Row.Amount = item.Paid;
                    freed.AddRange(FreeFromLines(lines, moving, _db));
                }
            }

            var proposal = JsonSerializer.Deserialize<List<ProposedLine>>(rescheduleCase.ProposedScheduleJson)!;
            var mapped = InstallmentScheduleBuilder.MapChargesToInstallments(freed, proposal.Select(p => p.Amount).ToList());
            var nextSeq = schedule.Installments.Max(i => i.Row.SequenceNumber) + 1;
            for (var i = 0; i < proposal.Count; i++)
            {
                var installment = new Installment
                {
                    PlanAssignmentId = rescheduleCase.PlanAssignmentId, SequenceNumber = nextSeq + i,
                    DueDate = DateTime.ParseExact(proposal[i].DueDate, "yyyy-MM-dd", CultureInfo.InvariantCulture), Amount = proposal[i].Amount,
                };
                foreach (var line in mapped.Where(m => m.InstallmentIndex == i))
                {
                    installment.ChargeLines.Add(new InstallmentChargeLine { ChargeId = line.ChargeId, Amount = line.Amount });
                }

                _db.Installments.Add(installment);
            }

            schedule.Assignment.RescheduleCount++;
            rescheduleCase.Status = RescheduleCaseStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
            await LogRevisionAsync(rescheduleCase.PlanAssignmentId, ScheduleRevisionCause.Rescheduled, rescheduleCase.Reason, before, cancellationToken);
        }

        // ------------------------------------------------------------------ BR-INS-006 promises

        public async Task<PromiseToPay> RecordPromiseAsync(int installmentId, int recordedByUserId, DateTime promisedDate, decimal amount, int horizonDays = 30, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            var schedule = await LoadScheduleAsync(row.PlanAssignmentId, cancellationToken);
            var item = schedule.Installments.Single(i => i.Row.Id == installmentId);
            if (!InstallmentStatusDeriver.IsTrulyOverdue(item.Row.Amount, item.Paid, item.Row.DueDate, schedule.Template.GraceDays, _clock.UtcNow, item.Row.IsSuperseded, item.Row.IsWrittenOff))
            {
                throw new InstallmentNotOverdueException(installmentId);
            }

            var today = _clock.UtcNow.Date;
            if (promisedDate.Date < today || promisedDate.Date > today.AddDays(horizonDays))
            {
                throw new PromiseDateOutOfRangeException(promisedDate);
            }

            var promise = new PromiseToPay { InstallmentId = installmentId, RecordedByUserId = recordedByUserId, PromisedDate = promisedDate.Date, Amount = amount };
            _db.PromisesToPay.Add(promise);
            await _db.SaveChangesAsync(cancellationToken);
            return promise;
        }

        public async Task<int> EvaluatePromisesAsync(CancellationToken cancellationToken = default)
        {
            var today = _clock.UtcNow.Date;
            var due = await _db.PromisesToPay.Where(p => p.Status == PromiseStatus.Open && p.PromisedDate < today).ToListAsync(cancellationToken);
            if (due.Count == 0)
            {
                return 0;
            }

            var installmentIds = due.Select(p => p.InstallmentId).Distinct().ToList();
            var assignmentByInstallment = await _db.Installments.Where(i => installmentIds.Contains(i.Id)).ToDictionaryAsync(i => i.Id, i => i.PlanAssignmentId, cancellationToken);
            var schedules = new Dictionary<int, LoadedSchedule>();
            var broken = 0;
            foreach (var promise in due)
            {
                var assignmentId = assignmentByInstallment[promise.InstallmentId];
                if (!schedules.TryGetValue(assignmentId, out var schedule))
                {
                    schedule = await LoadScheduleAsync(assignmentId, cancellationToken);
                    schedules[assignmentId] = schedule;
                }

                var item = schedule.Installments.Single(i => i.Row.Id == promise.InstallmentId);
                promise.Status = item.Status == InstallmentStatus.Paid ? PromiseStatus.Kept : PromiseStatus.Broken;
                promise.ResolvedAtUtc = _clock.UtcNow;
                if (promise.Status == PromiseStatus.Broken)
                {
                    broken++;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return broken;
        }

        // ------------------------------------------------------------------ BR-INS-009 PDC coverage, WF-06 write-off

        public async Task MarkPdcCoveredAsync(int installmentId, int pdcId, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            var assignment = await _db.PlanAssignments.SingleAsync(a => a.Id == row.PlanAssignmentId, cancellationToken);
            var pdc = await _db.Pdcs.SingleAsync(p => p.Id == pdcId, cancellationToken);
            var live = pdc.Status is PdcStatus.Lodged or PdcStatus.Due or PdcStatus.Deposited;
            if (pdc.PayerId != assignment.PayerId || !live)
            {
                throw new PdcNotCoverableException(pdcId);
            }

            row.CoveringPdcId = pdcId;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task WriteOffAsync(int installmentId, string reason, CancellationToken cancellationToken = default)
        {
            var row = await _db.Installments.SingleAsync(i => i.Id == installmentId, cancellationToken);
            if (row.IsSuperseded || row.IsWrittenOff)
            {
                throw new InstallmentNotOpenException(installmentId);
            }

            _audit.Reason = reason;
            row.IsWrittenOff = true;
            row.WriteOffReason = reason;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ BR-INS-008 dunning

        public async Task<IReadOnlyList<DunningEvent>> RunDunningAsync(CancellationToken cancellationToken = default)
        {
            await EvaluatePromisesAsync(cancellationToken);

            var fired = new List<DunningEvent>();
            var assignmentIds = await _db.PlanAssignments.Select(a => a.Id).ToListAsync(cancellationToken);
            foreach (var assignmentId in assignmentIds)
            {
                var schedule = await LoadScheduleAsync(assignmentId, cancellationToken);
                var installmentIds = schedule.Installments.Select(i => i.Row.Id).ToList();
                var events = await _db.DunningEvents.Where(e => installmentIds.Contains(e.InstallmentId)).ToListAsync(cancellationToken);
                var brokenPromises = await _db.PromisesToPay
                    .Where(p => installmentIds.Contains(p.InstallmentId) && p.Status == PromiseStatus.Broken)
                    .ToListAsync(cancellationToken);

                foreach (var item in schedule.Installments.Where(i => i.IsCollectible))
                {
                    var stepsFired = events.Where(e => e.InstallmentId == item.Row.Id).ToList();
                    var lastFiredAt = stepsFired.Select(e => e.FiredAtUtc).DefaultIfEmpty(DateTime.MinValue).Max();
                    var hasBrokenPromise = brokenPromises.Any(p => p.InstallmentId == item.Row.Id && p.ResolvedAtUtc > lastFiredAt);
                    var trulyOverdue = InstallmentStatusDeriver.IsTrulyOverdue(item.Row.Amount, item.Paid, item.Row.DueDate, schedule.Template.GraceDays, _clock.UtcNow, item.Row.IsSuperseded, item.Row.IsWrittenOff);

                    var step = DunningLadderEvaluator.Next(
                        item.Row.DueDate, _clock.UtcNow, item.Status, trulyOverdue, item.Row.CoveringPdcId != null,
                        stepsFired.Select(e => e.Step).ToList(), hasBrokenPromise);
                    if (step == null)
                    {
                        continue;
                    }

                    var dunningEvent = new DunningEvent { InstallmentId = item.Row.Id, Step = step.Value, FiredAtUtc = _clock.UtcNow, TriggeredByBrokenPromise = hasBrokenPromise };
                    _db.DunningEvents.Add(dunningEvent);
                    fired.Add(dunningEvent);

                    var recipients = await ResolvePayerRecipientsAsync(schedule.Assignment.PayerId, cancellationToken);
                    var payload = new Dictionary<string, string>
                    {
                        ["InstallmentNo"] = item.Row.SequenceNumber.ToString(CultureInfo.InvariantCulture),
                        ["DueDate"] = item.Row.DueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                        ["Amount"] = (item.Row.Amount - item.Paid).ToString("0.00", CultureInfo.InvariantCulture),
                        ["Step"] = step.Value.ToString(),
                    };
                    var eventCode = step.Value < DunningStep.Overdue3 ? DueSoonEventCode : OverdueEventCode;
                    await _notifications.PublishAsync(eventCode, recipients, payload, cancellationToken);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return fired;
        }

        private async Task<IReadOnlyCollection<NotificationRecipient>> ResolvePayerRecipientsAsync(int payerId, CancellationToken cancellationToken)
        {
            var payer = await _db.Payers.SingleOrDefaultAsync(p => p.Id == payerId, cancellationToken);
            if (payer?.ParentId == null)
            {
                return Array.Empty<NotificationRecipient>();
            }

            var parent = await _db.Parents.SingleOrDefaultAsync(p => p.Id == payer.ParentId, cancellationToken);
            return parent?.UserAccountId == null
                ? Array.Empty<NotificationRecipient>()
                : new[] { new NotificationRecipient(parent.UserAccountId.Value, parent.PreferredLanguage) };
        }
    }
}
