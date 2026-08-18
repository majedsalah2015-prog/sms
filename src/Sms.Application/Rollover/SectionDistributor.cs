using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Common;
using Sms.Domain.Grades;

namespace Sms.Application.Rollover
{
    /// <summary>A student awaiting a target-year section.</summary>
    public sealed class DistributionCandidate
    {
        public DistributionCandidate(int studentId, Gender gender)
        {
            StudentId = studentId;
            Gender = gender;
        }

        public int StudentId { get; }

        public Gender Gender { get; }
    }

    /// <summary>A target-year section with its current fill (existing memberships + already-planned rollover assignments).</summary>
    public sealed class DistributionSection
    {
        public DistributionSection(int sectionId, int capacity, int currentCount, GenderPolicy genderPolicy)
        {
            SectionId = sectionId;
            Capacity = capacity;
            CurrentCount = currentCount;
            GenderPolicy = genderPolicy;
        }

        public int SectionId { get; }

        public int Capacity { get; }

        public int CurrentCount { get; }

        public GenderPolicy GenderPolicy { get; }
    }

    public sealed class DistributionResult
    {
        public DistributionResult(IReadOnlyDictionary<int, int> assignments, IReadOnlyList<int> unplacedStudentIds)
        {
            Assignments = assignments;
            UnplacedStudentIds = unplacedStudentIds;
        }

        /// <summary>studentId → sectionId.</summary>
        public IReadOnlyDictionary<int, int> Assignments { get; }

        /// <summary>Students no compatible section had room for — surfaced, never force-placed (capacity is BR-SCN-002's hard rule).</summary>
        public IReadOnlyList<int> UnplacedStudentIds { get; }
    }

    /// <summary>
    /// BR-AYR-008 step 5 / BR-SCN-008 (size-balance + gender-policy subset):
    /// rule-based auto-distribution of confirmed students across a grade's
    /// target-year sections. Deterministic (students in id order, least-filled
    /// compatible section first, ties by section id) so a re-run over the same
    /// stragglers proposes the same seats. Language/curriculum grouping,
    /// sibling flags and Discipline keep-apart pairs are not consulted here
    /// (BR-SCN-008's other inputs — proposals only, humans confirm).
    /// </summary>
    public static class SectionDistributor
    {
        public static DistributionResult Distribute(IEnumerable<DistributionCandidate> candidates, IEnumerable<DistributionSection> sections)
        {
            var fill = sections.OrderBy(s => s.SectionId).ToDictionary(s => s.SectionId, s => (Section: s, Count: s.CurrentCount));
            var assignments = new Dictionary<int, int>();
            var unplaced = new List<int>();

            foreach (var candidate in candidates.OrderBy(c => c.StudentId))
            {
                var target = fill.Values
                    .Where(f => IsGenderCompatible(f.Section.GenderPolicy, candidate.Gender) && f.Count < f.Section.Capacity)
                    .OrderBy(f => f.Count)
                    .ThenBy(f => f.Section.SectionId)
                    .Select(f => f.Section)
                    .FirstOrDefault();

                if (target == null)
                {
                    unplaced.Add(candidate.StudentId);
                    continue;
                }

                assignments[candidate.StudentId] = target.SectionId;
                fill[target.SectionId] = (target, fill[target.SectionId].Count + 1);
            }

            return new DistributionResult(assignments, unplaced);
        }

        /// <summary>BR-GRD-004 semantics: Boys ⇢ Male only, Girls ⇢ Female only, Mixed ⇢ anyone.</summary>
        public static bool IsGenderCompatible(GenderPolicy policy, Gender gender)
        {
            return policy switch
            {
                GenderPolicy.Boys => gender == Gender.Male,
                GenderPolicy.Girls => gender == Gender.Female,
                _ => true,
            };
        }
    }
}
