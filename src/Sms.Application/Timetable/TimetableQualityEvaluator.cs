using System;
using System.Collections.Generic;
using System.Linq;

namespace Sms.Application.Timetable
{
    /// <summary>
    /// Pure BR-TTB-005 soft constraints ("warn + score", never block): the
    /// subset computable from placements alone — same subject twice in a
    /// day for a section (no double Math daily), a teacher's consecutive
    /// teaching run over the configured max, and a teacher's idle gaps
    /// within a day (daily spread). Heavy-subjects-early, home-room
    /// stability (BR-ROM-006) and room-travel minimisation need config /
    /// room geography that has no source yet — listed in doc §3 but not
    /// scored here. The quality score is 100 minus weighted penalties;
    /// publication remains allowed with acknowledgment (doc §3).
    /// </summary>
    public static class TimetableQualityEvaluator
    {
        public const int DefaultMaxConsecutivePeriods = 4;

        public enum WarningKind : short
        {
            SubjectRepeatedSameDay = 1,
            TeacherConsecutiveOverMax = 2,
            TeacherDailyGap = 3,
        }

        public readonly struct PlacedPeriod
        {
            public PlacedPeriod(int placementId, int sectionId, int curriculumOfferingId, int teacherProfileId, DayOfWeek dayOfWeek, int sequenceNumber)
            {
                PlacementId = placementId;
                SectionId = sectionId;
                CurriculumOfferingId = curriculumOfferingId;
                TeacherProfileId = teacherProfileId;
                DayOfWeek = dayOfWeek;
                SequenceNumber = sequenceNumber;
            }

            public int PlacementId { get; }

            public int SectionId { get; }

            public int CurriculumOfferingId { get; }

            public int TeacherProfileId { get; }

            public DayOfWeek DayOfWeek { get; }

            public int SequenceNumber { get; }
        }

        public sealed record Warning(WarningKind Kind, DayOfWeek DayOfWeek, int? SectionId, int? TeacherProfileId, int? CurriculumOfferingId, int Magnitude);

        public sealed record Result(IReadOnlyList<Warning> Warnings, int Score);

        /// <param name="placements">Teaching placements with their slot's day and sequence number.</param>
        /// <param name="breakSlots">(day, sequence) of non-teaching slots (breaks/assembly): never idle gaps, and — because they carry their own sequence number — they also end a consecutive run.</param>
        /// <param name="maxConsecutivePeriods">Soft cap on a teacher's consecutive teaching run.</param>
        public static Result Evaluate(
            IEnumerable<PlacedPeriod> placements,
            IReadOnlySet<(DayOfWeek Day, int Sequence)>? breakSlots = null,
            int maxConsecutivePeriods = DefaultMaxConsecutivePeriods)
        {
            var list = placements.ToList();
            var breaks = breakSlots ?? new HashSet<(DayOfWeek, int)>();
            var warnings = new List<Warning>();

            // Subject distribution: the same offering more than once on one day for a section.
            foreach (var g in list.GroupBy(p => (p.SectionId, p.DayOfWeek, p.CurriculumOfferingId)).Where(g => g.Count() > 1))
            {
                warnings.Add(new Warning(WarningKind.SubjectRepeatedSameDay, g.Key.DayOfWeek, g.Key.SectionId, null, g.Key.CurriculumOfferingId, g.Count()));
            }

            // Teacher daily runs and gaps over the sequence numbers they teach that day.
            foreach (var g in list.GroupBy(p => (p.TeacherProfileId, p.DayOfWeek)))
            {
                var seqs = g.Select(p => p.SequenceNumber).Distinct().OrderBy(s => s).ToList();
                var longestRun = LongestConsecutiveRun(seqs);
                if (longestRun > maxConsecutivePeriods)
                {
                    warnings.Add(new Warning(WarningKind.TeacherConsecutiveOverMax, g.Key.DayOfWeek, null, g.Key.TeacherProfileId, null, longestRun));
                }

                var breaksInside = seqs.Count < 2 ? 0 : Enumerable.Range(seqs[0], seqs[^1] - seqs[0] + 1).Count(seq => breaks.Contains((g.Key.DayOfWeek, seq)));
                var gaps = seqs.Count < 2 ? 0 : (seqs[^1] - seqs[0] + 1) - seqs.Count - breaksInside;
                if (gaps > 0)
                {
                    warnings.Add(new Warning(WarningKind.TeacherDailyGap, g.Key.DayOfWeek, null, g.Key.TeacherProfileId, null, gaps));
                }
            }

            var penalty = warnings.Sum(w => w.Kind switch
            {
                WarningKind.SubjectRepeatedSameDay => 5 * (w.Magnitude - 1),
                WarningKind.TeacherConsecutiveOverMax => 10,
                WarningKind.TeacherDailyGap => 2 * w.Magnitude,
                _ => 0,
            });

            return new Result(warnings, Math.Max(0, 100 - penalty));
        }

        private static int LongestConsecutiveRun(IReadOnlyList<int> sortedDistinct)
        {
            var best = 0;
            var run = 0;
            for (var i = 0; i < sortedDistinct.Count; i++)
            {
                run = i > 0 && sortedDistinct[i] == sortedDistinct[i - 1] + 1 ? run + 1 : 1;
                best = Math.Max(best, run);
            }

            return best;
        }
    }
}
