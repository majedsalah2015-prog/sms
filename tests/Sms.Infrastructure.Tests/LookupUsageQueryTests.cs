using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Lookups;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Lookups;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Lookups;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// The lookup usage counter behind doc/Modules/01 §9 ("deactivation of a lookup
    /// shows usage count and requires confirmation") and BR-SET-002, over a real
    /// Sqlite-backed AppDbContext. The counter finds its referencing columns by
    /// walking the EF model, so these tests are also the guard on that walk: they
    /// assert the shape of what it finds (entity name, column name, count) rather
    /// than a hard-coded list, and would fail the day the walk stopped seeing a
    /// column it used to see.
    /// </summary>
    public sealed class LookupUsageQueryTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        /// <summary>SchoolId is settable here (unlike the other suites') so the tenant-scoping test can write as another school.</summary>
        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId { get; set; } = 1;

            public int AcademicYearId => 2027;
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();

        public LookupUsageQueryTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        // --- the empty case: the only one where deactivating costs nothing ------

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_value_nothing_references_reports_no_usage_at_all()
        {
            using var db = CreateContext();
            var nationality = await SeedValueAsync(db, "Nationality", "SA");

            var usages = await new LookupUsageQuery(db).CountUsagesAsync(nationality.Id);

            Assert.Empty(usages);
            Assert.Equal(0, usages.TotalCount());
            Assert.False(usages.IsReferenced());
        }

        // --- the counting itself -----------------------------------------------

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_value_referenced_by_students_names_the_entity_the_column_and_the_count()
        {
            using var db = CreateContext();
            var nationality = await SeedValueAsync(db, "Nationality", "SA");
            db.Students.AddRange(
                NewStudent("S-1", nationality.Id),
                NewStudent("S-2", nationality.Id),
                NewStudent("S-3", nationality.Id));
            await db.SaveChangesAsync();

            var usages = await new LookupUsageQuery(db).CountUsagesAsync(nationality.Id);

            var row = Assert.Single(usages);
            Assert.Equal(nameof(Student), row.EntityName);
            Assert.Equal(nameof(Student.NationalityLookupId), row.PropertyName);
            Assert.Equal(3, row.Count);
            Assert.Equal(3, usages.TotalCount());
            Assert.True(usages.IsReferenced());
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_value_referenced_from_two_entities_reports_both_worst_offender_first()
        {
            using var db = CreateContext();
            var nationality = await SeedValueAsync(db, "Nationality", "SA");
            db.Students.AddRange(
                NewStudent("S-1", nationality.Id),
                NewStudent("S-2", nationality.Id),
                NewStudent("S-3", nationality.Id));
            db.Employees.Add(NewEmployee("E-1", nationality.Id));
            await db.SaveChangesAsync();

            var usages = await new LookupUsageQuery(db).CountUsagesAsync(nationality.Id);

            Assert.Equal(2, usages.Count);
            Assert.Equal(nameof(Student), usages[0].EntityName);
            Assert.Equal(3, usages[0].Count);
            Assert.Equal(nameof(Employee), usages[1].EntityName);
            Assert.Equal(1, usages[1].Count);
            Assert.All(usages, u => Assert.Equal(nameof(Student.NationalityLookupId), u.PropertyName));
            Assert.Equal(4, usages.TotalCount());
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_nullable_reference_column_is_counted_like_a_required_one()
        {
            // PrimaryIdTypeLookupId is int?, NationalityLookupId is int — the counter builds a
            // different comparison for each, and only this test exercises the nullable branch.
            using var db = CreateContext();
            var nationality = await SeedValueAsync(db, "Nationality", "SA");
            var idType = await SeedValueAsync(db, "IdType", "Passport");
            var student = NewStudent("S-1", nationality.Id);
            student.PrimaryIdTypeLookupId = idType.Id;
            db.Students.Add(student);
            db.Students.Add(NewStudent("S-2", nationality.Id));
            await db.SaveChangesAsync();

            var usages = await new LookupUsageQuery(db).CountUsagesAsync(idType.Id);

            var row = Assert.Single(usages);
            Assert.Equal(nameof(Student), row.EntityName);
            Assert.Equal(nameof(Student.PrimaryIdTypeLookupId), row.PropertyName);
            Assert.Equal(1, row.Count);
        }

        // --- the refusals to under-report ---------------------------------------

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task A_soft_deactivated_referencing_row_is_still_counted()
        {
            using var db = CreateContext();
            var nationality = await SeedValueAsync(db, "Nationality", "SA");
            var withdrawn = NewStudent("S-1", nationality.Id);
            db.Students.AddRange(withdrawn, NewStudent("S-2", nationality.Id));
            await db.SaveChangesAsync();

            withdrawn.IsActive = false;
            await db.SaveChangesAsync();

            // The soft-active filter hides the withdrawn student from every ordinary query …
            Assert.Single(db.Students.Where(s => s.NationalityLookupId == nationality.Id));

            // … but the row still points at the value, so the operator must still be told about it.
            var usages = await new LookupUsageQuery(db).CountUsagesAsync(nationality.Id);

            Assert.Equal(2, Assert.Single(usages).Count);
        }

        [Fact]
        [BusinessRule("BR-SET-002")]
        public async Task Another_schools_rows_are_never_counted()
        {
            // IgnoreQueryFilters drops the tenant filter along with the soft-active one, so the
            // counter re-applies the school predicate by hand. Without it this returns 2.
            LookupValue nationality;
            using (var db = CreateContext())
            {
                nationality = await SeedValueAsync(db, "Nationality", "SA");
                db.Students.Add(NewStudent("S-1", nationality.Id));
                await db.SaveChangesAsync();
            }

            _tenant.SchoolId = 2;
            using (var otherSchool = CreateContext())
            {
                otherSchool.Students.Add(NewStudent("S-1", nationality.Id));
                await otherSchool.SaveChangesAsync();
            }

            _tenant.SchoolId = 1;
            using var ours = CreateContext();
            var usages = await new LookupUsageQuery(ours).CountUsagesAsync(nationality.Id);

            Assert.Equal(1, Assert.Single(usages).Count);
        }

        [Fact]
        public async Task An_id_of_zero_is_refused_rather_than_answered_with_every_unset_row()
        {
            // A required *LookupId column on a row nobody filled in holds 0, so asking "who
            // references value 0" would otherwise report the whole half-populated register.
            using var db = CreateContext();
            db.Students.Add(NewStudent("S-1", nationalityLookupId: 0));
            await db.SaveChangesAsync();

            Assert.Empty(await new LookupUsageQuery(db).CountUsagesAsync(0));
            Assert.Empty(await new LookupUsageQuery(db).CountUsagesAsync(-1));
        }

        // --- helpers ------------------------------------------------------------

        private static async Task<LookupValue> SeedValueAsync(AppDbContext db, string categoryCode, string code)
        {
            var admin = new LookupAdmin(db);
            await admin.DefineCategoryAsync(categoryCode, LookupCategoryTier.ProductSeeded, categoryCode, categoryCode);
            return await admin.DefineValueAsync(categoryCode, code, code, code, sortOrder: 1);
        }

        private static Student NewStudent(string studentNo, int nationalityLookupId) => new()
        {
            StudentNo = studentNo,
            FirstNameAr = "أحمد",
            FatherNameAr = "علي",
            GrandfatherNameAr = "محمد",
            FamilyNameAr = "السالم",
            FirstNameEn = "Ahmed",
            FatherNameEn = "Ali",
            GrandfatherNameEn = "Mohammed",
            FamilyNameEn = "AlSalem",
            Gender = Gender.Male,
            DateOfBirth = new DateTime(2015, 1, 1),
            NationalityLookupId = nationalityLookupId,
            Status = StudentStatus.Enrolled,
        };

        private static Employee NewEmployee(string employeeNo, int nationalityLookupId) => new()
        {
            EmployeeNo = employeeNo,
            FirstNameAr = "سارة",
            FatherNameAr = "خالد",
            GrandfatherNameAr = "عبدالله",
            FamilyNameAr = "الحربي",
            FirstNameEn = "Sara",
            FatherNameEn = "Khaled",
            GrandfatherNameEn = "Abdullah",
            FamilyNameEn = "AlHarbi",
            Gender = Gender.Female,
            DateOfBirth = new DateTime(1990, 1, 1),
            NationalityLookupId = nationalityLookupId,
            Status = EmployeeStatus.Active,
        };
    }
}
