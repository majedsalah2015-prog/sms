using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sms.Domain.Attachments;
using Sms.Domain.Schools;

namespace Sms.Web.Services
{
    /// <summary>
    /// The logo and the seal a school puts on its official documents (BR-SCH-006,
    /// doc/Modules/02 §8.1).
    /// <para>
    /// Ordinary attachments rather than columns of bytes: the same intake, the same content
    /// inspection, the same scan gate and the same versioning as a birth certificate (doc 10). The
    /// school row keeps a pointer, so replacing a logo is a new version of one slot rather than a
    /// second file nobody can tell apart from the first — which is also what lets a document
    /// issued last year still name the mark it was issued under.
    /// </para>
    /// <para>
    /// <b>Deviation from doc/Modules/02 §9, stated rather than hidden.</b> The doc asks for
    /// "PNG/SVG ≤ 2 MB". SVG is not accepted here and cannot be: <see cref="DocumentFormat"/> has
    /// no SVG member, and an SVG is active content — it can carry script, and this product serves
    /// branding inline to any signed-in reader, so admitting one would be a stored-XSS decision
    /// taken in passing. JPG is accepted alongside PNG instead, because it is verifiable from its
    /// first bytes and is the format a school's existing logo usually arrives in. PNG stays the
    /// recommendation the doc makes, for the transparent background it asks for; the screen says so.
    /// </para>
    /// <para>
    /// Nothing about acceptance is decided here — <see cref="AttachmentIntake"/> owns that for
    /// every file in the product. This class only knows which slot a mark goes in and how large
    /// a mark may be.
    /// </para>
    /// </summary>
    public sealed class SchoolBrandingService
    {
        public const string LogoType = "SCHOOL_LOGO";

        public const string SealType = "SCHOOL_SEAL";

        /// <summary>doc/Modules/02 §9 names the ceiling: two megabytes.</summary>
        public const int MaxBrandingBytes = 2 * 1024 * 1024;

        /// <summary>PNG and JPG. See the class remarks for why the doc's SVG is not among them.</summary>
        public const DocumentFormat BrandingFormats = DocumentFormat.Png | DocumentFormat.Jpg;

        private readonly AttachmentIntake _intake;

        public SchoolBrandingService(AttachmentIntake intake) => _intake = intake;

        /// <summary>Says whether a chosen file can be a branding mark, without storing anything.</summary>
        public static FileRejection Inspect(IFormFile? file)
            => AttachmentIntake.Inspect(file, BrandingFormats, MaxBrandingBytes);

        /// <summary>The document type code backing a slot.</summary>
        public static string TypeCodeOf(SchoolBrandingAsset asset)
            => asset == SchoolBrandingAsset.Logo ? LogoType : SealType;

        /// <summary>
        /// Stores the file in the school's branding slot and returns the attachment id to point the
        /// school row at. The two types are product vocabulary rather than a school's — every school
        /// has a logo — so they are created on first use rather than waiting on a seeder run.
        /// </summary>
        public async Task<int> SaveAsync(
            IFormFile file, SchoolBrandingAsset asset, int schoolId, string moduleCode,
            CancellationToken cancellationToken = default)
        {
            var isLogo = asset == SchoolBrandingAsset.Logo;
            var typeCode = TypeCodeOf(asset);

            await _intake.EnsureTypeAsync(
                typeCode, moduleCode,
                isLogo ? "شعار المدرسة" : "ختم المدرسة",
                isLogo ? "School logo" : "School seal",
                BrandingFormats, MaxBrandingBytes, cancellationToken);

            return await _intake.SaveAsync(
                file, typeCode, SchoolEntity, schoolId, cancellationToken: cancellationToken);
        }

        /// <summary>The stored bytes and the content type to serve them as, or null when there is no usable mark.</summary>
        public Task<AttachmentIntake.StoredFile?> ReadAsync(int? attachmentId, CancellationToken cancellationToken = default)
            => _intake.ReadAsync(attachmentId, cancellationToken);

        /// <summary>What the attachment store calls the owning record — one school per deployment (ADR-2).</summary>
        public const string SchoolEntity = "School";
    }
}
