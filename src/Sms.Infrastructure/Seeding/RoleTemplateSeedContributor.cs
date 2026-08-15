using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Seeding;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// doc 06 §4.3's 21 seeded role templates (E-003's deferred remaining
    /// item). Shells only — Code/Name plus the 2FA/single-session defaults
    /// doc 06 calls out by name ("default ON for System Admin and Finance
    /// roles"; "single-session policy configurable for finance roles").
    /// Permission grants per role are a content-curation task across ~340
    /// module BRs, not an engineering one — out of scope here; schools/
    /// product admins assign permissions via the (deferred) role designer
    /// screen once RolePermission rows are curated.
    /// </summary>
    public class RoleTemplateSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;

        public RoleTemplateSeedContributor(AppDbContext db)
        {
            _db = db;
        }

        public string Name => "Role templates (doc 06 §4.3)";

        public int Order => 20;

        private static readonly (string Code, string Ar, string En, bool RequireTwoFactor, bool EnforceSingleSession)[] Templates =
        {
            ("SYSADMIN", "مدير النظام", "System Administrator", true, false),
            ("PRINCIPAL", "المدير", "Principal", false, false),
            ("VICE_PRINCIPAL", "نائب المدير", "Vice Principal", false, false),
            ("STAGE_SUPERVISOR", "مشرف مرحلة", "Stage Supervisor", false, false),
            ("REGISTRAR", "أمين السجلات", "Registrar", false, false),
            ("ADMISSIONS_OFFICER", "موظف قبول", "Admissions Officer", false, false),
            ("FINANCE_MANAGER", "المدير المالي", "Finance Manager", true, true),
            ("CASHIER", "أمين الصندوق", "Cashier", false, true),
            ("HR_OFFICER", "موظف موارد بشرية", "HR Officer", false, false),
            ("TEACHER", "معلم", "Teacher", false, false),
            ("HOMEROOM_TEACHER", "معلم الفصل", "Homeroom Teacher", false, false),
            ("HEAD_OF_DEPARTMENT", "رئيس قسم", "Head of Department", false, false),
            ("NURSE", "ممرض/ة", "Nurse", false, false),
            ("LIBRARIAN", "أمين مكتبة", "Librarian", false, false),
            ("STOREKEEPER", "أمين مخزن", "Storekeeper", false, false),
            ("CAFETERIA_OPERATOR", "مشغل المقصف", "Cafeteria Operator", false, false),
            ("TRANSPORT_SUPERVISOR", "مشرف النقل", "Transport Supervisor", false, false),
            ("RECEPTIONIST", "موظف استقبال", "Receptionist", false, false),
            ("PARENT", "ولي أمر", "Parent", false, false),
            ("STUDENT", "طالب", "Student", false, false),
            ("AUDITOR", "مدقق", "Auditor", false, false),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            foreach (var template in Templates)
            {
                var role = await _db.Roles.SingleOrDefaultAsync(r => r.Code == template.Code, cancellationToken);
                if (role == null)
                {
                    role = new Role { Code = template.Code };
                    _db.Roles.Add(role);
                }

                role.Name = new LocalizedName(template.Ar, template.En);
                role.RequireTwoFactor = template.RequireTwoFactor;
                role.EnforceSingleSession = template.EnforceSingleSession;
                role.IsActive = true;
            }

            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
