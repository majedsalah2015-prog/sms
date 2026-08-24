using System.Collections.Generic;
using System.Linq;
using Sms.Application.Rollover;
using Sms.Domain.Common;
using Sms.Domain.Grades;

namespace Sms.Application.Sections
{
    /// <summary>A student to place, with where they currently sit (null = not in any section yet).</summary>
    public sealed record BalanceStudent(int EnrollmentId, Gender Gender, int? CurrentSectionId);

    /// <summary>A section that can take students, with the two hard rules it enforces (BR-SCN-002/003).</summary>
    public sealed record BalanceSeat(int SectionId, int Capacity, GenderPolicy GenderPolicy);

    /// <summary>One proposed placement. <paramref name="FromSectionId"/> null means the student had no section.</summary>
    public sealed record BalanceMove(int EnrollmentId, int? FromSectionId, int ToSectionId);

    /// <summary>Why a requested placement was refused. Both kinds are BR-SCN hard rules — never overridden by a proposal.</summary>
    public enum BalanceViolationKind
    {
        /// <summary>BR-SCN-002: the section would hold more students than its capacity.</summary>
        Capacity = 1,

        /// <summary>BR-SCN-003: the student's gender is not one the section's policy admits.</summary>
        Gender = 2,
    }

    public sealed record BalanceViolation(int SectionId, BalanceViolationKind Kind, int? EnrollmentId);

    /// <summary>
    /// The outcome of a proposal run. <see cref="Fill"/> is the headcount each section
    /// would carry if every move were applied — the diff view needs the after picture
    /// as much as the moves, because "seven moves" says nothing about whether the
    /// grade ends up level.
    /// </summary>
    public sealed record BalanceProposal(
        IReadOnlyList<BalanceMove> Moves,
        IReadOnlyList<int> UnplacedEnrollmentIds,
        IReadOnlyDictionary<int, int> Fill);

    /// <summary>
    /// BR-SCN-008 for the assignment board (doc/Modules/08 §8.3 — "rule-based
    /// auto-distribute with proposal diff view"). Two jobs, deliberately separable
    /// by the caller: seat the students who have no section, and — only when asked —
    /// level sections that are already lopsided.
    /// <para>
    /// The separation is the point. Seating an unassigned student costs nothing;
    /// moving a seated one is a transfer, reason-coded and effective-dated
    /// (BR-SCN-005), and it drags marks continuity behind it (BR-SCN-006). A tool
    /// that quietly did the second while asked for the first would generate real
    /// transfer history for children who were already where they belonged.
    /// </para>
    /// <para>
    /// Every proposal honours capacity and gender policy — BR-SCN-008's "proposals
    /// never violate hard rules even before human confirmation" — so a student no
    /// compatible section has room for is reported unplaced rather than forced into
    /// a seat that does not exist. Language/curriculum grouping, sibling
    /// together/apart flags and Discipline keep-apart pairs are BR-SCN-008's other
    /// inputs and are <b>not</b> consulted: the flags they read are not modelled
    /// yet. A proposal from here is therefore size-and-gender only, and the screen
    /// says so rather than implying the behavioural rules were applied.
    /// </para>
    /// <para>
    /// Deterministic throughout — students in enrollment-id order, least-filled
    /// compatible section first, ties by section id — so re-running over the same
    /// grade proposes the same seats and a reviewer can trust a second look.
    /// </para>
    /// </summary>
    public static class SectionBalanceProposer
    {
        public static BalanceProposal Propose(
            IReadOnlyCollection<BalanceStudent> students,
            IReadOnlyCollection<BalanceSeat> seats,
            bool rebalancePlaced = false)
        {
            var seatById = seats.OrderBy(s => s.SectionId).ToDictionary(s => s.SectionId);
            var gender = students.ToDictionary(s => s.EnrollmentId, s => s.Gender);

            // A membership pointing at a section outside this grade is not something
            // to move — it is not this screen's business — so it reads as "no seat
            // here" and the student is left alone.
            var origin = students.ToDictionary(
                s => s.EnrollmentId,
                s => s.CurrentSectionId is int id && seatById.ContainsKey(id) ? id : (int?)null);
            var placement = new Dictionary<int, int?>(origin);

            var fill = seatById.Keys.ToDictionary(k => k, _ => 0);
            foreach (var seated in placement.Values.Where(v => v != null))
            {
                fill[seated!.Value]++;
            }

            SeatTheUnassigned(placement, fill, seatById, gender);

            if (rebalancePlaced)
            {
                Level(placement, fill, seatById, gender, students.Count);
            }

            var moves = placement
                .Where(p => p.Value != null && p.Value != origin[p.Key])
                .OrderBy(p => p.Key)
                .Select(p => new BalanceMove(p.Key, origin[p.Key], p.Value!.Value))
                .ToList();

            var unplaced = placement.Where(p => p.Value == null).Select(p => p.Key).OrderBy(x => x).ToList();

            return new BalanceProposal(moves, unplaced, fill);
        }

