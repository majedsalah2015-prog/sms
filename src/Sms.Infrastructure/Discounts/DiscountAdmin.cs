using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Discounts;
using Sms.Application.Fees;
using Sms.Application.Installments;
using Sms.Application.Numbering;
using Sms.Domain.Discounts;
using Sms.Domain.Employees;
using Sms.Domain.Fees;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Discounts
{
    /// <summary>Standalone admin operations — save themselves.</summary>
    public class DiscountAdmin : IDiscountAdmin
    {
        public const string DiscountDocumentSeriesCode = "DSC";

        private readonly AppDbContext _db;
        private readonly INumberIssuer _numberIssuer;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;
        private readonly IWorkingYearContext _workingYear;
        private readonly IFeeAdmin _feeAdmin;
        private readonly IInstallmentAdmin _installments;

        public DiscountAdmin(
            AppDbContext db, INumberIssuer numberIssuer, IClock clock, IAuditContext audit, IWorkingYearContext workingYear,
            IFeeAdmin feeAdmin, IInstallmentAdmin installments)
        {
            _db = db;
            _numberIssuer = numberIssuer;
            _clock = clock;
            _audit = audit;
            _workingYear = workingYear;
            _feeAdmin = feeAdmin;
            _installments = installments;
        }

        // ------------------------------------------------------------------ types

        public async Task<DiscountType> DefineTypeAsync(
            string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId = null, DiscountComputationStage stage = DiscountComputationStage.BeforeVat, decimal? capAmountPerStudent = null,
            bool isStackable = true, decimal maxCombinedPercent = 100m, DiscountRenewalMode renewalMode = DiscountRenewalMode.ManualRegrant,
            bool requiresHardshipDocumentation = false, IReadOnlyList<EligibilityRuleInput>? rules = null, CancellationToken cancellationToken = default)
        {
            var type = new DiscountType
            {
                NameAr = nameAr, NameEn = nameEn, Basis = basis, EligibilityMode = eligibilityMode, FeeCategoryId = feeCategoryId,
                ComputationStage = stage, CapAmountPerStudent = capAmountPerStudent, IsStackable = isStackable,
                MaxCombinedPercent = Math.Min(maxCombinedPercent, 100m), RenewalMode = renewalMode, RequiresHardshipDocumentation = requiresHardshipDocumentation,
            };
            foreach (var rule in rules ?? Array.Empty<EligibilityRuleInput>())
            {
                type.EligibilityRules.Add(new EligibilityRule { Kind = rule.Kind, Percent = rule.Percent, ChildOrdinal = rule.ChildOrdinal });
            }

            _db.DiscountTypes.Add(type);
            await _db.SaveChangesAsync(cancellationToken);
            return type;
        }

        public async Task UpdateTypeAsync(
            int discountTypeId, string nameAr, string nameEn, DiscountBasis basis, DiscountEligibilityMode eligibilityMode,
            int? feeCategoryId = null, DiscountComputationStage stage = DiscountComputationStage.BeforeVat, decimal? capAmountPerStudent = null,
            bool isStackable = true, decimal maxCombinedPercent = 100m, DiscountRenewalMode renewalMode = DiscountRenewalMode.ManualRegrant,
            bool requiresHardshipDocumentation = false, IReadOnlyList<EligibilityRuleInput>? rules = null, CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: a retired type is still editable — correcting its name is
            // exactly what someone does before putting it back in the catalog.
            var type = await _db.DiscountTypes.IgnoreQueryFilters().Include(t => t.EligibilityRules)
                .SingleOrDefaultAsync(t => t.Id == discountTypeId, cancellationToken)
                ?? throw new DiscountTypeNotFoundException(discountTypeId);

            type.NameAr = nameAr;
            type.NameEn = nameEn;
            type.Basis = basis;
            type.EligibilityMode = eligibilityMode;
            type.FeeCategoryId = feeCategoryId;
            type.ComputationStage = stage;
            type.CapAmountPerStudent = capAmountPerStudent;
            type.IsStackable = isStackable;
            type.MaxCombinedPercent = Math.Min(maxCombinedPercent, 100m);
            type.RenewalMode = renewalMode;
            type.RequiresHardshipDocumentation = requiresHardshipDocumentation;

            // RemoveRange, not Clear(): EligibilityRule is a separate table with a required
            // DiscountTypeId, so clearing the navigation makes EF try to null that FK and the
            // save dies on a conceptual null rather than deleting the rungs being replaced.
            _db.EligibilityRules.RemoveRange(type.EligibilityRules);
            foreach (var rule in rules ?? Array.Empty<EligibilityRuleInput>())
            {
                type.EligibilityRules.Add(new EligibilityRule { Kind = rule.Kind, Percent = rule.Percent, ChildOrdinal = rule.ChildOrdinal });
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetTypeActiveAsync(int discountTypeId, bool isActive, CancellationToken cancellationToken = default)
        {
            var type = await _db.DiscountTypes.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == discountTypeId, cancellationToken)
                ?? throw new DiscountTypeNotFoundException(discountTypeId);

            type.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ charge inputs (shared)

        private async Task<IReadOnlyList<DiscountAmountCalculator.ChargeInput>> LoadChargeInputsAsync(int studentId, int academicYearId, int? feeCategoryId, CancellationToken cancellationToken)
        {
            var charges = await _db.Charges
                .Where(c => c.StudentId == studentId && c.AcademicYearId == academicYearId && c.Status == ChargeStatus.Posted)
                .Where(c => feeCategoryId == null || c.FeeCategoryId == feeCategoryId)
                .OrderBy(c => c.PostedAtUtc).ThenBy(c => c.Id)
                .ToListAsync(cancellationToken);
            var ids = charges.Select(c => c.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize then sum in memory.
            var credits = (await _db.CreditNotes.Where(n => ids.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounts = (await _db.DiscountDocuments.Where(d => ids.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.Where(a => ids.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

            return charges.Select(c =>
            {
                var remaining = c.GrossAmount
                    - (credits.TryGetValue(c.Id, out var cr) ? cr : 0m)
                    - (discounts.TryGetValue(c.Id, out var ds) ? ds : 0m)
                    - (allocated.TryGetValue(c.Id, out var al) ? al : 0m);
                return new DiscountAmountCalculator.ChargeInput(c.Id, c.GrossAmount, c.VatRateSnapshot ?? 0m, remaining);
            }).ToList();
        }

        /// <summary>
        /// The same charge inputs as <see cref="LoadChargeInputsAsync(int, int, int?, CancellationToken)"/>,
        /// for a whole page of students at once and keyed by (student, year). The fee
        /// category is carried on the row rather than filtered in SQL, because each
        /// caller filters by a different type's category and one query beats a query
        /// per grant on a desk that shows five hundred of them.
        /// </summary>
        private async Task<Dictionary<(int StudentId, int AcademicYearId), List<CategorisedCharge>>> LoadChargeInputsAsync(
            IReadOnlyCollection<int> studentIds, IReadOnlyCollection<int> academicYearIds, CancellationToken cancellationToken)
        {
            var charges = await _db.Charges.AsNoTracking()
                .Where(c => studentIds.Contains(c.StudentId) && academicYearIds.Contains(c.AcademicYearId) && c.Status == ChargeStatus.Posted)
                .OrderBy(c => c.PostedAtUtc).ThenBy(c => c.Id)
                .ToListAsync(cancellationToken);
            var ids = charges.Select(c => c.Id).ToList();

            // EF Core's Sqlite provider can't translate Sum() over decimal - materialize then sum in memory.
            var credits = (await _db.CreditNotes.AsNoTracking().Where(n => ids.Contains(n.ChargeId)).Select(n => new { n.ChargeId, n.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var discounts = (await _db.DiscountDocuments.AsNoTracking().Where(d => ids.Contains(d.ChargeId)).Select(d => new { d.ChargeId, d.Amount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
            var allocated = (await _db.PaymentAllocations.AsNoTracking().Where(a => ids.Contains(a.ChargeId)).Select(a => new { a.ChargeId, a.AllocatedAmount }).ToListAsync(cancellationToken))
                .GroupBy(x => x.ChargeId).ToDictionary(g => g.Key, g => g.Sum(x => x.AllocatedAmount));

            var result = new Dictionary<(int, int), List<CategorisedCharge>>();
            foreach (var charge in charges)
            {
                var remaining = charge.GrossAmount
                    - (credits.TryGetValue(charge.Id, out var cr) ? cr : 0m)
                    - (discounts.TryGetValue(charge.Id, out var ds) ? ds : 0m)
                    - (allocated.TryGetValue(charge.Id, out var al) ? al : 0m);
                var key = (charge.StudentId, charge.AcademicYearId);
                if (!result.TryGetValue(key, out var list))
                {
                    result[key] = list = new List<CategorisedCharge>();
                }

                list.Add(new CategorisedCharge(
                    charge.FeeCategoryId,
                    new DiscountAmountCalculator.ChargeInput(charge.Id, charge.GrossAmount, charge.VatRateSnapshot ?? 0m, remaining)));
            }

            return result;
        }

        /// <summary>A charge input plus the category a discount type filters on. Posting order is the list's order.</summary>
        private sealed record CategorisedCharge(int FeeCategoryId, DiscountAmountCalculator.ChargeInput Input);

        /// <summary>
        /// BR-DIS-002's family truth in one pass: for each of these students, their
        /// position among the siblings they share a live guardian link with. Module
        /// 11's StudentGuardianLink is the single family record in this product,
        /// which is why the automatic run and the grant desk's preview both read it
        /// through here — two derivations of a child's ordinal would eventually
        /// disagree, and the disagreement would be about who gets the discount.
        /// </summary>
        private async Task<IReadOnlyDictionary<int, IReadOnlyList<SiblingLadderEvaluator.Position>>> LoadFamilyPositionsAsync(
            IReadOnlyCollection<int> enrolledStudentIds, CancellationToken cancellationToken)
        {
            var links = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.EffectiveToUtc == null && enrolledStudentIds.Contains(l.StudentId))
                .Select(l => new { l.ParentId, l.StudentId }).ToListAsync(cancellationToken);
            var dobs = await _db.Students.AsNoTracking()
                .Where(s => enrolledStudentIds.Contains(s.Id))
                .Select(s => new { s.Id, s.DateOfBirth })
                .ToDictionaryAsync(s => s.Id, s => s.DateOfBirth, cancellationToken);

            return SiblingLadderEvaluator.Positions(
                links.Select(l => new SiblingLadderEvaluator.FamilyLink(l.ParentId, l.StudentId, dobs[l.StudentId])).ToList());
        }

        private static decimal PercentEquivalent(DiscountType type, decimal basisValue, IReadOnlyList<DiscountAmountCalculator.ChargeInput> charges)
        {
            if (type.Basis == DiscountBasis.Percentage)
            {
                return basisValue;
            }

            var gross = charges.Sum(c => c.GrossAmount);
            return gross <= 0m ? 0m : Math.Round(basisValue / gross * 100m, 2, MidpointRounding.AwayFromZero);
        }

        private async Task EnsureStackableAsync(int studentId, int academicYearId, DiscountType type, decimal percentEquivalent, CancellationToken cancellationToken)
        {
            var existing = await (
                from g in _db.DiscountGrants
                join t in _db.DiscountTypes on g.DiscountTypeId equals t.Id
                where g.StudentId == studentId && g.AcademicYearId == academicYearId
                      && (g.Status == DiscountGrantStatus.Proposed || g.Status == DiscountGrantStatus.Approved)
                      && (t.FeeCategoryId == null || type.FeeCategoryId == null || t.FeeCategoryId == type.FeeCategoryId)
                select new { t.IsStackable, t.Basis, g.BasisValue, t.FeeCategoryId }).ToListAsync(cancellationToken);

            if (existing.Count == 0)
            {
                return;
            }

            var charges = await LoadChargeInputsAsync(studentId, academicYearId, type.FeeCategoryId, cancellationToken);
            var gross = charges.Sum(c => c.GrossAmount);
            var existingGrants = existing.Select(e => new StackingPolicyEvaluator.ExistingGrant(
                e.IsStackable,
                e.Basis == DiscountBasis.Percentage ? e.BasisValue : (gross <= 0m ? 0m : e.BasisValue / gross * 100m))).ToList();

            if (!StackingPolicyEvaluator.CanStack(type.IsStackable, percentEquivalent, existingGrants, type.MaxCombinedPercent))
            {
                throw new DiscountStackingViolationException(studentId);
            }
        }

        // ------------------------------------------------------------------ grants

        public async Task<DiscountGrant> ProposeManualGrantAsync(
            int studentId, int discountTypeId, decimal basisValue, string reason, int proposedByUserId,
            bool hasHardshipDocumentation = false, CancellationToken cancellationToken = default)
        {
            var type = await _db.DiscountTypes.SingleAsync(t => t.Id == discountTypeId, cancellationToken);
            if (type.RequiresHardshipDocumentation && !hasHardshipDocumentation)
            {
                throw new HardshipDocumentationRequiredException(discountTypeId);
            }

            var yearId = _workingYear.AcademicYearId;
            var charges = await LoadChargeInputsAsync(studentId, yearId, type.FeeCategoryId, cancellationToken);
            var percent = PercentEquivalent(type, basisValue, charges);
            await EnsureStackableAsync(studentId, yearId, type, percent, cancellationToken);

            var grant = new DiscountGrant
            {
                AcademicYearId = yearId, StudentId = studentId, DiscountTypeId = discountTypeId, Source = DiscountGrantSource.Manual,
                BasisValue = basisValue, Reason = reason, ProposedByUserId = proposedByUserId,
                RequiredTier = GrantApprovalRouter.Route(DiscountGrantSource.Manual, percent),
            };
            _db.DiscountGrants.Add(grant);
            await _db.SaveChangesAsync(cancellationToken);
            return grant;
        }

        public async Task<IReadOnlyList<DiscountGrant>> ProposeAutomaticGrantsAsync(int discountTypeId, int proposedByUserId, CancellationToken cancellationToken = default)
        {
            var type = await _db.DiscountTypes.Include(t => t.EligibilityRules).SingleAsync(t => t.Id == discountTypeId, cancellationToken);
            var yearId = _workingYear.AcademicYearId;
            var percentByStudent = new Dictionary<int, decimal>();

            var enrolledStudentIds = await _db.Enrollments
                .Where(e => e.AcademicYearId == yearId && e.Status == EnrollmentStatus.Active)
                .Select(e => e.StudentId).Distinct().ToListAsync(cancellationToken);

            var ladder = type.EligibilityRules.Where(r => r.Kind == EligibilityRuleKind.SiblingLadder && r.ChildOrdinal.HasValue)
                .Select(r => new SiblingLadderEvaluator.LadderStep(r.ChildOrdinal!.Value, r.Percent)).ToList();
            if (ladder.Count > 0)
            {
                var positions = await LoadFamilyPositionsAsync(enrolledStudentIds, cancellationToken);
                foreach (var (studentId, families) in positions)
                {
                    foreach (var position in families)
                    {
                        var percent = SiblingLadderEvaluator.Percent(position.Ordinal, ladder);
                        if (percent > 0m && (!percentByStudent.TryGetValue(studentId, out var current) || percent > current))
                        {
                            percentByStudent[studentId] = percent;
                        }
                    }
                }
            }

            var staff = type.EligibilityRules.FirstOrDefault(r => r.Kind == EligibilityRuleKind.Staff);
            if (staff != null)
            {
                // Parent↔Employee identity bridge = matching UserAccountId (the codebase's known bridging seam - flagged since E-304).
                var staffUserIds = await _db.Employees.Where(e => e.Status == EmployeeStatus.Active && e.UserAccountId != null).Select(e => e.UserAccountId!.Value).ToListAsync(cancellationToken);
                var staffParentIds = await _db.Parents.Where(p => p.UserAccountId != null && staffUserIds.Contains(p.UserAccountId.Value)).Select(p => p.Id).ToListAsync(cancellationToken);
                var staffChildren = await _db.StudentGuardianLinks
                    .Where(l => l.EffectiveToUtc == null && staffParentIds.Contains(l.ParentId) && enrolledStudentIds.Contains(l.StudentId))
                    .Select(l => l.StudentId).Distinct().ToListAsync(cancellationToken);
                foreach (var studentId in staffChildren)
                {
                    if (!percentByStudent.TryGetValue(studentId, out var current) || staff.Percent > current)
                    {
                        percentByStudent[studentId] = staff.Percent;
                    }
                }
            }

            var alreadyGranted = await _db.DiscountGrants
                .Where(g => g.DiscountTypeId == discountTypeId && g.AcademicYearId == yearId && g.Status != DiscountGrantStatus.Rejected)
                .Select(g => g.StudentId).ToListAsync(cancellationToken);

            var proposed = new List<DiscountGrant>();
            foreach (var (studentId, percent) in percentByStudent.Where(kv => !alreadyGranted.Contains(kv.Key)).OrderBy(kv => kv.Key))
            {
                var grant = new DiscountGrant
                {
                    AcademicYearId = yearId, StudentId = studentId, DiscountTypeId = discountTypeId, Source = DiscountGrantSource.Automatic,
                    BasisValue = percent, Reason = "automatic eligibility (BR-DIS-002)", ProposedByUserId = proposedByUserId,
                    RequiredTier = ApprovalTier.FinanceManager,
                };
                _db.DiscountGrants.Add(grant);
                proposed.Add(grant);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return proposed;
        }

        public async Task<IReadOnlyDictionary<int, GrantPreview>> PreviewGrantsAsync(IReadOnlyList<int> discountGrantIds, CancellationToken cancellationToken = default)
        {
            var result = new Dictionary<int, GrantPreview>();
            if (discountGrantIds.Count == 0)
            {
                return result;
            }

            var grants = await _db.DiscountGrants.AsNoTracking()
                .Where(g => discountGrantIds.Contains(g.Id)).ToListAsync(cancellationToken);
            if (grants.Count == 0)
            {
                return result;
            }

            // IgnoreQueryFilters: a grant keeps pointing at its type after the school
            // retires it, and the desk still has to show what that grant is worth.
            var typeIds = grants.Select(g => g.DiscountTypeId).Distinct().ToList();
            var types = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking().Include(t => t.EligibilityRules)
                .Where(t => t.SchoolId == _db.CurrentSchoolId && typeIds.Contains(t.Id))
                .ToDictionaryAsync(t => t.Id, cancellationToken);

            var studentIds = grants.Select(g => g.StudentId).Distinct().ToList();
            var yearIds = grants.Select(g => g.AcademicYearId).Distinct().ToList();
            var chargesByStudentYear = await LoadChargeInputsAsync(studentIds, yearIds, cancellationToken);

            // The family is only loaded when some type on the page actually has a
            // ladder — the guardian links of a whole year are not worth a query for a
            // desk showing nothing but hardship grants.
            var laddered = grants.Any(g => types.TryGetValue(g.DiscountTypeId, out var t)
                && t.EligibilityRules.Any(r => r.Kind == EligibilityRuleKind.SiblingLadder && r.ChildOrdinal.HasValue));
            IReadOnlyDictionary<int, IReadOnlyList<SiblingLadderEvaluator.Position>> positions =
                new Dictionary<int, IReadOnlyList<SiblingLadderEvaluator.Position>>();
            if (laddered)
            {
                var enrolled = await _db.Enrollments.AsNoTracking()
                    .Where(e => yearIds.Contains(e.AcademicYearId) && e.Status == EnrollmentStatus.Active)
                    .Select(e => e.StudentId).Distinct().ToListAsync(cancellationToken);
                positions = await LoadFamilyPositionsAsync(enrolled, cancellationToken);
            }

            foreach (var grant in grants)
            {
                if (!types.TryGetValue(grant.DiscountTypeId, out var type))
                {
                    continue;
                }

                var all = chargesByStudentYear.TryGetValue((grant.StudentId, grant.AcademicYearId), out var rows)
                    ? rows
                    : new List<CategorisedCharge>();
                var applicable = all
                    .Where(r => type.FeeCategoryId == null || r.FeeCategoryId == type.FeeCategoryId)
                    .Select(r => r.Input).ToList();

                decimal? expected = grant.Status == DiscountGrantStatus.Proposed
                    ? DiscountAmountCalculator
                        .Compute(type.Basis, type.ComputationStage, grant.BasisValue, type.CapAmountPerStudent, applicable)
                        .Sum(a => a.Amount)
                    : null;

                result[grant.Id] = new GrantPreview(
                    applicable.Sum(c => c.GrossAmount),
                    applicable.Sum(c => c.Remaining),
                    expected,
                    SiblingContext(type, grant.StudentId, positions));
            }

            return result;
        }

        /// <summary>
        /// The ladder's own reading of a student, for the desk to show beside the
        /// grant: which child of the family they are, and what the rung gives that
        /// ordinal. Null when the type has no ladder, or when the student sits in no
        /// family the guardian links know about. A child linked to two guardians is a
        /// sibling twice over, so the family that pays best wins — the same choice
        /// <see cref="ProposeAutomaticGrantsAsync"/> makes when it takes the maximum.
        /// </summary>
        private static SiblingPosition? SiblingContext(
            DiscountType type, int studentId, IReadOnlyDictionary<int, IReadOnlyList<SiblingLadderEvaluator.Position>> positions)
        {
            var ladder = type.EligibilityRules
                .Where(r => r.Kind == EligibilityRuleKind.SiblingLadder && r.ChildOrdinal.HasValue)
                .Select(r => new SiblingLadderEvaluator.LadderStep(r.ChildOrdinal!.Value, r.Percent)).ToList();
            if (ladder.Count == 0 || !positions.TryGetValue(studentId, out var families) || families.Count == 0)
            {
                return null;
            }

            return families
                .Select(p => new SiblingPosition(p.Ordinal, p.SiblingCount, SiblingLadderEvaluator.Percent(p.Ordinal, ladder)))
                .OrderByDescending(p => p.LadderPercent).ThenByDescending(p => p.SiblingCount).ThenBy(p => p.Ordinal)
                .First();
        }

        public async Task<ScholarshipProgram> DefineScholarshipProgramAsync(string nameAr, string nameEn, int discountTypeId, int? maxAwards, decimal? maxTotalAmount, CancellationToken cancellationToken = default)
        {
            var program = new ScholarshipProgram
            {
                AcademicYearId = _workingYear.AcademicYearId, NameAr = nameAr, NameEn = nameEn, DiscountTypeId = discountTypeId,
                MaxAwards = maxAwards, MaxTotalAmount = maxTotalAmount,
            };
            _db.ScholarshipPrograms.Add(program);
            await _db.SaveChangesAsync(cancellationToken);
            return program;
        }

        public async Task<DiscountGrant> NominateForScholarshipAsync(int studentId, int scholarshipProgramId, decimal basisValue, string reason, int proposedByUserId, string? sponsorNote = null, CancellationToken cancellationToken = default)
        {
            var program = await _db.ScholarshipPrograms.SingleAsync(p => p.Id == scholarshipProgramId, cancellationToken);
            var type = await _db.DiscountTypes.SingleAsync(t => t.Id == program.DiscountTypeId, cancellationToken);
            var charges = await LoadChargeInputsAsync(studentId, program.AcademicYearId, type.FeeCategoryId, cancellationToken);
            await EnsureStackableAsync(studentId, program.AcademicYearId, type, PercentEquivalent(type, basisValue, charges), cancellationToken);

            var grant = new DiscountGrant
            {
                AcademicYearId = program.AcademicYearId, StudentId = studentId, DiscountTypeId = type.Id, Source = DiscountGrantSource.Scholarship,
                BasisValue = basisValue, Reason = reason, ProposedByUserId = proposedByUserId, ScholarshipProgramId = scholarshipProgramId,
                SponsorNote = sponsorNote, RequiredTier = ApprovalTier.Committee,
            };
            _db.DiscountGrants.Add(grant);
            await _db.SaveChangesAsync(cancellationToken);
            return grant;
        }

        public async Task ApproveGrantAsync(int discountGrantId, int approvedByUserId, string? envelopeOverrideReason = null, CancellationToken cancellationToken = default)
        {
            var grant = await _db.DiscountGrants.SingleAsync(g => g.Id == discountGrantId, cancellationToken);
            if (grant.Status != DiscountGrantStatus.Proposed)
            {
                throw new InvalidDiscountGrantStateException(discountGrantId, DiscountGrantStatus.Proposed);
            }

            var type = await _db.DiscountTypes.SingleAsync(t => t.Id == grant.DiscountTypeId, cancellationToken);
            var charges = await LoadChargeInputsAsync(grant.StudentId, grant.AcademicYearId, type.FeeCategoryId, cancellationToken);
            var amounts = DiscountAmountCalculator.Compute(type.Basis, type.ComputationStage, grant.BasisValue, type.CapAmountPerStudent, charges);
            var total = amounts.Sum(a => a.Amount);

            if (grant.ScholarshipProgramId.HasValue)
            {
                var program = await _db.ScholarshipPrograms.SingleAsync(p => p.Id == grant.ScholarshipProgramId.Value, cancellationToken);
                var approved = await _db.DiscountGrants
                    .Where(g => g.ScholarshipProgramId == program.Id && g.Status == DiscountGrantStatus.Approved)
                    .Select(g => g.AppliedAmount).ToListAsync(cancellationToken);
                if (!EnvelopeEvaluator.HasRoom(program.MaxAwards, program.MaxTotalAmount, approved.Count, approved.Sum(), total))
                {
                    if (string.IsNullOrWhiteSpace(envelopeOverrideReason))
                    {
                        throw new ScholarshipEnvelopeExhaustedException(program.Id);
                    }

                    _audit.Reason = envelopeOverrideReason;
                    grant.EnvelopeOverrideReason = envelopeOverrideReason;
                }
            }

            // BR-DIS-005: application = numbered discount documents against the charges, atomic with the approval.
            foreach (var amount in amounts)
            {
                _db.DiscountDocuments.Add(new DiscountDocument
                {
                    DiscountGrantId = grant.Id, ChargeId = amount.ChargeId, Amount = amount.Amount,
                    DocumentNo = await _numberIssuer.IssueAsync(DiscountDocumentSeriesCode, cancellationToken), IssuedAtUtc = _clock.UtcNow,
                });
            }

            grant.AppliedAmount = total;
            grant.Status = DiscountGrantStatus.Approved;
            grant.ApprovedByUserId = approvedByUserId;
            grant.ApprovedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);

            // BR-DIS-005 -> BR-INS-003: recompute forward installments of any schedule that carries these charges.
            var chargeIds = amounts.Select(a => a.ChargeId).ToList();
            var byAssignment = await (
                from line in _db.InstallmentChargeLines
                join inst in _db.Installments on line.InstallmentId equals inst.Id
                where chargeIds.Contains(line.ChargeId)
                select new { inst.PlanAssignmentId, line.ChargeId }).Distinct().ToListAsync(cancellationToken);
            foreach (var group in byAssignment.GroupBy(x => x.PlanAssignmentId))
            {
                var reduction = amounts.Where(a => group.Any(g => g.ChargeId == a.ChargeId)).Sum(a => a.Amount);
                if (reduction > 0m)
                {
                    await _installments.ReduceScheduleAsync(group.Key, reduction, $"discount {type.NameEn} (grant {grant.Id})", cancellationToken);
                }
            }
        }

        public async Task ApproveGrantsAsync(IReadOnlyList<int> discountGrantIds, int approvedByUserId, CancellationToken cancellationToken = default)
        {
            foreach (var id in discountGrantIds)
            {
                await ApproveGrantAsync(id, approvedByUserId, envelopeOverrideReason: null, cancellationToken);
            }
        }

        public async Task RejectGrantAsync(int discountGrantId, int decidedByUserId, string reason, CancellationToken cancellationToken = default)
        {
            var grant = await _db.DiscountGrants.SingleAsync(g => g.Id == discountGrantId, cancellationToken);
            if (grant.Status != DiscountGrantStatus.Proposed)
            {
                throw new InvalidDiscountGrantStateException(discountGrantId, DiscountGrantStatus.Proposed);
            }

            _audit.Reason = reason;
            grant.Status = DiscountGrantStatus.Rejected;
            grant.ApprovedByUserId = decidedByUserId;
            grant.ApprovedAtUtc = _clock.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<decimal> GetGrantPercentEquivalentAsync(int discountGrantId, CancellationToken cancellationToken = default)
        {
            var grant = await _db.DiscountGrants.AsNoTracking().SingleAsync(g => g.Id == discountGrantId, cancellationToken);

            // IgnoreQueryFilters: a grant keeps pointing at its type after the type is
            // retired, and the chain that decides who signs must not fall over the day
            // somebody stops offering that discount to new students.
            var type = await _db.DiscountTypes.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.SchoolId == _db.CurrentSchoolId)
                .SingleAsync(t => t.Id == grant.DiscountTypeId, cancellationToken);

            var charges = await LoadChargeInputsAsync(grant.StudentId, grant.AcademicYearId, type.FeeCategoryId, cancellationToken);
            return PercentEquivalent(type, grant.BasisValue, charges);
        }

        public async Task RevokeGrantAsync(int discountGrantId, DateTime effectiveDate, string reason, bool clawBack = false, CancellationToken cancellationToken = default)
        {
            var grant = await _db.DiscountGrants.SingleAsync(g => g.Id == discountGrantId, cancellationToken);
            if (grant.Status != DiscountGrantStatus.Approved)
            {
                throw new InvalidDiscountGrantStateException(discountGrantId, DiscountGrantStatus.Approved);
            }

            if (effectiveDate.Date < _clock.UtcNow.Date)
            {
                throw new RevocationDateInPastException(effectiveDate);
            }

            _audit.Reason = reason;
            grant.Status = DiscountGrantStatus.Revoked;
            grant.RevokedEffectiveDate = effectiveDate.Date;
            grant.RevokedReason = reason;
            await _db.SaveChangesAsync(cancellationToken);

            if (!clawBack || grant.AppliedAmount <= 0m)
            {
                return; // BR-DIS-008 default policy: forgive the past, nothing else changes.
            }

            var year = await _db.AcademicYears.SingleAsync(y => y.Id == grant.AcademicYearId, cancellationToken);
            var amount = ClawbackCalculator.Amount(grant.AppliedAmount, effectiveDate, year.StartDate, year.EndDate);
            if (amount <= 0m)
            {
                return;
            }

            var firstDoc = await _db.DiscountDocuments.Where(d => d.DiscountGrantId == grant.Id).OrderBy(d => d.Id).FirstAsync(cancellationToken);
            var charge = await _db.Charges.SingleAsync(c => c.Id == firstDoc.ChargeId, cancellationToken);
            // The clawed-back figure is gross — DiscountAmountCalculator works off GrossAmount — so it is posted
            // gross at the discounted charge's own snapshot rate. Passing it as a net amount charged the family VAT
            // on VAT (S8 gap G-12), and re-reading the category's current rate would recover tax at a rate that was
            // never granted.
            await _feeAdmin.PostManualGrossChargeAsync(
                grant.StudentId, charge.PayerId, charge.FeeCategoryId, amount, charge.VatRateSnapshot, cancellationToken);
        }

        // ------------------------------------------------------------------ waivers

        public async Task<Waiver> ProposeWaiverAsync(int chargeId, WaiverKind kind, decimal amount, string reason, int proposedByUserId, decimal principalThreshold = 500m, CancellationToken cancellationToken = default)
        {
            var charge = await _db.Charges.SingleAsync(c => c.Id == chargeId, cancellationToken);
            var input = (await LoadChargeInputsAsync(charge.StudentId, charge.AcademicYearId, charge.FeeCategoryId, cancellationToken)).Single(c => c.ChargeId == chargeId);
            if (amount <= 0m || amount > input.Remaining)
            {
                throw new WaiverExceedsChargeRemainderException(chargeId);
            }

            var waiver = new Waiver
            {
                ChargeId = chargeId, Kind = kind, Amount = amount, Reason = reason, ProposedByUserId = proposedByUserId,
                RequiredTier = WaiverApprovalRouter.Route(amount, principalThreshold),
            };
            _db.Waivers.Add(waiver);
            await _db.SaveChangesAsync(cancellationToken);
            return waiver;
        }

        public async Task DecideWaiverAsync(int waiverId, bool approve, int decidedByUserId, CancellationToken cancellationToken = default)
        {
            var waiver = await _db.Waivers.SingleAsync(w => w.Id == waiverId, cancellationToken);
            if (waiver.Status != WaiverStatus.Proposed)
            {
                throw new WaiverNotPendingException(waiverId);
            }

            waiver.DecidedByUserId = decidedByUserId;
            waiver.DecidedAtUtc = _clock.UtcNow;
            if (!approve)
            {
                waiver.Status = WaiverStatus.Rejected;
                await _db.SaveChangesAsync(cancellationToken);
                return;
            }

            var creditNote = await _feeAdmin.IssueCreditNoteAsync(waiver.ChargeId, waiver.Amount, $"Waiver ({waiver.Kind}): {waiver.Reason}", cancellationToken);
            waiver.CreditNoteId = creditNote.Id;
            waiver.Status = WaiverStatus.Approved;
            await _db.SaveChangesAsync(cancellationToken);
        }

        // ------------------------------------------------------------------ renewal

        public async Task<IReadOnlyList<RenewalQueueItem>> BuildRenewalQueueAsync(int fromAcademicYearId, int toAcademicYearId, CancellationToken cancellationToken = default)
        {
            var candidates = await (
                from g in _db.DiscountGrants
                join t in _db.DiscountTypes on g.DiscountTypeId equals t.Id
                where g.AcademicYearId == fromAcademicYearId && g.Status == DiscountGrantStatus.Approved
                      && (t.RenewalMode == DiscountRenewalMode.ManualRegrant || t.EligibilityMode != DiscountEligibilityMode.Automatic)
                select g.Id).ToListAsync(cancellationToken);
            var queued = await _db.RenewalQueueItems.Where(i => i.NewAcademicYearId == toAcademicYearId).Select(i => i.PriorGrantId).ToListAsync(cancellationToken);

            var items = new List<RenewalQueueItem>();
            foreach (var grantId in candidates.Except(queued))
            {
                var item = new RenewalQueueItem { PriorGrantId = grantId, NewAcademicYearId = toAcademicYearId };
                _db.RenewalQueueItems.Add(item);
                items.Add(item);
            }

            await _db.SaveChangesAsync(cancellationToken);
            return items;
        }

        public async Task DecideRenewalAsync(int renewalQueueItemId, RenewalDecision decision, int decidedByUserId, decimal? adjustedBasisValue = null, CancellationToken cancellationToken = default)
        {
            var item = await _db.RenewalQueueItems.SingleAsync(i => i.Id == renewalQueueItemId, cancellationToken);
            if (item.Decision != RenewalDecision.Pending || decision == RenewalDecision.Pending)
            {
                throw new RenewalItemNotPendingException(renewalQueueItemId);
            }

            item.Decision = decision;
            item.DecidedByUserId = decidedByUserId;
            item.DecidedAtUtc = _clock.UtcNow;
            item.AdjustedBasisValue = decision == RenewalDecision.Adjusted ? adjustedBasisValue : null;

            if (decision != RenewalDecision.Dropped)
            {
                var prior = await _db.DiscountGrants.SingleAsync(g => g.Id == item.PriorGrantId, cancellationToken);
                var type = await _db.DiscountTypes.SingleAsync(t => t.Id == prior.DiscountTypeId, cancellationToken);
                var basis = decision == RenewalDecision.Adjusted && adjustedBasisValue.HasValue ? adjustedBasisValue.Value : prior.BasisValue;
                var charges = await LoadChargeInputsAsync(prior.StudentId, item.NewAcademicYearId, type.FeeCategoryId, cancellationToken);
                var renewed = new DiscountGrant
                {
                    AcademicYearId = item.NewAcademicYearId, StudentId = prior.StudentId, DiscountTypeId = prior.DiscountTypeId,
                    Source = DiscountGrantSource.Renewal, BasisValue = basis, Reason = $"renewal of grant {prior.Id}", ProposedByUserId = decidedByUserId,
                    ScholarshipProgramId = prior.ScholarshipProgramId, SponsorNote = prior.SponsorNote, RenewedFromGrantId = prior.Id,
                    RequiredTier = GrantApprovalRouter.Route(prior.Source == DiscountGrantSource.Scholarship ? DiscountGrantSource.Scholarship : DiscountGrantSource.Manual, PercentEquivalent(type, basis, charges)),
                };
                _db.DiscountGrants.Add(renewed);
                await _db.SaveChangesAsync(cancellationToken);
                item.NewGrantId = renewed.Id;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
