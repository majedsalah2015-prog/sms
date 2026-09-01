using System;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Schools;

namespace Sms.Application.Schools
{
    /// <summary>doc/Modules/02 §8 "School profile"/"Signatories" screens backing (screens deferred, the operations are core).</summary>
    public interface ISchoolAdmin
    {
        /// <summary>Null schoolId creates a new School; a given id updates it in place (BR-SCH-001 identity fields).</summary>
        Task<School> DefineSchoolAsync(
            int? schoolId,
            string nameAr,
            string nameEn,
            string licenseNumber,
            string ministryCode,
            string timeZoneId,
            string currencyCode,
            string? addressLine = null,
            string? city = null,
            string? contactEmail = null,
            string? contactPhone = null,
            string? website = null,
            DateTime? licenseExpiryDate = null,
            CancellationToken cancellationToken = default);

        /// <summary>Throws <see cref="Common.Exceptions.InvalidSchoolStatusTransitionException"/> on an illegal move (BR-SCH-005).</summary>
        Task ChangeStatusAsync(int schoolId, SchoolStatus newStatus, CancellationToken cancellationToken = default);

        /// <summary>
        /// BR-SCH-006: points a branding slot at a stored attachment, or clears it when
        /// <paramref name="attachmentId"/> is null. Clearing drops the pointer only — the file
        /// itself stays, because doc 10 does not delete documents while the record that owned them
        /// exists (BR-ATT-007), and a certificate issued under the old mark still has to be
        /// explicable afterwards.
        /// <para>
        /// The bytes are stored before this is called; deciding what an acceptable file is belongs
        /// to the attachment intake, not to the school.
        /// </para>
        /// </summary>
        Task SetBrandingAsync(int schoolId, SchoolBrandingAsset asset, int? attachmentId, CancellationToken cancellationToken = default);

        /// <summary>BR-SCH-004: closes out the previous current signatory for the document class (if any) and opens a new one.</summary>
        Task<Signatory> DefineSignatoryAsync(
            string documentClassCode,
            string nameAr,
            string nameEn,
            string titleAr,
            string titleEn,
            DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default);
    }
}