        /// <summary>
        /// Checks a placement the human dragged into place rather than one this class
        /// proposed. The board validates live in the browser, but the browser is not
        /// where a rule is enforced — the same two rules are re-checked here against
        /// the state the server actually holds.
        /// </summary>
        public static IReadOnlyList<BalanceViolation> Validate(
            IReadOnlyCollection<BalanceStudent> students,
            IReadOnlyCollection<BalanceSeat> seats,
            IReadOnlyDictionary<int, int> desired)
        {
            var seatById = seats.ToDictionary(s => s.SectionId);
            var gender = students.ToDictionary(s => s.EnrollmentId, s => s.Gender);
            var violations = new List<BalanceViolation>();

            foreach (var (enrollmentId, sectionId) in desired.OrderBy(d => d.Key))
            {
                if (!seatById.TryGetValue(sectionId, out var seat))
                {
                    continue;
                }

                if (gender.TryGetValue(enrollmentId, out var g) && !SectionDistributor.IsGenderCompatible(seat.GenderPolicy, g))
                {
                    violations.Add(new BalanceViolation(sectionId, BalanceViolationKind.Gender, enrollmentId));
                }
            }

            // Capacity is a property of the section after the whole batch lands, not
            // of any one student in it: checking it per move would pass a batch that
            // puts three students into the two seats that were left.
            foreach (var seat in seats.OrderBy(s => s.SectionId))
            {
                var landing = desired.Count(d => d.Value == seat.SectionId)
                    + students.Count(s => s.CurrentSectionId == seat.SectionId && !desired.ContainsKey(s.EnrollmentId));
                if (landing > seat.Capacity)
                {
                    violations.Add(new BalanceViolation(seat.SectionId, BalanceViolationKind.Capacity, null));
                }
            }

            return violations;
        }

        private static void SeatTheUnassigned(
            Dictionary<int, int?> placement,
            Dictionary<int, int> fill,
            Dictionary<int, BalanceSeat> seatById,
            Dictionary<int, Gender> gender)
        {
            foreach (var enrollmentId in placement.Where(p => p.Value == null).Select(p => p.Key).OrderBy(x => x).ToList())
            {
                var target = seatById.Values
                    .Where(s => SectionDistributor.IsGenderCompatible(s.GenderPolicy, gender[enrollmentId]) && fill[s.SectionId] < s.Capacity)
                    .OrderBy(s => fill[s.SectionId])
                    .ThenBy(s => s.SectionId)
                    .FirstOrDefault();

                if (target == null)
                {
                    continue;
                }

                placement[enrollmentId] = target.SectionId;
                fill[target.SectionId]++;
            }
        }

        /// <summary>
        /// Levels sections to within one student of each other, so far as the hard
        /// rules allow — a boys' section and a girls' section cannot level against
        /// each other however uneven they look, and the loop must not spin trying.
        /// Each student is moved at most once per run: it stops a pair of sections
        /// from passing the same child back and forth, and it keeps the diff a
        /// reviewer reads honest about what will actually happen.
        /// </summary>
        private static void Level(
            Dictionary<int, int?> placement,
            Dictionary<int, int> fill,
            Dictionary<int, BalanceSeat> seatById,
            Dictionary<int, Gender> gender,
            int studentCount)
        {
            var alreadyMoved = new HashSet<int>();

            for (var guard = studentCount * 2 + 8; guard > 0; guard--)
            {
                var best = (
                    from source in seatById.Values
                    from target in seatById.Values
                    where source.SectionId != target.SectionId
                          && fill[source.SectionId] - fill[target.SectionId] >= 2
                          && fill[target.SectionId] < target.Capacity
                    let candidate = placement
                        .Where(p => p.Value == source.SectionId
                                    && !alreadyMoved.Contains(p.Key)
                                    && SectionDistributor.IsGenderCompatible(target.GenderPolicy, gender[p.Key]))
                        .Select(p => (int?)p.Key)
                        .OrderBy(k => k)
                        .FirstOrDefault()
                    where candidate != null
                    orderby fill[target.SectionId] - fill[source.SectionId], source.SectionId, target.SectionId
                    select new { From = source.SectionId, To = target.SectionId, Enrollment = candidate.Value })
                    .FirstOrDefault();

                if (best == null)
                {
                    return;
                }

                placement[best.Enrollment] = best.To;
                fill[best.From]--;
                fill[best.To]++;
                alreadyMoved.Add(best.Enrollment);
            }
        }
    }
}
