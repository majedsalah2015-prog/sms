using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Sms.Domain.Attachments;

namespace Sms.Web.Services
{
    /// <summary>
    /// The one photograph a person's file carries, for students and staff alike.
    /// <para>
    /// It is an ordinary attachment rather than a column of bytes: the same intake, the same scan
    /// gate, the same storage abstraction and the same versioning as a contract or a birth
    /// certificate (doc 10). The person's row keeps a pointer, so replacing a photo is a new
    /// version of one slot rather than a second file nobody can tell apart from the first.
    /// </para>
    /// <para>
    /// Nothing about acceptance is decided here — <see cref="AttachmentIntake"/> owns that for
    /// every file in the product. This class only knows which slot a face goes in and how large a
    /// face is allowed to be.
    /// </para>
    /// </summary>
    public sealed class PersonPhotoService
    {
        public const string StudentPhotoType = "STUDENT_PHOTO";

        public const string EmployeePhotoType = "EMPLOYEE_PHOTO";

        /// <summary>A face, not a document scan. Two megabytes is generous for one and mean for the other.</summary>
        public const int MaxPhotoBytes = 2 * 1024 * 1024;

        /// <summary>JPEG and PNG only. A PDF of a photograph is a document, and belongs on the documents tab.</summary>
        public const DocumentFormat PhotoFormats = DocumentFormat.Jpg | DocumentFormat.Png;

        private readonly AttachmentIntake _intake;

        public PersonPhotoService(AttachmentIntake intake) => _intake = intake;

        /// <summary>
        /// Says whether a chosen file can be a photograph, without storing anything — so a screen
        /// that creates a person and takes their picture in one submission can refuse the picture
        /// before it creates the person, rather than leaving a half-made record behind a refusal.
        /// </summary>
        public static FileRejection Inspect(IFormFile? file)
            => AttachmentIntake.Inspect(file, PhotoFormats, MaxPhotoBytes);

        /// <summary>
        /// Stores the file as the person's photograph and returns the attachment id to point their
        /// row at. The two photo document types are product vocabulary rather than a school's, so
        /// they are created on first use.
        /// </summary>
        public async Task<int> SaveAsync(
            IFormFile file, string owningEntityType, int owningEntityId, string moduleCode,
            CancellationToken cancellationToken = default)
        {
            var isStudent = owningEntityType == "Student";
            var typeCode = isStudent ? StudentPhotoType : EmployeePhotoType;

            await _intake.EnsureTypeAsync(
                typeCode, moduleCode,
                isStudent ? "صورة الطالب" : "صورة الموظف",
                isStudent ? "Student photo" : "Employee photo",
                PhotoFormats, MaxPhotoBytes, cancellationToken);

            return await _intake.SaveAsync(
                file, typeCode, owningEntityType, owningEntityId, cancellationToken: cancellationToken);
        }

        /// <summary>The stored bytes and the content type to serve them as, or null when there is no usable photo.</summary>
        public Task<AttachmentIntake.StoredFile?> ReadAsync(int? attachmentId, CancellationToken cancellationToken = default)
            => _intake.ReadAsync(attachmentId, cancellationToken);
    }
}
