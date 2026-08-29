using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Schools;
using Sms.Domain.Schools;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Schools
{
    /// <summary>
    /// Standalone admin operations — save themselves, no larger transaction
    /// to ride. Identity-field and status changes are BR-SCH-002/005
    /// T1-audited with a mandatory reason — set via the ambient
    /// IAuditContext.Reason before calling, same pattern as every other
    /// T1-tagged entity in the codebase; this class does nothing special,
    /// the generic SmsDbContext pipeline enforces it.
    /// </summary>
    public class SchoolAdmin : ISchoolAdmin
    {
        private readonly AppDbContext _db;

        public SchoolAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<School> DefineSchoolAsync(
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
            CancellationToken cancellationToken = default)
        {
            School school;
            if (schoolId is int id)
            {
                school = await _db.Schools.SingleAsync(s => s.Id == id, cancellationToken);
            }
            else
            {
                school = new School();
                _db.Schools.Add(school);
            }

            school.NameAr = nameAr;
            school.NameEn = nameEn;
            school.LicenseNumber = licenseNumber;
            school.MinistryCode = ministryCode;
            school.TimeZoneId = timeZoneId;
            school.CurrencyCode = currencyCode;
            school.AddressLine = addressLine;
            school.City = city;
            school.ContactEmail = contactEmail;
            school.ContactPhone = contactPhone;
            school.Website = website;
            school.LicenseExpiryDate = licenseExpiryDate;

            await _db.SaveChangesAsync(cancellationToken);
            return school;
        }

        public async Task ChangeStatusAsync(int schoolId, SchoolStatus newStatus, CancellationToken cancellationToken = default)
        {
            var school = await _db.Schools.SingleAsync(s => s.Id == schoolId, cancellationToken);
            if (!SchoolStatusTransitions.CanTransition(school.Status, newStatus))
            {
                throw new InvalidSchoolStatusTransitionException(school.Status, newStatus);
            }

            school.Status = newStatus;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task SetBrandingAsync(int schoolId, SchoolBrandingAsset asset, int? attachmentId, CancellationToken cancellationToken = default)
        {
            var school = await _db.Schools.SingleAsync(s => s.Id == schoolId, cancellationToken);

            switch (asset)
            {
                case SchoolBrandingAsset.Logo:
                    school.LogoAttachmentId = attachmentId;
                    break;
                case SchoolBrandingAsset.Seal:
                    school.SealAttachmentId = attachmentId;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(asset), asset, null);
            }

            // No reason is set or required: the branding pointers carry no [RequiresAuditReason],
            // so the T1 tag on School records which slot changed without demanding the sentence
            // BR-SCH-002 asks of a name or a licence number.
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Signatory> DefineSignatoryAsync(
            string documentClassCode,
            string nameAr,
            string nameEn,
            string titleAr,
            string titleEn,
            DateTime effectiveFromUtc,
            CancellationToken cancellationToken = default)
        {
            var current = await _db.Signatories.SingleOrDefaultAsync(
                s => s.DocumentClassCode == documentClassCode && s.EffectiveToUtc == null, cancellationToken);
            if (current != null)
            {
                current.EffectiveToUtc = effectiveFromUtc;
            }

            var signatory = new Signatory
            {
                DocumentClassCode = documentClassCode,
                NameAr = nameAr,
                NameEn = nameEn,
                TitleAr = titleAr,
                TitleEn = titleEn,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.Signatories.Add(signatory);

            await _db.SaveChangesAsync(cancellationToken);
            return signatory;
        }
    }
}
