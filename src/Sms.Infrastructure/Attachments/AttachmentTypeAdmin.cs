using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attachments;
using Sms.Application.Common.Exceptions;
using Sms.Domain.Attachments;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Attachments
{
    public class AttachmentTypeAdmin : IAttachmentTypeAdmin
    {
        private readonly AppDbContext _db;

        public AttachmentTypeAdmin(AppDbContext db)
        {
            _db = db;
        }

        public async Task<DocumentType> DefineDocumentTypeAsync(
            string code,
            string moduleCode,
            string nameAr,
            string nameEn,
            DocumentFormat allowedFormats,
            int? maxSizeBytes,
            bool isMandatoryByDefault,
            bool isExpiryTracked,
            bool isRestricted,
            CancellationToken cancellationToken = default)
        {
            // IgnoreQueryFilters: the code is the identity and the unique index does not care
            // whether a row is active. Looking the upsert up through the soft-active filter finds
            // nothing for a retired type, and the insert that follows collides with the row that
            // is still there. Re-defining a retired code is also how a school un-retires one.
            var type = await _db.DocumentTypes.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Code == code && t.SchoolId == _db.CurrentSchoolId, cancellationToken);
            if (type == null)
            {
                type = new DocumentType { Code = code };
                _db.DocumentTypes.Add(type);
            }

            type.ModuleCode = moduleCode;
            type.Name = new Sms.Domain.Common.LocalizedName(nameAr, nameEn);
            type.AllowedFormats = allowedFormats;
            type.MaxSizeBytes = maxSizeBytes;
            type.IsMandatoryByDefault = isMandatoryByDefault;
            type.IsExpiryTracked = isExpiryTracked;
            type.IsRestricted = isRestricted;
            type.IsActive = true;

            await _db.SaveChangesAsync(cancellationToken);
            return type;
        }

        public async Task SetDocumentTypeActiveAsync(int documentTypeId, bool isActive, CancellationToken cancellationToken = default)
        {
            var type = await _db.DocumentTypes.IgnoreQueryFilters()
                .SingleOrDefaultAsync(t => t.Id == documentTypeId && t.SchoolId == _db.CurrentSchoolId, cancellationToken)
                ?? throw new DocumentTypeNotFoundException(documentTypeId.ToString(CultureInfo.InvariantCulture));

            type.IsActive = isActive;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
