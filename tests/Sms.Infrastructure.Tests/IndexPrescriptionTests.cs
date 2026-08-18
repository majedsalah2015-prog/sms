using System;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attachments;
using Sms.Domain.Attendance;
using Sms.Domain.Cafeteria;
using Sms.Domain.Fees;
using Sms.Domain.Grading;
using Sms.Domain.Installments;
using Sms.Domain.Notifications;
using Sms.Domain.Payments;
using Sms.Domain.Sections;
using Sms.Domain.Students;
using Sms.Domain.Timetable;
using Sms.Infrastructure.Persistence;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// S8/E-802 — docs/Database/04 §1 "key prescriptions (analysis-known hot paths)" are present in the EF model.
    /// Column names follow the code where the doc's differ (Date vs SessionDate/AttendanceDate, CreatedAtUtc vs
    /// QueuedAtUtc, IssuedAtUtc vs PostedAtUtc); prescriptions whose columns don't exist (Installment.PayerId/Status,
    /// PaymentAllocation.InstallmentId, Charge.EnrollmentId, MarkEntry 2-col UQ) are recorded as adapted in the configs.
    /// </summary>
    public sealed class IndexPrescriptionTests : IDisposable
    {
        private sealed class Tenant : ITenantContext, IWorkingYearContext { public int SchoolId => 1; public int AcademicYearId => 1; }
        private sealed class Clock : IClock { public DateTime UtcNow => DateTime.UtcNow; }
        private sealed class User : ICurrentUser { public int UserId => 1; }

        private readonly SqliteConnection _connection = new("DataSource=:memory:");
        private readonly AppDbContext _db;

        public IndexPrescriptionTests()
        {
            _connection.Open();
            _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options, new Tenant(), new User(), new Clock());
        }

        public void Dispose()
        {
            _db.Dispose();
            _connection.Dispose();
        }

        [Theory]
        [InlineData(typeof(Enrollment), "SchoolId,AcademicYearId,GradeYearProfileId", false)]
        [InlineData(typeof(Enrollment), "StudentId,AcademicYearId", true)]
        [InlineData(typeof(SectionMembership), "SectionId,EffectiveToUtc", false)]
        [InlineData(typeof(SectionMembership), "EnrollmentId,EffectiveFromUtc", false)]
        [InlineData(typeof(Session), "PlacementId,Date", true)]
        [InlineData(typeof(Session), "SchoolId,Date", false)]
        [InlineData(typeof(AttendanceDay), "EnrollmentId,Date", true)]
        [InlineData(typeof(AttendanceDay), "SchoolId,Date,Status", false)]
        [InlineData(typeof(MarkEntry), "MarksheetId,BlueprintComponentId,EnrollmentId", true)]
        [InlineData(typeof(MarkEntry), "EnrollmentId", false)]
        [InlineData(typeof(Charge), "SchoolId,ChargeNo", true)]
        [InlineData(typeof(Charge), "PayerId,Status", false)]
        [InlineData(typeof(Installment), "SchoolId,DueDate", false)]
        [InlineData(typeof(PaymentAllocation), "ReceiptId", false)]
        [InlineData(typeof(PaymentAllocation), "ChargeId", false)]
        [InlineData(typeof(Receipt), "SchoolId,ReceiptNo", true)]
        [InlineData(typeof(Receipt), "PayerId,IssuedAtUtc", false)]
        [InlineData(typeof(Sms.Domain.Audit.AuditEntry), "EntityType,EntityId,OccurredAtUtc", false)]
        [InlineData(typeof(Sms.Domain.Audit.AuditEntry), "ActorUserId,OccurredAtUtc", false)]
        [InlineData(typeof(Sms.Domain.Audit.AuditEntry), "SchoolId,OccurredAtUtc", false)]
        [InlineData(typeof(Delivery), "SchoolId,Status,CreatedAtUtc", false)]
        [InlineData(typeof(WalletLedgerEntry), "WalletId,Id", false)]
        [InlineData(typeof(Attachment), "OwningEntityType,OwningEntityId,DocumentTypeId", false)]
        [InlineData(typeof(Attachment), "DocumentTypeId,ExpiryDateUtc", false)]
        public void Db04_hot_path_index_exists(Type entity, string columns, bool unique)
        {
            var type = _db.Model.FindEntityType(entity)!;
            var expected = columns.Split(',');
            var match = type.GetIndexes().FirstOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(expected) && (!unique || i.IsUnique));

            Assert.True(match != null, $"{entity.Name} is missing index ({columns}){(unique ? " UNIQUE" : string.Empty)}. Present: "
                + string.Join(" | ", type.GetIndexes().Select(i => string.Join(",", i.Properties.Select(p => p.Name)) + (i.IsUnique ? " UQ" : string.Empty))));
        }

        [Fact]
        public void Filtered_prescriptions_carry_their_filters()
        {
            Assert.Equal("[IsSuperseded] = 0 AND [IsWrittenOff] = 0", Find(typeof(Installment), "SchoolId,DueDate").GetFilter());
            Assert.Equal("[Status] IN (1, 4)", Find(typeof(Delivery), "SchoolId,Status,CreatedAtUtc").GetFilter());
            Assert.Equal("[ExpiryDateUtc] IS NOT NULL", Find(typeof(Attachment), "DocumentTypeId,ExpiryDateUtc").GetFilter());
        }

        [Fact]
        public void Snapshot_tables_live_in_the_rpt_schema_with_as_of()
        {
            foreach (var t in new[] { typeof(Domain.ReadModels.AgedReceivablesSnapshot), typeof(Domain.ReadModels.DailyAttendanceSummarySnapshot), typeof(Domain.ReadModels.CollectionCalendarSnapshot) })
            {
                var type = _db.Model.FindEntityType(t)!;
                Assert.Equal("rpt", type.GetSchema());
                Assert.NotNull(type.FindProperty("AsOfUtc"));
            }
        }

        private Microsoft.EntityFrameworkCore.Metadata.IIndex Find(Type entity, string columns)
            => _db.Model.FindEntityType(entity)!.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(columns.Split(',')));
    }
}
