using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Attachments;
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
            var type = await _db.DocumentTypes.SingleOrDefaultAsync(t => t.Code == code, cancellationToken);
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
    }
}
