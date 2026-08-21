using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Security;

namespace Sms.Application.Security
{
    /// <summary>
    /// Authorization port for screens, menus, and repositories. Implementations
    /// resolve the current user's grants (cached per session, T-8) and evaluate
    /// via <see cref="PermissionEvaluator"/>.
    /// </summary>
    public interface IPermissionService
    {
        Task<bool> HasPermissionAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default);

        /// <summary>Null = not granted (deny by default, BR-GLB-070).</summary>
        Task<EffectiveScope?> GetEffectiveScopeAsync(string moduleCode, string screenCode, ActionVerb action, CancellationToken cancellationToken = default);

        /// <summary>
        /// Every screen code granted to <paramref name="userAccountId"/> under one
        /// module and verb. Takes the user explicitly rather than reading the
        /// ambient <c>ICurrentUser</c>, because its caller is sign-in: the
        /// principal does not exist yet at the moment the claims are being built
        /// (<see cref="IExternalPermissionCatalog"/>).
        /// </summary>
        Task<IReadOnlyList<string>> GetGrantedScreenCodesAsync(int userAccountId, string moduleCode, ActionVerb action, CancellationToken cancellationToken = default);
    }
}
