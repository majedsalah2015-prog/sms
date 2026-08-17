using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.GlExport;
using Sms.Domain.Common;
using Sms.Domain.Fees;
using Sms.Domain.GlExport;
using Sms.Domain.Grades;
using Sms.Domain.Numbering;
using Sms.Domain.Payments;
using Sms.Domain.Schools;
using Sms.Domain.Students;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Fees;
using Sms.Infrastructure.GlExport;
using Sms.Infrastructure.Numbering;
using Sms.Infrastructure.Payments;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>S5/E-503 (GL journal-summary export, O3 assumption) over real E-303 documents in a Sqlite-backed AppDbContext.</summary>
    public sealed class GlExportServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 9, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId { get; set; }
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private int _studentId;
        private int _payerId;
        private int _tuitionId;

        public GlExportServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            using var db = CreateContext();
            db.Database.EnsureCreated();

            foreach (var (code, template) in new[] { ("INV", "INV-{SEQ:6}"), ("RCP", "RCP-{SEQ:6}"), ("CRN", "CRN-{SEQ:5}"), ("GLX", "GLX-{SEQ:4}") })
            {
                db.NumberingSeries.Add(new NumberingSeries
                {
                    Code = code, EntityName = code, FormatTemplate = template,
                    ResetPolicy = ResetPolicy.Never, GapPolicy = GapPolicy.Strict, EffectiveFromUtc = _clock.UtcNow, IsActive = true,
                });
            }

            var year = new AcademicYear
            {
                LabelAr = "Year", LabelEn = "2026-2027", HijriLabel = "Hijri",
                StartDate = new DateTime(2026, 9, 1), EndDate = new DateTime(2027, 6, 30), Status = AcademicYearStatus.Active,
            };
            db.AcademicYears.Add(year);
            var stage = new Stage { Name = new LocalizedName("Stage", "Elementary"), SequenceOrder = 1, DefaultGenderPolicy = GenderPolicy.Mixed };
            db.Stages.Add(stage);
            db.SaveChanges();
            _tenant.AcademicYearId = year.Id;
            var grade = new GradeLevel { StageId = stage.Id, Code = "G3", Name = new LocalizedName("Grade", "Grade 3"), SequenceOrder = 3 };
            db.GradeLevels.Add(grade);
            db.SaveChanges();
            var profile = new GradeYearProfile { GradeLevelId = grade.Id, AcademicYearId = year.Id, GenderPolicy = GenderPolicy.Mixed, TargetSections = 1, TargetSectionSize = 25 };
            db.GradeYearProfiles.Add(profile);
            db.SaveChanges();
            var student = new Student
            {
                StudentNo = "STU-1", FirstNameAr = "S", FatherNameAr = "F", GrandfatherNameAr = "G", FamilyNameAr = "Fam",
                FirstNameEn = "S", FatherNameEn = "F", GrandfatherNameEn = "G", FamilyNameEn = "Fam",
                Gender = Gender.Male, DateOfBirth = new DateTime(2018, 1, 1), NationalityLookupId = 1,
            };
            db.Students.Add(student);
            db.SaveChanges();
            db.Enrollments.Add(new Enrollment { AcademicYearId = year.Id, StudentId = student.Id, GradeYearProfileId = profile.Id, EnrollmentDate = new DateTime(2026, 9, 1), SourceType = EnrollmentSourceType.Admission });
            var payer = new Payer { Type = PayerType.Parent };
            db.Payers.Add(payer);
            var tuition = new FeeCategory { NameAr = "Tuition", NameEn = "Tuition", IsMandatory = true, IsRefundable = true, VatRate = 0.15m, GlExportCode = "4100" };
            db.FeeCategories.Add(tuition);
            db.SaveChanges();

            _studentId = student.Id;
            _payerId = payer.Id;
            _tuitionId = tuition.Id;
        }

        public void Dispose() => _connection.Dispose();

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private NumberIssuer Issuer(AppDbContext db) => new(db, _tenant, _tenant, _clock);

        private GlExportService CreateService(AppDbContext db) => new(db, Issuer(db), _clock, _audit);

        private async Task SeedDocumentsAsync(AppDbContext db)
        {
            var fees = new FeeAdmin(db, Issuer(db), _clock);
            var charge = await fees.PostManualChargeAsync(_studentId, _payerId, _tuitionId, 1000m);   // 1150 gross
            await fees.IssueCreditNoteAsync(charge.Id, 115m, "correction");
            await new PaymentAdmin(db, Issuer(db), _clock).CaptureReceiptAsync(_payerId, PaymentMethod.Cash, 1200m);   // 1035 allocated, 165 advance
        }

        private async Task SeedMappingsAsync(GlExportService service)
        {
            await service.DefineMappingAsync(GlAccountKeys.Receivables, "1200", "ذمم", "Receivables");
            await service.DefineMappingAsync(GlAccountKeys.VatOutput, "2300", "ضريبة", "VAT output");
            await service.DefineMappingAsync(GlAccountKeys.AdvancesReceived, "2400", "دفعات مقدمة", "Advances received");
            await service.DefineMappingAsync(GlAccountKeys.Cash("Cash"), "1000", "نقد", "Cash");
            await service.DefineMappingAsync("4100", "4100", "إيراد رسوم", "Tuition revenue");
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task A_period_generates_a_balanced_numbered_batch_over_real_documents()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await SeedDocumentsAsync(db);
            await SeedMappingsAsync(service);

            var batch = await service.GenerateAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30, 23, 59, 59), generatedByUserId: 1);

            Assert.Equal("GLX-0001", batch.BatchNo);
            Assert.Equal(batch.TotalDebit, batch.TotalCredit);
            Assert.Equal(3, batch.SourceDocumentCount);
            var lines = db.GlJournalLines.Where(l => l.GlExportBatchId == batch.Id).ToList();
            Assert.Equal(1000m, lines.Single(l => l.AccountKey == "4100" && l.Credit > 0).Credit);
            Assert.Equal("4100", lines.Single(l => l.AccountKey == "4100" && l.Credit > 0).AccountCode);
            Assert.Equal(100m, lines.Single(l => l.AccountKey == "4100" && l.Debit > 0).Debit);
            Assert.Equal(1200m, lines.Single(l => l.AccountKey == "Cash:Cash").Debit);
            Assert.Equal(165m, lines.Single(l => l.AccountKey == GlAccountKeys.AdvancesReceived).Credit);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task Missing_mappings_block_generation_and_name_every_missing_key()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await SeedDocumentsAsync(db);
            await service.DefineMappingAsync(GlAccountKeys.Receivables, "1200", "ذمم", "Receivables");

            var ex = await Assert.ThrowsAsync<GlMappingMissingException>(() => service.GenerateAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), 1));

            Assert.Contains("4100", ex.MissingKeys);
            Assert.Contains(GlAccountKeys.VatOutput, ex.MissingKeys);
            Assert.Contains("Cash:Cash", ex.MissingKeys);
            Assert.DoesNotContain(GlAccountKeys.Receivables, ex.MissingKeys);
            Assert.Empty(db.GlExportBatches);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task Overlapping_periods_are_refused_until_the_earlier_batch_is_voided()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await SeedDocumentsAsync(db);
            await SeedMappingsAsync(service);
            var first = await service.GenerateAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), 1);

            await Assert.ThrowsAsync<GlPeriodOverlapException>(() => service.GenerateAsync(new DateTime(2026, 9, 15), new DateTime(2026, 10, 15), 1));
            await service.VoidAsync(first.Id, "corrections posted");
            var second = await service.GenerateAsync(new DateTime(2026, 9, 15), new DateTime(2026, 10, 15), 1);

            Assert.Equal(GlExportBatchStatus.Voided, db.GlExportBatches.Single(b => b.Id == first.Id).Status);
            Assert.Equal("GLX-0002", second.BatchNo);
            var audit = db.AuditEntries.Single(e => e.EntityType == nameof(GlExportBatch) && e.FieldName == nameof(GlExportBatch.VoidReason));
            Assert.Equal("corrections posted", audit.Reason);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task The_rendered_csv_matches_the_stored_content_hash()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await SeedDocumentsAsync(db);
            await SeedMappingsAsync(service);
            var batch = await service.GenerateAsync(new DateTime(2026, 9, 1), new DateTime(2026, 9, 30), 1);

            var csv = await service.RenderCsvAsync(batch.Id);

            Assert.Equal(batch.ContentHash, GlExportService.Hash(csv));
            Assert.StartsWith("BatchNo,PeriodFrom", csv);
            Assert.Contains("\"GLX-0001\",2026-09-01,2026-09-30,1,", csv);
        }

        [Fact]
        [BusinessRule("BR-FEE-001")]
        public async Task An_empty_period_still_produces_an_empty_balanced_batch()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var batch = await service.GenerateAsync(new DateTime(2025, 1, 1), new DateTime(2025, 1, 31), 1);

            Assert.Equal(0m, batch.TotalDebit);
            Assert.Equal(0, batch.SourceDocumentCount);
            Assert.Empty(db.GlJournalLines);
        }
    }
}
