using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attachments;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Domain.Attachments;
using Sms.Infrastructure.Attachments;
using Sms.Infrastructure.Audit;
using Sms.Infrastructure.Persistence;
using Sms.TestSupport;
using Xunit;

namespace Sms.Infrastructure.Tests
{
    /// <summary>
    /// E-008 slice 1 over a real Sqlite-backed AppDbContext + a real local-disk
    /// file store (temp directory per test run), so BR-ATT-009's quarantine
    /// gate and BR-ATT-010's content-hash integrity are exercised end to end.
    /// </summary>
    public sealed class AttachmentServiceTests : IDisposable
    {
        private sealed class FixedClock : IClock
        {
            public DateTime UtcNow { get; set; } = new(2026, 8, 15, 8, 0, 0, DateTimeKind.Utc);
        }

        private sealed class FixedUser : ICurrentUser
        {
            public int UserId { get; set; }
        }

        private sealed class FixedTenant : ITenantContext, IWorkingYearContext
        {
            public int SchoolId => 1;

            public int AcademicYearId => 2027;
        }

        private sealed class InfectedScanner : IVirusScanner
        {
            public Task<ScanStatus> ScanAsync(byte[] content, System.Threading.CancellationToken cancellationToken = default)
                => Task.FromResult(ScanStatus.Infected);
        }

        private readonly SqliteConnection _connection;
        private readonly FixedClock _clock = new();
        private readonly FixedUser _user = new();
        private readonly FixedTenant _tenant = new();
        private readonly AuditContext _audit = new();
        private readonly string _storageRoot;
        private readonly LocalDiskFileStore _fileStore;

        public AttachmentServiceTests()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            using var db = CreateContext();
            db.Database.EnsureCreated();

            db.DocumentTypes.Add(new DocumentType
            {
                Code = "STU-BIRTH-CERT",
                ModuleCode = "STU",
                AllowedFormats = DocumentFormat.Pdf | DocumentFormat.Jpg,
                MaxSizeBytes = 1024,
            });
            db.DocumentTypes.Add(new DocumentType
            {
                Code = "STU-IQAMA",
                ModuleCode = "STU",
                AllowedFormats = DocumentFormat.Pdf,
                IsExpiryTracked = true,
            });
            db.SaveChanges();

            _storageRoot = Path.Combine(Path.GetTempPath(), "sms-attachment-tests-" + Guid.NewGuid().ToString("N"));
            _fileStore = new LocalDiskFileStore(_storageRoot);
        }

        public void Dispose()
        {
            _connection.Dispose();
            if (Directory.Exists(_storageRoot))
            {
                Directory.Delete(_storageRoot, recursive: true);
            }
        }

