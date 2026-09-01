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

        /// <summary>
        /// BR-GLB-005: retires a document type or puts it back. The catalog could only ever grow —
        /// a type entered against the wrong module, or one a school stopped collecting, stayed in
        /// every attachment picker for good.
        /// <para>
        /// Files already filed under the type are untouched and stay readable: retiring it stops
        /// it being offered for the next upload, which is why this is a flag and not a delete.
        /// </para>
        /// </summary>
        Task SetDocumentTypeActiveAsync(int documentTypeId, bool isActive, CancellationToken cancellationToken = default);
    }
}
