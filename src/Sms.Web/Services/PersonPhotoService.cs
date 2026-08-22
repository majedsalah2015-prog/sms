using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attachments;
using Sms.Domain.Attachments;
using Sms.Infrastructure.Persistence;

namespace Sms.Web.Services
{
    /// <summary>
    /// The one photograph a person's file carries, for students and staff alike.
    /// <para>
    /// It is an ordinary attachment rather than a column of bytes: the same scan gate, the same
    /// storage abstraction, the same versioning as a contract or a birth certificate (doc 10). The
    /// person's row keeps a pointer, so replacing a photo is a new version of one slot rather than a
    /// second file nobody can tell apart from the first.
    /// </para>
    /// <para>
    /// The two document types are created on first use. They are product vocabulary rather than a
    /// school's, and a photo upload failing because nobody had run a seeder would be a poor way to
    /// learn that.
    /// </para>
    /// </summary>
    public sealed class PersonPhotoService
    {
        public const string StudentPhotoType = "STUDENT_PHOTO";

        public const string EmployeePhotoType = "EMPLOYEE_PHOTO";

        /// <summary>A face, not a document scan. Two megabytes is generous for one and mean for the other.</summary>
        public const int MaxPhotoBytes = 2 * 1024 * 1024;

        private readonly IAttachmentService _attachments;
        private readonly IAttachmentTypeAdmin _types;
        private readonly AppDbContext _db;

        public PersonPhotoService(IAttachmentService attachments, IAttachmentTypeAdmin types, AppDbContext db)
        {
            _attachments = attachments;
            _types = types;
            _db = db;
        }

        public sealed record Stored(int AttachmentId, string ContentType);

        /// <summary>
        /// Reads the uploaded file, rejects anything that is not a JPEG or PNG within the size limit,
        /// and stores it as the person's photo slot. Returns the attachment id to point the person's
        /// row at.
        /// </summary>
        public async Task<int> SaveAsync(
            IFormFile file, string owningEntityType, int owningEntityId, string moduleCode,
            CancellationToken cancellationToken = default)
        {
            if (file == null || file.Length == 0)
            {
                throw new InvalidOperationException("No file was chosen.");
            }

            if (file.Length > MaxPhotoBytes)
            {
                throw new InvalidOperationException($"A photo must be {MaxPhotoBytes / (1024 * 1024)} MB or smaller.");
            }

            var format = FormatOf(file.FileName, file.ContentType)
                ?? throw new InvalidOperationException("A photo must be a JPEG or a PNG.");

            var typeCode = owningEntityType == "Student" ? StudentPhotoType : EmployeePhotoType;
            await EnsureTypeAsync(typeCode, moduleCode, cancellationToken);

            byte[] content;
            await using (var buffer = new MemoryStream())
            {
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }

            await _attachments.UploadAsync(
                typeCode, owningEntityType, owningEntityId, content, SafeName(file.FileName), format,
                cancellationToken: cancellationToken);

            var attachment = await _db.Attachments.AsNoTracking()
                .Where(a => a.OwningEntityType == owningEntityType
                            && a.OwningEntityId == owningEntityId
                            && a.Status != AttachmentStatus.Void)
                .Join(_db.DocumentTypes.Where(t => t.Code == typeCode), a => a.DocumentTypeId, t => t.Id, (a, t) => a)
                .OrderByDescending(a => a.Id)
                .FirstAsync(cancellationToken);

            return attachment.Id;
        }

        /// <summary>The stored bytes and the content type to serve them as, or null when there is no usable photo.</summary>
        public async Task<(byte[] Content, string ContentType)?> ReadAsync(int? attachmentId, CancellationToken cancellationToken = default)
        {
            if (attachmentId is not int id)
            {
                return null;
            }

            var version = await _db.AttachmentVersions.AsNoTracking()
                .Where(v => v.AttachmentId == id)
                .OrderByDescending(v => v.VersionNumber)
                .FirstOrDefaultAsync(cancellationToken);
            if (version == null)
            {
                return null;
            }

            // A quarantined or still-pending file is not served. ReadCurrentVersionAsync enforces
            // that itself; catching it here turns a scan verdict into "no photo yet" rather than a
            // 500 on somebody's file screen.
            try
            {
                var content = await _attachments.ReadCurrentVersionAsync(id, cancellationToken);
                return (content, version.Format == DocumentFormat.Png ? "image/png" : "image/jpeg");
            }
            catch (Sms.Application.Common.Exceptions.AttachmentQuarantinedException)
            {
                return null;
            }
        }

        private async Task EnsureTypeAsync(string code, string moduleCode, CancellationToken cancellationToken)
        {
            if (await _db.DocumentTypes.AnyAsync(t => t.Code == code && t.IsActive, cancellationToken))
            {
                return;
            }

            var arabic = code == StudentPhotoType ? "صورة الطالب" : "صورة الموظف";
            var english = code == StudentPhotoType ? "Student photo" : "Employee photo";
            await _types.DefineDocumentTypeAsync(
                code, moduleCode, arabic, english,
                DocumentFormat.Jpg | DocumentFormat.Png, MaxPhotoBytes,
                isMandatoryByDefault: false, isExpiryTracked: false, isRestricted: false,
                cancellationToken);
        }

        /// <summary>
        /// The extension decides, with the browser's content type as a second opinion. Neither is
        /// proof of anything — the scan gate is what stands between an upload and a reader.
        /// </summary>
        private static DocumentFormat? FormatOf(string fileName, string? contentType)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            if (extension is ".jpg" or ".jpeg") { return DocumentFormat.Jpg; }
            if (extension == ".png") { return DocumentFormat.Png; }

            return (contentType ?? string.Empty).ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => DocumentFormat.Jpg,
                "image/png" => DocumentFormat.Png,
                _ => null,
            };
        }

        /// <summary>A browser sends whatever the file was called, path and all on some of them.</summary>
        private static string SafeName(string fileName)
        {
            var name = Path.GetFileName(fileName ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "photo" : name;
        }
    }
}
