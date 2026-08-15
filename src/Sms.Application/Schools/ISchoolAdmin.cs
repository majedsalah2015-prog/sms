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
