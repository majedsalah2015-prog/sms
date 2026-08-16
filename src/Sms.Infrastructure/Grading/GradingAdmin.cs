using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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

        public GradingAdmin(AppDbContext db, IClock clock)
        {
            _db = db;
            _clock = clock;
        }

        public async Task<GradingScale> DefineScaleAsync(
            int stageId, string nameAr, string nameEn, int? curriculumLookupValueId = null, CancellationToken cancellationToken = default)
        {
            var scale = new GradingScale
            {
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

                _db.TermResults.Add(new TermResult
                {
                    AcademicYearId = marksheet.AcademicYearId,
                    EnrollmentId = group.Key,
                    CurriculumOfferingId = blueprint.CurriculumOfferingId,
                    TermId = blueprint.TermId,
                    ScorePercent = roundedPercent,
                    ScaleBandId = bandId,
                    CalculationSnapshotJson = snapshot,
                    PublishedAtUtc = _clock.UtcNow,
                });
            }
        }
    }
}
