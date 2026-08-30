using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Security;
using Sms.Application.Seeding;
using Sms.Domain.Security;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// Catalogues <see cref="ScreenCatalog"/> into <c>sec.Permission</c> and gives
    /// the seeded role templates a working default set of grants — the curation
    /// <c>RoleTemplateSeedContributor</c> deferred, without which every role was a
    /// named shell holding nothing and <c>[RequirePermission]</c> would lock all
    /// of them out of everything.
    /// <para>
    /// <b>Defaults, not policy.</b> Grants are seeded only for a role that holds
    /// none at all — first provisioning. A school that has curated a role owns it
    /// from then on, and a revoked grant stays revoked across restarts. The one
    /// visible edge: stripping a role down to zero grants makes it look
    /// unprovisioned, and the defaults return. Leaving a single grant in place
    /// prevents that, and the role designer will make it explicit.
    /// </para>
    /// <para>
    /// The matrix below is a starting point a school adjusts, not an assertion
    /// about how every school is organised. It is written to be defensible rather
    /// than generous: a cashier takes money and prints receipts but cannot touch
    /// the fee structure or grant a discount, and no role except the system
    /// administrator can both define a discount type and approve a grant under it.
    /// </para>
    /// </summary>
    public class PermissionSeedContributor : ISeedContributor
    {
        private const string SystemAdministrator = "SYSADMIN";
        private const string AnyModule = "*";
        private const string AnyScreen = "*";

        private readonly AppDbContext _db;

        public PermissionSeedContributor(AppDbContext db)
        {
            _db = db;
        }

        public string Name => "Screen permissions and role defaults (doc 06 §4)";

        // Between the role templates (20) and the hosted subsystems' permissions
        // (22), so the roles exist to be granted and the ERP's grants land on top.
        public int Order => 21;

        // ------------------------------------------------------------------ the role matrix

        private sealed record Grant(string RoleCode, string ModuleCode, string ScreenCode, ActionVerb[]? Verbs);

        private static readonly ActionVerb[] ReadPrintExport = { ActionVerb.View, ActionVerb.Print, ActionVerb.Export };
        private static readonly ActionVerb[] Oversight = { ActionVerb.View, ActionVerb.Print, ActionVerb.Export, ActionVerb.Approve };
        private static readonly ActionVerb[] Read = { ActionVerb.View };

        private static readonly Grant[] Matrix =
        {
            // The one role that can change what the roles themselves may do.
            new(SystemAdministrator, AnyModule, AnyScreen, null),

            // Sees everything and decides anything; creates and edits nothing directly.
            // Approve only lands where a screen defines it, so this is narrower than it looks.
            new("PRINCIPAL", AnyModule, AnyScreen, Oversight),
            new("VICE_PRINCIPAL", AnyModule, AnyScreen, ReadPrintExport),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Attendance, AnyScreen, new[] { ActionVerb.Approve }),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Grading, AnyScreen, new[] { ActionVerb.Approve }),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Timetable, AnyScreen, new[] { ActionVerb.Approve }),

            // Read-only across the product, plus the file exports an audit needs.
            new("AUDITOR", AnyModule, AnyScreen, ReadPrintExport),

            // A stage's academics, read-mostly; the one thing it decides is an attendance correction.
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Attendance, AnyScreen, new[] { ActionVerb.View, ActionVerb.Approve }),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Grading, AnyScreen, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Sections, AnyScreen, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Students, AnyScreen, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Timetable, AnyScreen, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Subjects, AnyScreen, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Grades, AnyScreen, Read),

            // The people record is this role's own.
            new("REGISTRAR", ScreenCatalog.Modules.Students, AnyScreen, null),
            new("REGISTRAR", ScreenCatalog.Modules.Parents, AnyScreen, null),
            new("REGISTRAR", ScreenCatalog.Modules.Admissions, AnyScreen, null),
            new("REGISTRAR", ScreenCatalog.Modules.Sections, AnyScreen, null),
            new("REGISTRAR", ScreenCatalog.Modules.AcademicYears, AnyScreen, Read),
            new("REGISTRAR", ScreenCatalog.Modules.Grades, AnyScreen, Read),
            new("REGISTRAR", ScreenCatalog.Modules.Subjects, AnyScreen, Read),
            new("REGISTRAR", ScreenCatalog.Modules.Classrooms, AnyScreen, Read),
            new("REGISTRAR", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.ReportCard, null),

            new("ADMISSIONS_OFFICER", ScreenCatalog.Modules.Admissions, AnyScreen, null),
            new("ADMISSIONS_OFFICER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, null),
            new("ADMISSIONS_OFFICER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, new[] { ActionVerb.View, ActionVerb.Edit }),
            new("ADMISSIONS_OFFICER", ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, null),
            new("ADMISSIONS_OFFICER", ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.File, new[] { ActionVerb.View, ActionVerb.Edit }),

            // Finance end to end, including the seam to the ledger.
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Fees, AnyScreen, null),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Payments, AnyScreen, null),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Installments, AnyScreen, null),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Discounts, AnyScreen, null),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Reports, AnyScreen, ReadPrintExport),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("FINANCE_MANAGER", ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, Read),

            // Takes money and accounts for the drawer. Cannot price anything, cannot forgive anything.
            new("CASHIER", ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Cashier, null),
            new("CASHIER", ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Till, null),
            new("CASHIER", ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Pdc, null),
            new("CASHIER", ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Refunds, new[] { ActionVerb.View, ActionVerb.Submit }),
            new("CASHIER", ScreenCatalog.Modules.Payments, ScreenCatalog.Payments.Allocations, Read),
            new("CASHIER", ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Charges, Read),
            new("CASHIER", ScreenCatalog.Modules.Fees, ScreenCatalog.Fees.Position, null),
            new("CASHIER", ScreenCatalog.Modules.Installments, ScreenCatalog.Installments.Schedule, Read),
            new("CASHIER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("CASHIER", ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, Read),

            new("HR_OFFICER", ScreenCatalog.Modules.Employees, AnyScreen, null),
            new("HR_OFFICER", ScreenCatalog.Modules.Teachers, AnyScreen, Read),

            // Marks and attendance for their own classes — the scope grants narrow "own", not this list.
            new("TEACHER", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, new[] { ActionVerb.View, ActionVerb.Edit }),
            new("TEACHER", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, new[] { ActionVerb.View, ActionVerb.Edit, ActionVerb.Submit }),
            new("TEACHER", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Results, Read),
            new("TEACHER", ScreenCatalog.Modules.Timetable, ScreenCatalog.Timetable.Builder, Read),
            new("TEACHER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("TEACHER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, Read),
            new("TEACHER", ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, Read),

            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Capture, new[] { ActionVerb.View, ActionVerb.Edit }),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Justifications, new[] { ActionVerb.View, ActionVerb.Submit }),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Analytics, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, new[] { ActionVerb.View, ActionVerb.Edit, ActionVerb.Submit }),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.ReportCard, null),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Roster, null),

            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Subjects, AnyScreen, Read),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Grading, AnyScreen, Read),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Grading, ScreenCatalog.Grading.Marksheets, new[] { ActionVerb.View, ActionVerb.Edit, ActionVerb.Submit }),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Teachers, AnyScreen, Read),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Timetable, ScreenCatalog.Timetable.Builder, Read),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Analytics, Read),

            // The gate is a front-desk job; the register is not.
            new("RECEPTIONIST", ScreenCatalog.Modules.Attendance, ScreenCatalog.Attendance.Gate, null),
            new("RECEPTIONIST", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("RECEPTIONIST", ScreenCatalog.Modules.Parents, ScreenCatalog.Parents.Directory, Read),

            // Modules 23-28 have no screens yet, so these four roles start with the cross-cutting
            // lookup their work actually needs — finding the student in front of them — and grow
            // when their module's screens land.
            new("NURSE", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("NURSE", ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, Read),
            new("LIBRARIAN", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("STOREKEEPER", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("CAFETERIA_OPERATOR", ScreenCatalog.Modules.Cafeteria, AnyScreen, null),
            new("CAFETERIA_OPERATOR", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, Read),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Sections, ScreenCatalog.Sections.Sections_, Read),

            // Module 23's screens landed, so this role stops being a name with a student lookup
            // attached. It runs the transport operation end to end — the fleet's documents, the crew,
            // the routes, who rides, and the morning itself.
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Fleet, null),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Staff, null),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Routes, null),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, null),
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Safety, null),

            // Everything on the subscription desk except the arrears suspension: stopping a child's
            // ride over money is the Principal's call (BR-TRN-008), and the supervisor who would
            // otherwise be asked to do it quietly is exactly who the rule exists to protect.
            new("TRANSPORT_SUPERVISOR", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions,
                new[] { ActionVerb.View, ActionVerb.Create, ActionVerb.Edit, ActionVerb.Deactivate }),

            // The two decisions the rule reserves for the Principal, and the oversight of them.
            new("PRINCIPAL", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Subscriptions, new[] { ActionVerb.Approve }),
            new("PRINCIPAL", ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Safety, new[] { ActionVerb.Approve }),

            // Module 25. BR-DCP-008 makes behaviour data restricted, and BR-DCP-002 makes recording
            // it wide: those two pull opposite ways, and the split below is where they meet. Any
            // teacher may record what they saw. Almost nobody may decide what follows from it.
            //
            // There is no discipline-officer role among doc 06 §4.3's twenty-one, so the officer's
            // work lands on the Vice Principal, which is where a school of this size actually puts
            // it. A school that appoints a dedicated officer makes the role in the role designer and
            // copies these grants — that is the screen's purpose.
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, new[] { ActionVerb.View, ActionVerb.Configure }),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, null),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, null),
            new("VICE_PRINCIPAL", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Actions, null),

            // Publishing the code to families is the Principal's, and so is every decision the
            // module reserves — both arrive through the blanket Oversight grant above, which is why
            // neither is repeated here. Deciding a case needs Approve, and Approve is what Oversight
            // is.

            // Runs the cases in their stage, cannot rewrite the code they are decided against.
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, Read),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, null),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, new[] { ActionVerb.View, ActionVerb.Edit }),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Actions, null),
            new("STAGE_SUPERVISOR", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Analytics, Read),

            // BR-DCP-002: records what they witnessed, and reads the code they are recording against.
            // Deliberately no Cases grant — a teacher who reported an incident is the last person who
            // should be able to steer the case that follows it (BR-DCP-010's protection cuts both ways).
            new("TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, Read),
            new("TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, null),

            // The homeroom teacher additionally sees where their own class stands — BR-DCP-008 names
            // homeroom as a reader of their own students, and the scope grant is what narrows "own".
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, null),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, Read),
            new("HOMEROOM_TEACHER", ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Analytics, Read),

            // E-Learning (doc/Modules/37 §6). The module shipped its screens with no row here at
            // all, which made it invisible rather than merely ungranted: the sidebar and the
            // launcher both hide a module the user can open no screen of (BR-SEC-010), so a
            // teacher was not refused the lesson planner — they were never shown that one exists.
            //
            // §6 gives the planner and the resource library to "Teacher, HoD" and the homework desk
            // to "Teacher"; BR-LRN-002 then extends a head of department's reach across their
            // department's offerings for content *and* homework, which is why the desk is granted to
            // both and not only to the teacher the table names. Reach is the engine's business
            // either way — these open the screen, and BR-LRN-002 decides which (offering, section)
            // pairs it will list.
            //
            // DEVIATION, inherited from ScreenCatalog rather than introduced here: §6's table names
            // no verb for publishing a lesson or issuing homework, and the catalogue maps both onto
            // Approve. So the null verb list is deliberate — granting only the four verbs §6 spells
            // out would leave a teacher able to write a plan and never publish it, with no other
            // role named to publish it for them.
            new("TEACHER", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, null),
            new("TEACHER", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, null),
            new("TEACHER", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, null),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Planner, null),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Resources, null),
            new("HEAD_OF_DEPARTMENT", ScreenCatalog.Modules.Learning, ScreenCatalog.Learning.Homework, null),

            // The portal. A student is not shown the family's money — that is the parent's screen.
            new("PARENT", ScreenCatalog.Modules.Portal, AnyScreen, null),
            new("STUDENT", ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Home, null),
            new("STUDENT", ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Announcements, null),
            new("STUDENT", ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Child, null),
            // doc/Modules/37 §6 gives "my work" to the student in the POR space. The parent reaches
            // it through the wildcard above; the student is enumerated screen by screen, so a screen
            // added to the portal after this list was written reaches the parent and silently misses
            // the student — which is what happened to this one. It is the same page for both
            // audiences (§8.10): a student account's family is itself.
            new("STUDENT", ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Work, null),
            // §5 gives the student "read content" beside "submit homework". The
            // parent reads the same page (§8.10's own audience note) and reaches
            // it through the wildcard above.
            new("STUDENT", ScreenCatalog.Modules.Portal, ScreenCatalog.Portal.Lessons, null),
        };

        // ------------------------------------------------------------------ seeding

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            var catalogued = await CatalogueAsync(cancellationToken);
            await GrantDefaultsAsync(catalogued, cancellationToken);
        }

        /// <summary>Inserts any catalogue triple <c>sec.Permission</c> does not hold yet, and returns the whole set keyed for lookup.</summary>
        private async Task<Dictionary<(string Module, string Screen, ActionVerb Action), Permission>> CatalogueAsync(CancellationToken cancellationToken)
        {
            var moduleCodes = ScreenCatalog.Screens.Select(s => s.ModuleCode).Distinct().ToList();
            var existing = await _db.Permissions
                .Where(p => moduleCodes.Contains(p.ModuleCode))
                .ToListAsync(cancellationToken);

            var byKey = existing.ToDictionary(
                p => (p.ModuleCode, p.ScreenCode, p.Action),
                p => p,
                KeyComparer);

            foreach (var (module, screen, action) in ScreenCatalog.Permissions())
            {
                if (byKey.ContainsKey((module, screen, action)))
                {
                    continue;
                }

                var permission = new Permission { ModuleCode = module, ScreenCode = screen, Action = action };
                _db.Permissions.Add(permission);
                byKey[(module, screen, action)] = permission;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return byKey;
        }

        private async Task GrantDefaultsAsync(
            Dictionary<(string Module, string Screen, ActionVerb Action), Permission> catalogued,
            CancellationToken cancellationToken)
        {
            var roles = await _db.Roles.ToDictionaryAsync(r => r.Code, r => r.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);

            // "Already provisioned" has to mean "holds a school permission", not "holds any
            // permission at all". The hosted subsystems catalogue their own under their own module
            // codes, and counting those as curation left the system administrator with the ERP's
            // grants and none of this system's - locked out of the product by its own seeder.
            var schoolPermissionIds = catalogued.Values.Select(p => p.Id).ToHashSet();
            var rolesWithGrants = new HashSet<int>(await _db.RolePermissions
                .Where(rp => schoolPermissionIds.Contains(rp.PermissionId))
                .Select(rp => rp.RoleId)
                .Distinct()
                .ToListAsync(cancellationToken));

            foreach (var roleCode in Matrix.Select(g => g.RoleCode).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!roles.TryGetValue(roleCode, out var roleId))
                {
                    // The template is absent — a school removed it, which is theirs to do.
                    continue;
                }

                // The system administrator is topped up on every run, unlike a staff role. It is
                // the role that grants the others, so a permission it cannot reach is a permission
                // nobody in the school can ever be given: a screen shipped after first provisioning
                // would be invisible to the entire product, permanently and silently. A staff role
                // keeps its curation, because revoking from a cashier is a decision and this is
                // not.
                //
                // The two portal roles are topped up for the same reason, and it is not a widening.
                // A staff role is a decision - doc 06 §7 keeps "who exists" apart from "what they
                // may do" precisely so a school can curate one. A portal role is not: it follows
                // from the account type (RoleTemplates.ForPortalAccount), PortalAreaFilter already
                // confines the account to the portal (BR-SEC-010), and exactly one seeded role opens
                // it. So the same permanent-invisibility trap applies with nobody able to notice it,
                // and it has already been sprung: POR|Work was added to this matrix for the student
                // after these databases were provisioned, and reached the student on none of them -
                // "my work" was catalogued, granted to nobody, and therefore hidden by the portal's
                // own bar. A top-up only ever adds, so a school that granted something extra keeps it.
                var alwaysTopUp = string.Equals(roleCode, SystemAdministrator, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(roleCode, RoleTemplates.Parent, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(roleCode, RoleTemplates.Student, StringComparison.OrdinalIgnoreCase);
                if (rolesWithGrants.Contains(roleId) && !alwaysTopUp)
                {
                    continue;
                }

                var held = alwaysTopUp
                    ? new HashSet<int>(await _db.RolePermissions.Where(rp => rp.RoleId == roleId).Select(rp => rp.PermissionId).ToListAsync(cancellationToken))
                    : new HashSet<int>();

                foreach (var key in Expand(roleCode))
                {
                    if (catalogued.TryGetValue(key, out var permission) && !held.Contains(permission.Id))
                    {
                        _db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permission.Id });
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Turns one role's matrix rows into concrete triples. A wildcard verb list
        /// is intersected with what each screen actually defines, so
        /// "<c>Approve everywhere</c>" grants approval only where approving is a
        /// thing — never a permission for a verb the screen has no action behind.
        /// </summary>
        private static IEnumerable<(string Module, string Screen, ActionVerb Action)> Expand(string roleCode)
        {
            var seen = new HashSet<(string, string, ActionVerb)>(KeyComparer);

            foreach (var grant in Matrix.Where(g => string.Equals(g.RoleCode, roleCode, StringComparison.OrdinalIgnoreCase)))
            {
                foreach (var screen in ScreenCatalog.Screens)
                {
                    if (grant.ModuleCode == AnyModule)
                    {
                        // A staff wildcard never reaches the portal: it is a separate audience with
                        // its own data scoping, and widening into it by accident is exactly the kind
                        // of thing a wildcard is good at.
                        if (screen.ModuleCode == ScreenCatalog.Modules.Portal)
                        {
                            continue;
                        }
                    }
                    else if (!string.Equals(screen.ModuleCode, grant.ModuleCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (grant.ScreenCode != AnyScreen && !string.Equals(screen.ScreenCode, grant.ScreenCode, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var verbs = grant.Verbs == null ? screen.Verbs : screen.Verbs.Where(v => grant.Verbs.Contains(v));
                    foreach (var verb in verbs)
                    {
                        var key = (screen.ModuleCode, screen.ScreenCode, verb);
                        if (seen.Add(key))
                        {
                            yield return key;
                        }
                    }
                }
            }
        }

        private static readonly IEqualityComparer<(string Module, string Screen, ActionVerb Action)> KeyComparer = new TripleComparer();

        private sealed class TripleComparer : IEqualityComparer<(string Module, string Screen, ActionVerb Action)>
        {
            public bool Equals((string Module, string Screen, ActionVerb Action) a, (string Module, string Screen, ActionVerb Action) b)
                => string.Equals(a.Module, b.Module, StringComparison.OrdinalIgnoreCase)
                   && string.Equals(a.Screen, b.Screen, StringComparison.OrdinalIgnoreCase)
                   && a.Action == b.Action;

            public int GetHashCode((string Module, string Screen, ActionVerb Action) key)
                => HashCode.Combine(
                    key.Module.ToUpperInvariant(),
                    key.Screen.ToUpperInvariant(),
                    key.Action);
        }
    }
}