        private AppDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            return new AppDbContext(options, _tenant, _user, _clock, _audit);
        }

        private AttachmentService CreateService(AppDbContext db, IVirusScanner? scanner = null)
            => new(db, _fileStore, scanner ?? new NullVirusScanner(), _clock);

        private static byte[] Bytes(string text) => Encoding.UTF8.GetBytes(text);

        // --- upload: policy, hashing, storage (BR-ATT-002/003/010) -----------

        [Fact]
        [BusinessRule("BR-ATT-001")]
        public async Task Uploading_against_an_unknown_document_type_is_rejected()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            await Assert.ThrowsAsync<DocumentTypeNotFoundException>(() =>
                service.UploadAsync("NOT-A-TYPE", "Student", 501, Bytes("x"), "f.pdf", DocumentFormat.Pdf));
        }

        [Fact]
        [BusinessRule("BR-ATT-003")]
        public async Task Uploading_over_the_documenttypes_size_limit_is_rejected()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            var oversized = new byte[2000];

            var ex = await Assert.ThrowsAsync<AttachmentPolicyViolationException>(() =>
                service.UploadAsync("STU-BIRTH-CERT", "Student", 501, oversized, "f.pdf", DocumentFormat.Pdf));
            Assert.Contains(UploadLimitViolation.ExceedsTypeSizeLimit, ex.Violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-008")]
        public async Task An_expiry_tracked_type_rejects_upload_without_an_expiry_date()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var ex = await Assert.ThrowsAsync<AttachmentPolicyViolationException>(() =>
                service.UploadAsync("STU-IQAMA", "Student", 501, Bytes("x"), "iqama.pdf", DocumentFormat.Pdf));
            Assert.Contains(UploadLimitViolation.ExpiryDateRequired, ex.Violations);
        }

        [Fact]
        [BusinessRule("BR-ATT-010")]
        public async Task A_clean_upload_is_hashed_stored_and_readable_back_byte_for_byte()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            var content = Bytes("birth certificate content");

            var version = await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, content, "cert.pdf", DocumentFormat.Pdf);

            Assert.Equal(ScanStatus.Clean, version.ScanStatus);
            Assert.Equal(64, version.ContentHash.Length); // SHA-256 hex
            Assert.Equal(content.Length, version.SizeBytes);

            var readBack = await service.ReadCurrentVersionAsync(
                db.Attachments.Single(a => a.OwningEntityId == 501).Id);
            Assert.Equal(content, readBack);
        }

        // --- storage layout: a folder per document type (BR-ATT-010) ---------
        //
        // The point of the folder is operational, not cosmetic: a school that wants its student
        // photographs — to back them up, to hand them to an ID-card printer, to purge them on a
        // retention date — should not have to work out which of ten thousand GUIDs are the faces.

        [Fact]
        [BusinessRule("BR-ATT-010")]
        public async Task A_stored_file_lands_in_a_folder_named_for_its_document_type()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var version = await service.UploadAsync("STU-BIRTH-CERT", "Student", 601, Bytes("cert"), "cert.pdf", DocumentFormat.Pdf);

            Assert.StartsWith("stu-birth-cert/", version.StorageReference, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(_storageRoot, "stu-birth-cert", Path.GetFileName(version.StorageReference))));

            // Still opaque: the folder says what kind of file it is, the name never says whose.
            Assert.DoesNotContain("601", version.StorageReference, StringComparison.Ordinal);
        }

        [Fact]
        [BusinessRule("BR-ATT-010")]
        public async Task Two_document_types_do_not_share_a_folder()
        {
            using var db = CreateContext();
            var service = CreateService(db);

            var cert = await service.UploadAsync("STU-BIRTH-CERT", "Student", 602, Bytes("cert"), "cert.pdf", DocumentFormat.Pdf);
            var iqama = await service.UploadAsync("STU-IQAMA", "Student", 602, Bytes("iqama"), "iqama.pdf", DocumentFormat.Pdf,
                expiryDateUtc: _clock.UtcNow.AddYears(1));

            Assert.NotEqual(
                Path.GetDirectoryName(cert.StorageReference),
                Path.GetDirectoryName(iqama.StorageReference));
        }

        [Fact]
        [BusinessRule("BR-ATT-010")]
        public async Task A_file_stored_flat_before_folders_existed_still_reads_back()
        {
            // The references already in a live database have no folder in them. Foldering new
            // uploads must not orphan them, which is the whole reason the reference is resolved
            // against the root rather than re-derived from the type.
            var legacyReference = await ((IFileStore)_fileStore).SaveAsync(Bytes("older file"), "old.pdf");

            Assert.DoesNotContain('/', legacyReference);
            Assert.Equal(Bytes("older file"), await _fileStore.ReadAsync(legacyReference));
        }

        [Theory]
        [InlineData("../escape")]
        [InlineData("..\\escape")]
        [InlineData("/rooted")]
        [InlineData("صور")]
        [BusinessRule("BR-SEC-023")]
        public async Task A_category_that_is_not_a_plain_name_never_becomes_one(string folder)
        {
            // A school defines its own document types and may code them in Arabic or with a
            // separator in them. None of that may reach the file system: what survives sanitising
            // is a plain name, or nothing at all and the file stores at the root.
            var reference = await _fileStore.SaveAsync(Bytes("x"), "x.pdf", folder);

            Assert.DoesNotContain("..", reference, StringComparison.Ordinal);
            Assert.DoesNotContain('\\', reference);
            Assert.False(reference.StartsWith('/'));
            Assert.Equal(Bytes("x"), await _fileStore.ReadAsync(reference));
            Assert.StartsWith(Path.GetFullPath(_storageRoot), Path.GetFullPath(Path.Combine(_storageRoot, reference)), StringComparison.Ordinal);
        }

        [Fact]
        [BusinessRule("BR-SEC-023")]
        public async Task A_reference_pointing_outside_the_root_is_refused_rather_than_read()
        {
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _fileStore.ReadAsync("../" + Path.GetFileName(_storageRoot) + "-elsewhere/secret.pdf"));
        }

        // --- versioning: re-upload on the same slot (doc 10 §2) --------------

        [Fact]
        [BusinessRule("BR-ATT-001")]
        public async Task Reuploading_for_the_same_slot_creates_a_new_version_not_a_new_attachment()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("v1"), "a.pdf", DocumentFormat.Pdf);
            var v2 = await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("v2"), "b.pdf", DocumentFormat.Pdf);

            Assert.Single(db.Attachments.Where(a => a.OwningEntityId == 501));
            Assert.Equal(2, v2.VersionNumber);
            Assert.Equal(2, db.AttachmentVersions.Count(v => v.AttachmentId == v2.AttachmentId));
        }

        [Fact]
        [BusinessRule("BR-ATT-001")]
        public async Task Reuploading_resets_verification_on_the_new_content()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("v1"), "a.pdf", DocumentFormat.Pdf);
            var attachmentId = db.Attachments.Single(a => a.OwningEntityId == 501).Id;
            await service.VerifyAsync(attachmentId, verifiedByUserId: 7);
            Assert.NotNull(db.Attachments.Single(a => a.Id == attachmentId).VerifiedAtUtc);

            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("v2"), "b.pdf", DocumentFormat.Pdf);

            Assert.Null(db.Attachments.Single(a => a.Id == attachmentId).VerifiedAtUtc);
        }

        // --- quarantine gate (BR-ATT-009) -------------------------------------

        [Fact]
        [BusinessRule("BR-ATT-009")]
        public async Task An_infected_upload_is_quarantined_and_never_readable_or_verifiable()
        {
            using var db = CreateContext();
            var service = CreateService(db, new InfectedScanner());
            var version = await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("evil"), "a.pdf", DocumentFormat.Pdf);
            var attachmentId = db.Attachments.Single(a => a.OwningEntityId == 501).Id;

            Assert.Equal(ScanStatus.Infected, version.ScanStatus);
            Assert.Equal(AttachmentStatus.Quarantined, db.Attachments.Single(a => a.Id == attachmentId).Status);

            await Assert.ThrowsAsync<AttachmentQuarantinedException>(() => service.ReadCurrentVersionAsync(attachmentId));
            await Assert.ThrowsAsync<AttachmentQuarantinedException>(() => service.VerifyAsync(attachmentId, 7));
        }

        [Fact]
        [BusinessRule("BR-ATT-009")]
        public async Task A_clean_reupload_over_a_quarantined_slot_reactivates_it()
        {
            using var db = CreateContext();
            await CreateService(db, new InfectedScanner()).UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("evil"), "a.pdf", DocumentFormat.Pdf);
            var attachmentId = db.Attachments.Single(a => a.OwningEntityId == 501).Id;

            await CreateService(db, new NullVirusScanner()).UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("clean"), "b.pdf", DocumentFormat.Pdf);

            Assert.Equal(AttachmentStatus.Active, db.Attachments.Single(a => a.Id == attachmentId).Status);
        }

        // --- verify / void (doc 10 §2, BR-ATT-007) ----------------------------

        [Fact]
        [BusinessRule("BR-ATT-007")]
        public async Task Voiding_sets_status_and_reason_and_is_not_a_physical_delete()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("x"), "a.pdf", DocumentFormat.Pdf);
            var attachmentId = db.Attachments.Single(a => a.OwningEntityId == 501).Id;

            await service.VoidAsync(attachmentId, "wrong document uploaded");

            var attachment = db.Attachments.Single(a => a.Id == attachmentId);
            Assert.Equal(AttachmentStatus.Void, attachment.Status);
            Assert.Equal("wrong document uploaded", attachment.VoidReason);
            Assert.NotNull(attachment.VoidedAtUtc);
        }

        [Fact]
        [BusinessRule("BR-ATT-001")]
        public async Task Uploading_again_after_a_void_starts_a_fresh_attachment_slot()
        {
            using var db = CreateContext();
            var service = CreateService(db);
            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("x"), "a.pdf", DocumentFormat.Pdf);
            var firstId = db.Attachments.Single(a => a.OwningEntityId == 501).Id;
            await service.VoidAsync(firstId, "duplicate");

            await service.UploadAsync("STU-BIRTH-CERT", "Student", 501, Bytes("y"), "b.pdf", DocumentFormat.Pdf);

            Assert.Equal(2, db.Attachments.Count(a => a.OwningEntityId == 501));
        }
    }
}
