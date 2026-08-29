using System;
using System.Collections.Generic;
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
    /// <summary>Why a chosen file cannot be stored. Carried instead of a sentence — the screen says why, in the reader's language.</summary>
    public enum FileRejection
    {
        /// <summary>The file is usable.</summary>
        None = 0,

        /// <summary>Nothing was chosen, or the chosen file was empty.</summary>
        NoFile = 1,

        /// <summary>Above the document type's limit (BR-ATT-003).</summary>
        TooLarge = 2,

        /// <summary>The extension and the browser's content type name nothing this product stores.</summary>
        UnknownFormat = 3,

        /// <summary>A format this product understands, but not one this document type accepts (BR-ATT-002).</summary>
        FormatNotAllowed = 4,

        /// <summary>The bytes are not what the name claims — BR-ATT-002 asks for content inspection, not extension alone.</summary>
        ContentMismatch = 5,

        /// <summary>The document type is expiry-tracked and no date was given (BR-ATT-008).</summary>
        ExpiryDateRequired = 6,

        /// <summary>No such document type, or it has been retired.</summary>
        UnknownDocumentType = 7,
    }

    /// <summary>
    /// A refusal carrying its reason and the limits that produced it, never its wording. The
    /// intake has no language; the screen that catches this says why in the reader's, which is
    /// the rule the product runs on and the reason an English sentence is never thrown from here.
    /// </summary>
    public sealed class FileRejectedException : InvalidOperationException
    {
        public FileRejectedException(FileRejection rejection, DocumentFormat allowedFormats = 0, long maxBytes = 0)
            : base($"The chosen file cannot be stored ({rejection}).")
        {
            Rejection = rejection;
            AllowedFormats = allowedFormats;
            MaxBytes = maxBytes;
        }

        public FileRejection Rejection { get; }

        /// <summary>What the document type does accept, so the message can say so rather than only refusing.</summary>
        public DocumentFormat AllowedFormats { get; }

        /// <summary>The size ceiling that was exceeded, in bytes.</summary>
        public long MaxBytes { get; }
    }

    /// <summary>
    /// The one gate every uploaded file in this product passes through — a student's photograph,
    /// a birth certificate, an employee contract, a bus insurance paper (doc 10 §2/§5).
    /// <para>
    /// Screens differ in what they ask for and how they show it; none of them decides what a file
    /// is, whether it may be stored, or where the bytes go. That lives here once, on top of
    /// <see cref="IAttachmentService"/> and its upload/version/scan/void pipeline, so the answer
    /// to "is this an acceptable file" cannot drift between the registration form and the file
    /// screen.
    /// </para>
    /// <para>
    /// BR-ATT-002 asks for content inspection rather than extension alone, so the declared format
    /// is checked against the first bytes: a renamed executable is refused even when its name and
    /// the browser both call it a PDF.
    /// </para>
    /// </summary>
    public sealed class AttachmentIntake
    {
        private readonly IAttachmentService _attachments;
        private readonly IAttachmentTypeAdmin _types;
        private readonly AppDbContext _db;

        public AttachmentIntake(IAttachmentService attachments, IAttachmentTypeAdmin types, AppDbContext db)
        {
            _attachments = attachments;
            _types = types;
            _db = db;
        }

        /// <summary>Stored bytes, with what to serve them as and what to call them on the way out.</summary>
        public sealed record StoredFile(byte[] Content, string ContentType, string FileName);

        /// <summary>One row of an entity's document list — the slot and its current version, flattened for a screen.</summary>
        public sealed record DocumentRow(
            int AttachmentId,
            string TypeCode,
            string TypeNameAr,
            string TypeNameEn,
            bool TypeIsRestricted,
            string? TitleAr,
            string? TitleEn,
            string FileName,
            DocumentFormat Format,
            long SizeBytes,
            int VersionNumber,
            AttachmentStatus Status,
            ScanStatus ScanStatus,
            DateTime UploadedAtUtc,
            DateTime? ExpiryDateUtc,
            DateTime? VerifiedAtUtc);

        // ------------------------------------------------------------------ inspection

        /// <summary>
        /// Says whether a chosen file can be stored under these rules, without storing anything —
        /// so a screen that creates a person and takes their document in one submission can refuse
        /// the document before it creates the person, rather than leaving a half-made record
        /// behind a refusal.
        /// </summary>
        public static FileRejection Inspect(IFormFile? file, DocumentFormat allowedFormats, long maxBytes)
        {
            if (file == null || file.Length == 0) { return FileRejection.NoFile; }
            if (file.Length > maxBytes) { return FileRejection.TooLarge; }

            var format = FormatOf(file.FileName, file.ContentType);
            if (format == null) { return FileRejection.UnknownFormat; }
            if ((allowedFormats & format.Value) == 0) { return FileRejection.FormatNotAllowed; }

            return FileRejection.None;
        }

        /// <summary>
        /// What this product would call the file: the extension decides, with the browser's
        /// content type as a second opinion. Neither is proof of anything — <see cref="ContentMatches"/>
        /// and the scan gate are what stand between an upload and a reader.
        /// </summary>
        public static DocumentFormat? FormatOf(string? fileName, string? contentType)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
            switch (extension)
            {
                case ".jpg":
                case ".jpeg": return DocumentFormat.Jpg;
                case ".png": return DocumentFormat.Png;
                case ".pdf": return DocumentFormat.Pdf;
                case ".docx": return DocumentFormat.Docx;
                case ".xlsx": return DocumentFormat.Xlsx;
            }

            return (contentType ?? string.Empty).ToLowerInvariant() switch
            {
                "image/jpeg" or "image/jpg" => DocumentFormat.Jpg,
                "image/png" => DocumentFormat.Png,
                "application/pdf" => DocumentFormat.Pdf,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => DocumentFormat.Docx,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => DocumentFormat.Xlsx,
                _ => null,
            };
        }

        /// <summary>
        /// BR-ATT-002 content inspection: the first bytes have to agree with the name. An
        /// executable renamed to .pdf fails here, which is the point of the rule — an extension is
        /// a claim, not evidence.
        /// </summary>
        public static bool ContentMatches(DocumentFormat format, byte[] content) => format switch
        {
            DocumentFormat.Jpg => StartsWith(content, 0xFF, 0xD8, 0xFF),
            DocumentFormat.Png => StartsWith(content, 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A),
            DocumentFormat.Pdf => StartsWith(content, 0x25, 0x50, 0x44, 0x46),

            // Both Office formats are zip containers; which parts sit inside one is the package's
            // business, not the store's.
            DocumentFormat.Docx or DocumentFormat.Xlsx => StartsWith(content, 0x50, 0x4B),
            _ => false,
        };

        /// <summary>What a browser should be told a stored file is, on the way back out.</summary>
        public static string ContentTypeOf(DocumentFormat format) => format switch
        {
            DocumentFormat.Png => "image/png",
            DocumentFormat.Jpg => "image/jpeg",
            DocumentFormat.Pdf => "application/pdf",
            DocumentFormat.Docx => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            DocumentFormat.Xlsx => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            _ => "application/octet-stream",
        };

        /// <summary>True for the formats a browser can show inline, which is what earns a thumbnail instead of an icon.</summary>
        public static bool IsImage(DocumentFormat format) => format is DocumentFormat.Jpg or DocumentFormat.Png;

        // ------------------------------------------------------------------ storing

        /// <summary>
        /// Reads the chosen file, judges it against the document type's own rules, and stores it in
        /// that (owning entity, type) slot — a new version if the slot is already filled (doc 10 §2
        /// "Version"). Returns the attachment id, which is what an owning row points at.
        /// </summary>
        public async Task<int> SaveAsync(
            IFormFile? file,
            string typeCode,
            string owningEntityType,
            long owningEntityId,
            string? titleAr = null,
            string? titleEn = null,
            DateTime? expiryDateUtc = null,
            CancellationToken cancellationToken = default)
        {
            var type = await _db.DocumentTypes.AsNoTracking()
                .SingleOrDefaultAsync(t => t.Code == typeCode, cancellationToken);
            if (type == null) { throw new FileRejectedException(FileRejection.UnknownDocumentType); }

            var maxBytes = EffectiveMaxBytes(type);
            var rejection = Inspect(file, type.AllowedFormats, maxBytes);
            if (rejection != FileRejection.None)
            {
                throw new FileRejectedException(rejection, type.AllowedFormats, maxBytes);
            }

            if (type.IsExpiryTracked && expiryDateUtc == null)
            {
                throw new FileRejectedException(FileRejection.ExpiryDateRequired, type.AllowedFormats, maxBytes);
            }

            var format = FormatOf(file!.FileName, file.ContentType)!.Value;

            byte[] content;
            await using (var buffer = new MemoryStream())
            {
                await file.CopyToAsync(buffer, cancellationToken);
                content = buffer.ToArray();
            }

            if (!ContentMatches(format, content))
            {
                throw new FileRejectedException(FileRejection.ContentMismatch, type.AllowedFormats, maxBytes);
            }

            // The version comes back with its attachment's key already filled in, so the slot is
            // never looked up again afterwards — a re-query would answer "the newest slot" rather
            // than "the one just written", and two clerks uploading at once would find out why
            // those are different questions.
            var version = await _attachments.UploadAsync(
                typeCode, owningEntityType, owningEntityId, content, SafeName(file.FileName), format,
                titleAr, titleEn, expiryDateUtc, cancellationToken);

            return version.AttachmentId;
        }

        /// <summary>The stored bytes and how to serve them, or null when there is nothing readable — no slot, no version, or a file the scan gate holds back (BR-ATT-009).</summary>
        public async Task<StoredFile?> ReadAsync(int? attachmentId, CancellationToken cancellationToken = default)
        {
            if (attachmentId is not int id) { return null; }

            var version = await _db.AttachmentVersions.AsNoTracking()
                .Where(v => v.AttachmentId == id)
                .OrderByDescending(v => v.VersionNumber)
                .Select(v => new { v.Format, v.FileName })
                .FirstOrDefaultAsync(cancellationToken);
            if (version == null) { return null; }

            // A quarantined or still-pending file is not served. ReadCurrentVersionAsync enforces
            // that itself; catching it here turns a scan verdict into "nothing to show" rather
            // than a 500 on somebody's file screen.
            try
            {
                var content = await _attachments.ReadCurrentVersionAsync(id, cancellationToken);
                return new StoredFile(content, ContentTypeOf(version.Format), version.FileName);
            }
            catch (Sms.Application.Common.Exceptions.AttachmentQuarantinedException)
            {
                return null;
            }
        }

        // ------------------------------------------------------------------ listing

        /// <summary>
        /// Everything filed against one record, newest first, each slot flattened onto its current
        /// version. Restricted types are dropped unless the caller says the reader holds the
        /// restricted category (BR-ATT-004) — dropped rather than greyed out, because a disabled
        /// row still says a document of that kind exists and whom it is about.
        /// </summary>
        public async Task<IReadOnlyList<DocumentRow>> ListAsync(
            string owningEntityType, long owningEntityId, bool canSeeRestricted, CancellationToken cancellationToken = default)
        {
            // The type is read past the soft-active filter: retiring a document type must not make
            // the documents already filed under it vanish from the file that owns them. The picker
            // below is the filtered list, answering the different question of what may be filed now.
            // IgnoreQueryFilters drops the tenant filter along with the soft-active one, so the
            // school is put back by hand — a filter turned off for one reason must not quietly be
            // off for the other.
            var types = _db.DocumentTypes.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.SchoolId == _db.CurrentSchoolId);

            var rows = await (
                from a in _db.Attachments.AsNoTracking()
                where a.OwningEntityType == owningEntityType && a.OwningEntityId == owningEntityId
                join t in types on a.DocumentTypeId equals t.Id
                join v in _db.AttachmentVersions.AsNoTracking() on a.Id equals v.AttachmentId
                where v.VersionNumber == a.CurrentVersionNumber
                select new DocumentRow(
                    a.Id, t.Code, t.Name.NameAr, t.Name.NameEn, t.IsRestricted,
                    a.TitleAr, a.TitleEn, v.FileName, v.Format, v.SizeBytes, v.VersionNumber,
                    a.Status, v.ScanStatus, v.CreatedAtUtc, a.ExpiryDateUtc, a.VerifiedAtUtc))
                .ToListAsync(cancellationToken);

            return rows
                .Where(r => canSeeRestricted || !r.TypeIsRestricted)
                .OrderByDescending(r => r.UploadedAtUtc)
                .ToList();
        }

        /// <summary>
        /// The document types a module may file against, for the picker — the filtered list, so a
        /// retired type is never offered even though documents already filed under it stay readable.
        /// </summary>
        public async Task<IReadOnlyList<DocumentType>> TypesForAsync(
            string moduleCode, bool includeRestricted, CancellationToken cancellationToken = default)
        {
            var types = await _db.DocumentTypes.AsNoTracking()
                .Where(t => t.ModuleCode == moduleCode)
                .ToListAsync(cancellationToken);

            return types
                .Where(t => includeRestricted || !t.IsRestricted)
                .OrderBy(t => t.Name.NameEn, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>BR-ATT-007: a document is voided with a reason, never removed while the record that owns it exists.</summary>
        public Task VoidAsync(int attachmentId, string reason, CancellationToken cancellationToken = default)
            => _attachments.VoidAsync(attachmentId, reason, cancellationToken);

        /// <summary>doc 10 §2 "Verification" — staff sighted the original and it matches.</summary>
        public Task VerifyAsync(int attachmentId, int verifiedByUserId, CancellationToken cancellationToken = default)
            => _attachments.VerifyAsync(attachmentId, verifiedByUserId, cancellationToken);

        /// <summary>Which record a stored document belongs to, so a download can be refused before the bytes are read.</summary>
        public Task<(string OwningEntityType, long OwningEntityId)> OwnerOfAsync(int attachmentId, CancellationToken cancellationToken = default)
            => _db.Attachments.AsNoTracking()
                .Where(a => a.Id == attachmentId)
                .Select(a => new ValueTuple<string, long>(a.OwningEntityType, a.OwningEntityId))
                .SingleOrDefaultAsync(cancellationToken);

        // ------------------------------------------------------------------ types

        /// <summary>
        /// The document type as the catalogue holds it, or null when nobody has defined it. Screens
        /// read this to know what to accept and how large a file may be, so the box on the page and
        /// the rule on the server cannot disagree.
        /// </summary>
        public async Task<DocumentType?> TypeAsync(string code, CancellationToken cancellationToken = default)
            => await _db.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code, cancellationToken);

        /// <summary>
        /// Defines a type the product itself owns — a person's photograph, not a school's ministry
        /// form — on first use. Product vocabulary belongs to the product, and a photo upload
        /// failing because nobody had run a seeder would be a poor way to learn that.
        /// </summary>
        public async Task<DocumentType> EnsureTypeAsync(
            string code, string moduleCode, string nameAr, string nameEn,
            DocumentFormat allowedFormats, int maxSizeBytes, CancellationToken cancellationToken = default)
        {
            var existing = await _db.DocumentTypes.AsNoTracking().SingleOrDefaultAsync(t => t.Code == code, cancellationToken);
            if (existing is { IsActive: true }) { return existing; }

            return await _types.DefineDocumentTypeAsync(
                code, moduleCode, nameAr, nameEn, allowedFormats, maxSizeBytes,
                isMandatoryByDefault: false, isExpiryTracked: false, isRestricted: false,
                cancellationToken);
        }

        /// <summary>BR-ATT-003: the type's own limit, never above the product ceiling.</summary>
        public static long EffectiveMaxBytes(DocumentType type)
            => Math.Min(type.MaxSizeBytes ?? UploadLimitPolicy.ProductDefaultSizeBytes, UploadLimitPolicy.ProductCeilingSizeBytes);

        /// <summary>A browser sends whatever the file was called, path and all on some of them.</summary>
        public static string SafeName(string? fileName)
        {
            var name = Path.GetFileName(fileName ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) ? "file" : name;
        }

        private static bool StartsWith(byte[] content, params byte[] signature)
        {
            if (content.Length < signature.Length) { return false; }
            for (var i = 0; i < signature.Length; i++)
            {
                if (content[i] != signature[i]) { return false; }
            }

            return true;
        }
    }
}
