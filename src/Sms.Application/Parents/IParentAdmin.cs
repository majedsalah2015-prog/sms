using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Parents;

namespace Sms.Application.Parents
{
    /// <summary>
    /// doc/Modules/11 §8 Parent File screens backing (screens deferred, the
    /// operation is core). Issues the parent's permanent file number via
    /// E-006's INumberIssuer (series "PAR"). Registrar-direct creation path
    /// only — the Admissions-portal dedup pipeline (BR-PAR-002) and merge
    /// tool (BR-PAR-003) are deferred.
    /// </summary>
    public interface IParentAdmin
    {
        Task<Parent> RegisterParentAsync(
            string nameAr, string nameEn, string primaryMobile, string? email = null, string? address = null,
            string? occupationEmployer = null, string preferredLanguage = "ar", CancellationToken cancellationToken = default);
    }
}
