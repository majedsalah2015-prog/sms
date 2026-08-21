using ERP2028.Application.Abstractions.Identity;
using ERP2028.Application.Abstractions.Time;
using Microsoft.Extensions.DependencyInjection;
using Sms.Erp.Bridge.GlPosting;
using Sms.Erp.Bridge.Identity;
using Sms.Application.GlExport;
using Sms.Application.Security;
using Sms.Erp.Bridge.Time;

namespace Sms.Erp.Bridge.DependencyInjection
{
    /// <summary>
    /// Registers everything the ERP modules expect their host to provide, backed
    /// by this system's own services.
    /// <para>
    /// The list is deliberately short, and that is the finding worth keeping: the
    /// ERP asks its host for a clock, the current user, and a way to name users.
    /// Everything else it brings with it. <c>IFileStore</c> and
    /// <c>ISupportAgentDirectory</c> are not registered because neither the
    /// Accounting nor the Organization module resolves them; registering
    /// abstractions nobody asks for would only hide, later, which ones actually
    /// matter.
    /// </para>
    /// <para>
    /// The host still owns the two things that are its own decisions and not
    /// adaptations: the shared connection (which database, which provider) and
    /// the permission catalog (which modules exist). Those stay in
    /// <c>Sms.Web/Startup.cs</c> where they can be read next to the module
    /// registrations they belong to.
    /// </para>
    /// </summary>
    public static class ErpBridgeRegistration
    {
        public static IServiceCollection AddErpHostAdapters(this IServiceCollection services)
        {
            // Singleton, matching both sides: this system registers IClock as a singleton and the ERP
            // host registers its own IDateTime the same way, so the wrapper adds no lifetime of its own.
            services.AddSingleton<IDateTime, ErpClockAdapter>();

            services.AddScoped<ICurrentUser, ErpCurrentUserAdapter>();
            services.AddScoped<IUserDirectory, ErpUserDirectoryAdapter>();

            // The direction the other three do not go: this one lets the school reach the ledger,
            // rather than letting the ledger reach the school. Registering it is what turns E-503's
            // CSV export into a real posting — and not registering it leaves the CSV, which is a
            // supported way to run (IGlPostingPort).
            services.AddScoped<IGlPostingPort, ErpGlPostingAdapter>();

            // Fills the mapping table from the ERP chart, so the first batch can be generated without
            // an administrator transcribing account codes. Registered here and resolved by the seeder.
            services.AddScoped<IGlAccountProvisioner, ErpGlAccountProvisioner>();

            return services;
        }

        /// <summary>
        /// Declares the ERP permission names to this system's catalogue, so they become grantable on
        /// the ordinary role screen.
        /// <para>
        /// Separate from <see cref="AddErpHostAdapters"/> because the two have different callers. The
        /// adapters are web-host services — the current user comes off an HTTP context. This one is
        /// needed by the seeder tool, which has no request and must not be made to resolve services
        /// that assume one. Both hosts call this; only the web host calls the other.
        /// </para>
        /// </summary>
        public static IServiceCollection AddErpPermissionCatalog(this IServiceCollection services)
        {
            services.AddSingleton<IExternalPermissionCatalog, ErpPermissionCatalog>();
            return services;
        }
    }
}
