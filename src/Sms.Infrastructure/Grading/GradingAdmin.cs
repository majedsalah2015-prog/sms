using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Grading;
using Sms.Domain.Grading;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Grading
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class GradingAdmin : IGradingAdmin
    {
        private readonly AppDbContext _db;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;

        public GradingAdmin(AppDbContext db, IClock clock, IAuditContext audit)
        {
            _db = db;
            _clock = clock;
            _audit = audit;
        }

        public async Task<GradingScale> DefineScaleAsync(
            int stageId, string nameAr, string nameEn, int? curriculumLookupValueId = null, int? academicYearId = null, CancellationToken cancellationToken = default)
        {
            // Year-versioned (BR-GRA-001): default to the Active year rather than leaving the IYearScoped column at 0.
            var yearId = academicYearId ?? await _db.AcademicYears
                .Where(y => y.Status == Sms.Domain.Schools.AcademicYearStatus.Active)
                .Select(y => (int?)y.Id)
                .FirstOrDefaultAsync(cancellationToken) ?? 0;

            var scale = new GradingScale
            {
                AcademicYearId = yearId,
                StageId = stageId,
                CurriculumLookupValueId = curriculumLookupValueId,
                NameAr = nameAr,
                NameEn = nameEn,
            };
            _db.GradingScales.Add(scale);

            await _db.SaveChangesAsync(cancellationToken);
            return scale;
        }

        public async Task<ScaleBand> AddScaleBandAsync(
            int gradingScaleId, decimal minPercent, decimal maxPercent, string bandCode, string labelAr, string labelEn,
            bool isPassing, int sortOrder, decimal? gpaPoints = null, CancellationToken cancellationToken = default)
        {
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == gradingScaleId, cancellationToken);
            if (scale.IsLocked)
            {
                throw new GradingScaleLockedException(gradingScaleId);
            }

            var band = new ScaleBand
            {
                GradingScaleId = gradingScaleId,
                MinPercent = minPercent,
                MaxPercent = maxPercent,
                BandCode = bandCode,
                LabelAr = labelAr,
                LabelEn = labelEn,
                IsPassing = isPassing,
                SortOrder = sortOrder,
                GpaPoints = gpaPoints,
            };
            _db.ScaleBands.Add(band);

            await _db.SaveChangesAsync(cancellationToken);
            return band;
        }

        public async Task LockScaleAsync(int gradingScaleId, CancellationToken cancellationToken = default)
        {
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == gradingScaleId, cancellationToken);
            scale.IsLocked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Blueprint> DefineBlueprintAsync(
            int curriculumOfferingId, int termId, int gradingScaleId, bool redistributeWeightOnExemption = false,
            CancellationToken cancellationToken = default)
        {
            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == curriculumOfferingId, cancellationToken);

            var blueprint = new Blueprint
            {
                AcademicYearId = offering.AcademicYearId,
                CurriculumOfferingId = curriculumOfferingId,
                TermId = termId,
                GradingScaleId = gradingScaleId,
                RedistributeWeightOnExemption = redistributeWeightOnExemption,
            };
            _db.Blueprints.Add(blueprint);

            await _db.SaveChangesAsync(cancellationToken);
            return blueprint;
        }

        public async Task<BlueprintComponent> AddBlueprintComponentAsync(
            int blueprintId, string nameAr, string nameEn, decimal weight, decimal maxScore, CancellationToken cancellationToken = default)
        {
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == blueprintId, cancellationToken);
            if (blueprint.IsLocked)
            {
                throw new BlueprintLockedException(blueprintId);
            }

            var component = new BlueprintComponent
            {
                BlueprintId = blueprintId,
                NameAr = nameAr,
                NameEn = nameEn,
                Weight = weight,
                MaxScore = maxScore,
            };
            _db.BlueprintComponents.Add(component);

            await _db.SaveChangesAsync(cancellationToken);
            return component;
        }

        public async Task LockBlueprintAsync(int blueprintId, CancellationToken cancellationToken = default)
        {
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == blueprintId, cancellationToken);
            var weights = await _db.BlueprintComponents.Where(c => c.BlueprintId == blueprintId).Select(c => c.Weight).ToListAsync(cancellationToken);

            if (!BlueprintWeightValidator.SumsTo100(weights))
            {
                throw new BlueprintWeightMismatchException(blueprintId, weights.Sum());
            }

            blueprint.IsLocked = true;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Marksheet> CreateMarksheetAsync(int blueprintId, int sectionId, CancellationToken cancellationToken = default)
        {
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == blueprintId, cancellationToken);
            if (!blueprint.IsLocked)
            {
                throw new BlueprintNotFinalizedException(blueprintId);
            }

            var marksheet = new Marksheet
            {
                AcademicYearId = blueprint.AcademicYearId,
                BlueprintId = blueprintId,
                SectionId = sectionId,
                Status = MarksheetStatus.Draft,
            };
            _db.Marksheets.Add(marksheet);
            await _db.SaveChangesAsync(cancellationToken);

            var componentIds = await _db.BlueprintComponents.Where(c => c.BlueprintId == blueprintId).Select(c => c.Id).ToListAsync(cancellationToken);
            var enrollmentIds = await _db.SectionMemberships
                .Where(m => m.SectionId == sectionId && m.EffectiveToUtc == null)
                .Select(m => m.EnrollmentId)
                .ToListAsync(cancellationToken);

            foreach (var enrollmentId in enrollmentIds)
            {
                foreach (var componentId in componentIds)
                {
                    _db.MarkEntries.Add(new MarkEntry
                    {
                        MarksheetId = marksheet.Id,
                        BlueprintComponentId = componentId,
                        EnrollmentId = enrollmentId,
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            return marksheet;
        }

        public async Task EnterMarkAsync(
            int marksheetId, int blueprintComponentId, int enrollmentId, decimal? score, bool isAbsent, bool isExempt,
            CancellationToken cancellationToken = default)
        {
            var entry = await _db.MarkEntries.SingleAsync(
                e => e.MarksheetId == marksheetId && e.BlueprintComponentId == blueprintComponentId && e.EnrollmentId == enrollmentId,
                cancellationToken);

            entry.Score = score;
            entry.IsAbsent = isAbsent;
            entry.IsExempt = isExempt;

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task ChangeMarksheetStatusAsync(int marksheetId, MarksheetStatus newStatus, CancellationToken cancellationToken = default)
        {
            var marksheet = await _db.Marksheets.SingleAsync(m => m.Id == marksheetId, cancellationToken);
            if (!MarksheetStatusTransitions.CanTransition(marksheet.Status, newStatus))
            {
                throw new InvalidMarksheetStatusTransitionException(marksheet.Status, newStatus);
            }

            var entries = await _db.MarkEntries.Where(e => e.MarksheetId == marksheetId).ToListAsync(cancellationToken);
            var unresolved = entries.Count(e => e.Score == null && !e.IsAbsent && !e.IsExempt);
            if (newStatus == MarksheetStatus.Published && unresolved > 0)
            {
                throw new UnresolvedMarkEntriesException(marksheetId, unresolved);
            }

            marksheet.Status = newStatus;
            switch (newStatus)
            {
                case MarksheetStatus.Submitted:
                    marksheet.SubmittedAtUtc = _clock.UtcNow;
                    break;
                case MarksheetStatus.HoDReviewed:
                    marksheet.ReviewedAtUtc = _clock.UtcNow;
                    break;
                case MarksheetStatus.Approved:
                    marksheet.ApprovedAtUtc = _clock.UtcNow;
                    break;
                case MarksheetStatus.Published:
                    marksheet.PublishedAtUtc = _clock.UtcNow;
                    await PublishResultsAsync(marksheet, entries, cancellationToken);
                    break;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task CorrectPublishedMarksheetAsync(int marksheetId, string reason, CancellationToken cancellationToken = default)
        {
            var marksheet = await _db.Marksheets.SingleAsync(m => m.Id == marksheetId, cancellationToken);
            if (!MarksheetStatusTransitions.CanTransition(marksheet.Status, MarksheetStatus.Draft))
            {
                throw new InvalidMarksheetStatusTransitionException(marksheet.Status, MarksheetStatus.Draft);
            }

            _audit.Reason = reason;
            marksheet.Status = MarksheetStatus.Draft;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<PromotionCriteria> DefinePromotionCriteriaAsync(
            int gradeYearProfileId, decimal overallPassMark, int maxFailedSubjectsForPromotion, CancellationToken cancellationToken = default)
        {
            var criteria = await _db.PromotionCriteria.SingleOrDefaultAsync(c => c.GradeYearProfileId == gradeYearProfileId, cancellationToken);
            if (criteria == null)
            {
                criteria = new PromotionCriteria { GradeYearProfileId = gradeYearProfileId };
                _db.PromotionCriteria.Add(criteria);
            }

            criteria.OverallPassMark = overallPassMark;
            criteria.MaxFailedSubjectsForPromotion = maxFailedSubjectsForPromotion;

            await _db.SaveChangesAsync(cancellationToken);
            return criteria;
        }

        public async Task<YearResult> ComputeYearResultAsync(
            int enrollmentId, int academicYearId, int gradeYearProfileId, CancellationToken cancellationToken = default)
        {
            var criteria = await _db.PromotionCriteria.SingleAsync(c => c.GradeYearProfileId == gradeYearProfileId, cancellationToken);

            // Latest TermResult per offering stands in for full term-weighted year aggregation (BR-GRA-003's
            // configurable term-weight scheme isn't implemented in this slice - see IGradingAdmin's doc comment).
            var results = await _db.TermResults
                .Where(r => r.EnrollmentId == enrollmentId && r.AcademicYearId == academicYearId)
                .OrderByDescending(r => r.PublishedAtUtc)
                .ToListAsync(cancellationToken);
            var latestPerOffering = results.GroupBy(r => r.CurriculumOfferingId).Select(g => g.First()).ToList();

            var offeringIds = latestPerOffering.Select(r => r.CurriculumOfferingId).ToList();
            var weightByOffering = await _db.CurriculumOfferings
                .Where(o => offeringIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.GpaWeight, cancellationToken);

            var bandIds = latestPerOffering.Where(r => r.ScaleBandId.HasValue).Select(r => r.ScaleBandId!.Value).Distinct().ToList();
            var bandsById = await _db.ScaleBands.Where(b => bandIds.Contains(b.Id)).ToDictionaryAsync(b => b.Id, cancellationToken);

            var gpaInputs = latestPerOffering.Select(r =>
            {
                var gpaPoints = r.ScaleBandId.HasValue && bandsById.TryGetValue(r.ScaleBandId.Value, out var band) ? band.GpaPoints : null;
                return new GpaCalculator.OfferingResult(gpaPoints, weightByOffering[r.CurriculumOfferingId]);
            }).ToList();
            var gpa = GpaCalculator.Calculate(gpaInputs);

            var failedSubjectCount = latestPerOffering.Count(r =>
                r.ScaleBandId.HasValue && bandsById.TryGetValue(r.ScaleBandId.Value, out var band) && !band.IsPassing);

            var totalWeight = latestPerOffering.Sum(r => weightByOffering[r.CurriculumOfferingId]);
            var overallPercent = totalWeight > 0
                ? latestPerOffering.Sum(r => r.ScorePercent * weightByOffering[r.CurriculumOfferingId]) / totalWeight
                : 0m;
            var overallPassed = overallPercent >= criteria.OverallPassMark;

            var outcome = PromotionEvaluator.Evaluate(failedSubjectCount, criteria.MaxFailedSubjectsForPromotion, overallPassed);

            var yearResult = await _db.YearResults.SingleOrDefaultAsync(
                r => r.EnrollmentId == enrollmentId && r.AcademicYearId == academicYearId, cancellationToken);
            if (yearResult == null)
            {
                yearResult = new YearResult { AcademicYearId = academicYearId, EnrollmentId = enrollmentId };
                _db.YearResults.Add(yearResult);
            }

            yearResult.Gpa = gpa;
            yearResult.FailedSubjectCount = failedSubjectCount;
            yearResult.PromotionOutcome = outcome;
            yearResult.ComputedAtUtc = _clock.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return yearResult;
        }

        // ---- E-302 screen support --------------------------------------------------------------

        public async Task<GradingScale> UpdateScaleAsync(int gradingScaleId, string nameAr, string nameEn, CancellationToken cancellationToken = default)
        {
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == gradingScaleId, cancellationToken);
            scale.NameAr = nameAr;
            scale.NameEn = nameEn;
            await _db.SaveChangesAsync(cancellationToken);
            return scale;
        }

        public async Task DeleteScaleAsync(int gradingScaleId, CancellationToken cancellationToken = default)
        {
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == gradingScaleId, cancellationToken);
            if (scale.IsLocked)
            {
                throw new GradingScaleLockedException(gradingScaleId);
            }
            var blueprintCount = await _db.Blueprints.CountAsync(b => b.GradingScaleId == gradingScaleId, cancellationToken);
            if (blueprintCount > 0)
            {
                throw new GradingScaleInUseException(gradingScaleId, blueprintCount);
            }

            var bands = await _db.ScaleBands.Where(b => b.GradingScaleId == gradingScaleId).ToListAsync(cancellationToken);
            _db.ScaleBands.RemoveRange(bands);
            _db.GradingScales.Remove(scale);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<ScaleBand> UpdateScaleBandAsync(
            int scaleBandId, decimal minPercent, decimal maxPercent, string bandCode, string labelAr, string labelEn,
            bool isPassing, int sortOrder, decimal? gpaPoints = null, CancellationToken cancellationToken = default)
        {
            var band = await _db.ScaleBands.SingleAsync(b => b.Id == scaleBandId, cancellationToken);
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == band.GradingScaleId, cancellationToken);
            if (scale.IsLocked)
            {
                throw new GradingScaleLockedException(scale.Id);
            }

            band.MinPercent = minPercent;
            band.MaxPercent = maxPercent;
            band.BandCode = bandCode;
            band.LabelAr = labelAr;
            band.LabelEn = labelEn;
            band.IsPassing = isPassing;
            band.SortOrder = sortOrder;
            band.GpaPoints = gpaPoints;
            await _db.SaveChangesAsync(cancellationToken);
            return band;
        }

        public async Task RemoveScaleBandAsync(int scaleBandId, CancellationToken cancellationToken = default)
        {
            var band = await _db.ScaleBands.SingleAsync(b => b.Id == scaleBandId, cancellationToken);
            var scale = await _db.GradingScales.SingleAsync(s => s.Id == band.GradingScaleId, cancellationToken);
            if (scale.IsLocked)
            {
                throw new GradingScaleLockedException(scale.Id);
            }

            _db.ScaleBands.Remove(band);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<BlueprintComponent> UpdateBlueprintComponentAsync(
            int blueprintComponentId, string nameAr, string nameEn, decimal weight, decimal maxScore, CancellationToken cancellationToken = default)
        {
            var component = await _db.BlueprintComponents.SingleAsync(c => c.Id == blueprintComponentId, cancellationToken);
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == component.BlueprintId, cancellationToken);
            if (blueprint.IsLocked)
            {
                throw new BlueprintLockedException(blueprint.Id);
            }

            component.NameAr = nameAr;
            component.NameEn = nameEn;
            component.Weight = weight;
            component.MaxScore = maxScore;
            await _db.SaveChangesAsync(cancellationToken);
            return component;
        }

        public async Task RemoveBlueprintComponentAsync(int blueprintComponentId, CancellationToken cancellationToken = default)
        {
            var component = await _db.BlueprintComponents.SingleAsync(c => c.Id == blueprintComponentId, cancellationToken);
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == component.BlueprintId, cancellationToken);
            if (blueprint.IsLocked)
            {
                throw new BlueprintLockedException(blueprint.Id);
            }

            _db.BlueprintComponents.Remove(component);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteBlueprintAsync(int blueprintId, CancellationToken cancellationToken = default)
        {
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == blueprintId, cancellationToken);
            if (blueprint.IsLocked)
            {
                throw new BlueprintLockedException(blueprintId);
            }
            var marksheetCount = await _db.Marksheets.CountAsync(m => m.BlueprintId == blueprintId, cancellationToken);
            if (marksheetCount > 0)
            {
                throw new BlueprintInUseException(blueprintId, marksheetCount);
            }

            var components = await _db.BlueprintComponents.Where(c => c.BlueprintId == blueprintId).ToListAsync(cancellationToken);
            _db.BlueprintComponents.RemoveRange(components);
            _db.Blueprints.Remove(blueprint);
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task EnterMarksAsync(int marksheetId, System.Collections.Generic.IReadOnlyList<MarkInput> marks, CancellationToken cancellationToken = default)
        {
            var entries = await _db.MarkEntries.Where(e => e.MarksheetId == marksheetId).ToListAsync(cancellationToken);
            foreach (var input in marks)
            {
                var entry = entries.SingleOrDefault(e => e.BlueprintComponentId == input.BlueprintComponentId && e.EnrollmentId == input.EnrollmentId);
                if (entry == null)
                {
                    continue; // a stale cell (student left the section after the sheet was created) — ignore, don't invent rows
                }
                entry.Score = input.Score;
                entry.IsAbsent = input.IsAbsent;
                entry.IsExempt = input.IsExempt;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task DeleteMarksheetAsync(int marksheetId, CancellationToken cancellationToken = default)
        {
            var marksheet = await _db.Marksheets.SingleAsync(m => m.Id == marksheetId, cancellationToken);
            if (marksheet.Status != MarksheetStatus.Draft)
            {
                throw new MarksheetInUseException(marksheetId, $"it is {marksheet.Status}");
            }
            var entries = await _db.MarkEntries.Where(e => e.MarksheetId == marksheetId).ToListAsync(cancellationToken);
            if (entries.Any(e => e.Score != null || e.IsAbsent || e.IsExempt))
            {
                throw new MarksheetInUseException(marksheetId, "marks have already been entered");
            }

            _db.MarkEntries.RemoveRange(entries);
            _db.Marksheets.Remove(marksheet);
            await _db.SaveChangesAsync(cancellationToken);
        }

        private async Task PublishResultsAsync(Marksheet marksheet, System.Collections.Generic.List<MarkEntry> entries, CancellationToken cancellationToken)
        {
            var blueprint = await _db.Blueprints.SingleAsync(b => b.Id == marksheet.BlueprintId, cancellationToken);
            var components = await _db.BlueprintComponents.Where(c => c.BlueprintId == blueprint.Id).ToListAsync(cancellationToken);
            var bands = await _db.ScaleBands.Where(b => b.GradingScaleId == blueprint.GradingScaleId).ToListAsync(cancellationToken);

            var bandArgs = bands.Select(b => new ScaleBandResolver.Band(b.Id, b.MinPercent, b.MaxPercent)).ToList();
            var componentById = components.ToDictionary(c => c.Id);

            var byEnrollment = entries.GroupBy(e => e.EnrollmentId);
            foreach (var group in byEnrollment)
            {
                var marks = group.Select(e =>
                {
                    var component = componentById[e.BlueprintComponentId];
                    return new TermScoreCalculator.ComponentMark(e.Score, component.MaxScore, component.Weight, e.IsAbsent, e.IsExempt);
                }).ToList();

                var rawPercent = TermScoreCalculator.CalculateWeightedPercent(marks);
                var roundedPercent = TermScoreCalculator.RoundHalfUp(rawPercent);
                var bandId = ScaleBandResolver.Resolve(roundedPercent, bandArgs);

                var snapshot = JsonSerializer.Serialize(new
                {
                    blueprint.Id,
                    BlueprintGradingScaleId = blueprint.GradingScaleId,
                    Components = group.Select(e => new { e.BlueprintComponentId, e.Score, e.IsAbsent, e.IsExempt }),
                    RawPercent = rawPercent,
                    RoundedPercent = roundedPercent,
                });

                // Upsert - WF-08 (BR-GRA-005) reopens a Published marksheet back to Draft for correction, and
                // re-publishing must update the same TermResult row rather than violate its unique index.
                var existing = await _db.TermResults.SingleOrDefaultAsync(
                    r => r.EnrollmentId == group.Key && r.CurriculumOfferingId == blueprint.CurriculumOfferingId && r.TermId == blueprint.TermId,
                    cancellationToken);

                if (existing == null)
                {
                    existing = new TermResult
                    {
                        AcademicYearId = marksheet.AcademicYearId,
                        EnrollmentId = group.Key,
                        CurriculumOfferingId = blueprint.CurriculumOfferingId,
                        TermId = blueprint.TermId,
                    };
                    _db.TermResults.Add(existing);
                }

                existing.ScorePercent = roundedPercent;
                existing.ScaleBandId = bandId;
                existing.CalculationSnapshotJson = snapshot;
                existing.PublishedAtUtc = _clock.UtcNow;
            }
        }
    }
}
