using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Attachments;

namespace Sms.Application.Attachments
{
    /// <summary>doc 10 §5 "Document-type administration" backing (screen deferred, the upsert itself is core).</summary>
    public interface IAttachmentTypeAdmin
    {
        Task<DocumentType> DefineDocumentTypeAsync(
            string code,
            string moduleCode,
            string nameAr,
            string nameEn,
            DocumentFormat allowedFormats,
            int? maxSizeBytes,
            bool isMandatoryByDefault,
            bool isExpiryTracked,
            bool isRestricted,
            CancellationToken cancellationToken = default);
    }
}
