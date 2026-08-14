using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Infrastructure.Common;
using Sms.Infrastructure.Persistence;
using Sms.Infrastructure.Security;

namespace Sms.Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            // E-002 tenancy + working-year context (ADR-2/3). Static single-tenant
            // wiring until M02/M03 provide school resolution and the year switcher.
            var tenant = new StaticTenantContext(
                Configuration.GetValue("Tenant:SchoolId", 1),
                Configuration.GetValue("Tenant:WorkingAcademicYearId", 1));
            services.AddSingleton<ITenantContext>(tenant);
            services.AddSingleton<IWorkingYearContext>(tenant);
            services.AddSingleton<IClock, SystemClock>();
            services.AddSingleton<ICurrentUser, SystemUser>();

            // E-003 authorization core: deny-by-default policy engine (doc 06).
            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("Sms")));
            services.AddScoped<IPermissionService, PermissionService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }
            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
            });
        }
    }
}
