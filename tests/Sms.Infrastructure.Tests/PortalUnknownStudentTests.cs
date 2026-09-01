using System;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Schools;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Portal;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// Pins the sharp edge on <see cref="ParentPortalQuery"/> that BR-SEC-011's
    /// disclosure guarantee rests on: asked about a student id that <b>does not
    /// exist</b>, the port does not refuse — it faults.
    /// <para>
    /// <c>EnsureAccessAsync</c> resolves the student with <c>SingleAsync</c>,
    /// which throws <c>InvalidOperationException("Sequence contains no
    /// elements")</c> rather than <see cref="PortalAccessDeniedException"/>.
    /// Both callers therefore have to check the row exists before they ask the
    /// gate anything, and both do: <c>PortalController.Student</c> has always
    /// done it, and <c>PortalApiController.StudentExistsAsync</c> now does.
    /// </para>
    /// <para>
    /// <b>Why this is worth a test of its own.</b> The mobile API called the
    /// port without that check and an unknown id came back as <b>500</b> while
    /// another family's child came back as 404 — and the difference told a
    /// caller which student ids exist, which is exactly the disclosure
    /// BR-SEC-011 exists to prevent. Found by smoke-testing the API on
    /// 2026-08-31. If somebody later makes the port itself answer
    /// <see cref="PortalAccessDeniedException"/> for an absent row — which would
    /// be the better shape — this test fails and points at the two guards that
    /// can then come out.
    /// </para>
    /// <para>
    /// Written as its own file rather than added to <c>ParentPortalQueryTests</c>
    /// because that file is being edited on another branch.
    /// </para>
    /// </summary>
    public sealed class PortalUnknownStudentTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; } = 1;
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId { get; set; } = 1;

            public int AcademicYearId { get; set; } = 1;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public PortalUnknownStudentTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.Schools.Add(new School
            {
                NameAr = "مدرسة الاختبار",
                NameEn = "Test School",
                LicenseNumber = "L-1",
                MinistryCode = "M-1",
            });
            db.SaveChanges();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task An_absent_student_faults_rather_than_refusing_so_every_caller_must_check_first()
        {
            using var db = CreateContext();
            var portal = new ParentPortalQuery(db, _tenant);

            // Not PortalAccessDeniedException — that is the point. A caller that
            // catches only the refusal will let this through as a 500.
            var fault = await Assert.ThrowsAsync<InvalidOperationException>(
                () => portal.GetAttendanceSummaryAsync(_user.UserId, studentId: 9999));

            Assert.IsNotType<PortalAccessDeniedException>(fault);
        }

        [Fact]
        [BusinessRule("BR-SEC-011")]
        public async Task The_same_is_true_of_every_student_scoped_read_on_the_port()
        {
            using var db = CreateContext();
            var portal = new ParentPortalQuery(db, _tenant);

            // Enumerated rather than asserted on one method: the guard has to be on
            // every entry point, and a new student-scoped read added to the port
            // inherits the same edge without anything saying so.
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => portal.GetPublishedResultsAsync(_user.UserId, 9999));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => portal.GetFeePositionAsync(_user.UserId, 9999));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => portal.GetSetWorkAsync(_user.UserId, 9999));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => portal.GetPublishedLessonsAsync(_user.UserId, 9999));
        }
    }
}
