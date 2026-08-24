using System.Collections.Generic;
using System.Linq;
using Sms.Application.Sections;
using Sms.Domain.Common;
using Sms.Domain.Grades;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Sections
{
    /// <summary>
    /// BR-SCN-008. The proposer's output becomes real transfer history the moment a
    /// registrar presses confirm, so the cases tested here are the ones where a
    /// plausible-looking proposal would do damage: moving children who were already
    /// where they belonged, filling a section past its capacity, or putting a boy in
    /// a girls' section because the arithmetic said it was the emptiest.
    /// </summary>
    public class SectionBalanceProposerTests
    {
        private static BalanceSeat Seat(int id, int capacity = 25, GenderPolicy policy = GenderPolicy.Mixed)
            => new(id, capacity, policy);

        private static BalanceStudent Student(int enrollmentId, int? at = null, Gender gender = Gender.Male)
            => new(enrollmentId, gender, at);

        [Fact]
        [BusinessRule("BR-SCN-008")]
        public void Unassigned_students_fill_the_emptiest_compatible_section_first()
        {
            var proposal = SectionBalanceProposer.Propose(
                new[] { Student(1), Student(2), Student(3), Student(4) },
                new[] { Seat(10), Seat(20) });

            Assert.Equal(4, proposal.Moves.Count);
            Assert.Equal(2, proposal.Fill[10]);
            Assert.Equal(2, proposal.Fill[20]);
            Assert.All(proposal.Moves, m => Assert.Null(m.FromSectionId));
        }

        /// <summary>
        /// The whole reason seating and levelling are separate arguments: a registrar
        /// asking "seat the six children who have no section" must not be handed a
        /// proposal that also moves thirty who already had one.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-008")]
        public void Seating_the_unassigned_never_moves_a_student_who_already_has_a_section()
        {
            var students = new[]
            {
                Student(1, at: 10), Student(2, at: 10), Student(3, at: 10), Student(4, at: 10),
                Student(5),
            };

            var proposal = SectionBalanceProposer.Propose(students, new[] { Seat(10), Seat(20) });

            var move = Assert.Single(proposal.Moves);
            Assert.Equal(5, move.EnrollmentId);
            Assert.Null(move.FromSectionId);
            Assert.Equal(20, move.ToSectionId);
        }

        [Fact]
        [BusinessRule("BR-SCN-008")]
        public void Levelling_evens_a_lopsided_grade_to_within_one()
        {
            var students = Enumerable.Range(1, 6).Select(i => Student(i, at: 10))
                .Append(Student(7, at: 20))
                .ToList();

            var proposal = SectionBalanceProposer.Propose(students, new[] { Seat(10), Seat(20) }, rebalancePlaced: true);

            Assert.Equal(4, proposal.Fill[10]);
            Assert.Equal(3, proposal.Fill[20]);
            Assert.All(proposal.Moves, m => Assert.Equal(20, m.ToSectionId));
        }

        /// <summary>
        /// A boys' section and a girls' section cannot level against each other however
        /// uneven they look. The loop must notice that and stop rather than spin.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-003")]
        public void Levelling_stops_rather_than_spinning_when_gender_policy_forbids_every_move()
        {
            var students = Enumerable.Range(1, 5).Select(i => Student(i, at: 10, gender: Gender.Male))
                .Append(Student(9, at: 20, gender: Gender.Female))
                .ToList();

            var proposal = SectionBalanceProposer.Propose(
                students,
                new[] { Seat(10, policy: GenderPolicy.Boys), Seat(20, policy: GenderPolicy.Girls) },
                rebalancePlaced: true);

            Assert.Empty(proposal.Moves);
            Assert.Equal(5, proposal.Fill[10]);
            Assert.Equal(1, proposal.Fill[20]);
        }

        [Fact]
        [BusinessRule("BR-SCN-003")]
        public void A_student_is_never_proposed_into_a_section_their_gender_bars()
        {
            var proposal = SectionBalanceProposer.Propose(
                new[] { Student(1, gender: Gender.Female), Student(2, gender: Gender.Male) },
                new[] { Seat(10, policy: GenderPolicy.Boys), Seat(20, policy: GenderPolicy.Girls) });

            Assert.Equal(20, proposal.Moves.Single(m => m.EnrollmentId == 1).ToSectionId);
            Assert.Equal(10, proposal.Moves.Single(m => m.EnrollmentId == 2).ToSectionId);
        }

        /// <summary>
        /// Capacity is BR-SCN-002's hard rule, so the surplus student is reported
        /// rather than forced into a seat that does not exist. Silently placing them
        /// is how a grade ends up with thirty-one children in a room built for thirty.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void A_student_no_compatible_section_has_room_for_is_reported_not_forced()
        {
            var proposal = SectionBalanceProposer.Propose(
                new[] { Student(1), Student(2), Student(3) },
                new[] { Seat(10, capacity: 2) });

            Assert.Equal(2, proposal.Moves.Count);
            Assert.Equal(3, Assert.Single(proposal.UnplacedEnrollmentIds));
            Assert.Equal(2, proposal.Fill[10]);
        }

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Levelling_will_not_push_a_section_past_its_capacity()
        {
            var students = Enumerable.Range(1, 8).Select(i => Student(i, at: 10)).ToList();

            var proposal = SectionBalanceProposer.Propose(
                students,
                new[] { Seat(10, capacity: 30), Seat(20, capacity: 2) },
                rebalancePlaced: true);

            Assert.Equal(2, proposal.Fill[20]);
            Assert.Equal(6, proposal.Fill[10]);
        }

        /// <summary>
        /// A membership pointing at a section outside this grade is not this screen's
        /// business, and moving it would be a silent cross-grade transfer.
        /// </summary>
        [Fact]
        public void A_student_seated_outside_this_grade_is_left_where_they_are()
        {
            var proposal = SectionBalanceProposer.Propose(
                new[] { Student(1, at: 99) },
                new[] { Seat(10) });

            var move = Assert.Single(proposal.Moves);
            Assert.Null(move.FromSectionId);
            Assert.Equal(10, move.ToSectionId);
        }

        [Fact]
        public void Two_runs_over_the_same_grade_propose_the_same_seats()
        {
            var students = Enumerable.Range(1, 9).Select(i => Student(i)).ToList();
            var seats = new[] { Seat(30), Seat(10), Seat(20) };

            var first = SectionBalanceProposer.Propose(students, seats);
            var second = SectionBalanceProposer.Propose(students, seats);

            Assert.Equal(
                first.Moves.Select(m => (m.EnrollmentId, m.ToSectionId)),
                second.Moves.Select(m => (m.EnrollmentId, m.ToSectionId)));
        }

        // ---- Validate: the board's own drag-drop, re-checked server-side ----------

        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Validate_counts_the_whole_batch_against_capacity_not_one_move_at_a_time()
        {
            var students = new[] { Student(1), Student(2), Student(3) };
            var desired = new Dictionary<int, int> { [1] = 10, [2] = 10, [3] = 10 };

            var violations = SectionBalanceProposer.Validate(students, new[] { Seat(10, capacity: 2) }, desired);

            var violation = Assert.Single(violations);
            Assert.Equal(BalanceViolationKind.Capacity, violation.Kind);
            Assert.Equal(10, violation.SectionId);
        }

        /// <summary>
        /// Students staying put still occupy their seats. A batch checked only against
        /// the students it names would wave through a move into a section that is
        /// already full of children nobody is moving.
        /// </summary>
        [Fact]
        [BusinessRule("BR-SCN-002")]
        public void Validate_counts_the_students_who_are_not_moving()
        {
            var students = new[] { Student(1, at: 10), Student(2, at: 10), Student(3, at: 20) };
            var desired = new Dictionary<int, int> { [3] = 10 };

            var violations = SectionBalanceProposer.Validate(students, new[] { Seat(10, capacity: 2), Seat(20) }, desired);

            Assert.Equal(BalanceViolationKind.Capacity, Assert.Single(violations).Kind);
        }

        [Fact]
        [BusinessRule("BR-SCN-003")]
        public void Validate_names_the_student_whose_gender_the_section_bars()
        {
            var students = new[] { Student(1, gender: Gender.Female) };
            var desired = new Dictionary<int, int> { [1] = 10 };

            var violations = SectionBalanceProposer.Validate(students, new[] { Seat(10, policy: GenderPolicy.Boys) }, desired);

            var violation = Assert.Single(violations);
            Assert.Equal(BalanceViolationKind.Gender, violation.Kind);
            Assert.Equal(1, violation.EnrollmentId);
        }

        [Fact]
        public void Validate_passes_a_placement_that_breaks_nothing()
        {
            var students = new[] { Student(1, at: 10), Student(2, at: 20) };
            var desired = new Dictionary<int, int> { [1] = 20, [2] = 10 };

            Assert.Empty(SectionBalanceProposer.Validate(students, new[] { Seat(10), Seat(20) }, desired));
        }
    }
}
