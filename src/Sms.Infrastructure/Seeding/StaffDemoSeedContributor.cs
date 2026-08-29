using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Employees;
using Sms.Application.Seeding;
using Sms.Application.Teachers;
using Sms.Domain.Common;
using Sms.Domain.Employees;
using Sms.Domain.Teachers;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Seeding
{
    /// <summary>
    /// A staffed school, so Module 12's screens can be exercised against something
    /// (doc/Modules/12 §8.1 directory, §8.2 employee file, §8.3 org chart, §8.6 contract
    /// manager — and Module 13's teacher directory / matrix / load board, which read the
    /// same people).
    /// <para>
    /// <see cref="DemoSeedContributor"/> registers exactly one employee, because its job is
    /// to prove the stack composes end to end. That is not enough to look at: an org chart
    /// with one person in it does not show whether the tree indents, a contract manager with
    /// one contract cannot show that its four buckets sort correctly, and a directory with
    /// one row never reveals that a filter is wrong. This contributor exists for the
    /// looking — seven files complete enough that every implemented tab has content and
    /// every bucket is occupied.
    /// </para>
    /// <para>
    /// <b>Every date here is an offset from the day the seed runs</b>, never a literal. A
    /// fixture with hard-coded dates classifies correctly on the afternoon it is written and
    /// wrongly forever after: the contract that was "expiring in 52 days" becomes an expired
    /// one, and the console that was being demonstrated shows an empty bucket instead.
    /// </para>
    /// <para>
    /// What it deliberately does <b>not</b> seed: staff attendance, leave balances, training
    /// records and employee documents. doc/Modules/12 §8.2 lists those as tabs on the
    /// employee file, and this build has neither their engines nor their screens — the file
    /// collects them under "More…" as pending. Seeding rows for them is not possible and
    /// pretending otherwise would hide a real gap. Photographs are also left empty: they go
    /// through the attachment scan gate on upload, and the upload path is itself worth
    /// testing by hand.
    /// </para>
    /// </summary>
    public class StaffDemoSeedContributor : ISeedContributor
    {
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly IEmployeeAdmin _employees;
        private readonly ITeacherAdmin _teachers;

        public StaffDemoSeedContributor(AppDbContext db, IAuditContext audit, IClock clock, IEmployeeAdmin employees, ITeacherAdmin teachers)
        {
            _db = db;
            _audit = audit;
            _clock = clock;
            _employees = employees;
            _teachers = teachers;
        }

        public string Name => "Staff demo (org tree + seven employee files, doc/Modules/12 §8)";

        /// <summary>
        /// After <see cref="DemoSeedContributor"/> (50), because the teaching assignments need
        /// the sections and curriculum offerings it creates, and before the portal accounts (55).
        /// </summary>
        public int Order => 52;

        // ============================================================ the org tree (BR-EMP-002)
        //
        // Three levels deep on purpose: a two-level tree renders identically whether the chart
        // indents by depth or just lists children, so it proves nothing.

        private static readonly (string Key, string NameAr, string NameEn, string? ParentKey)[] Units =
        {
            ("admin", "الإدارة المدرسية", "School Administration", null),
            ("academic", "الشؤون الأكاديمية", "Academic Affairs", "admin"),
            ("science", "قسم العلوم والرياضيات", "Science & Mathematics Department", "academic"),
            ("finance", "الشؤون المالية والإدارية", "Finance & Administration", "admin"),
            ("support", "شؤون الطلبة والخدمات المساندة", "Student Affairs & Support Services", "admin"),
        };

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            // Nothing to hang a staff register off before the school itself exists, and the job
            // titles are named by code below — an unseeded lookup table is a "come back later",
            // not a failure.
            if (!await _db.Schools.AnyAsync(cancellationToken)) { return; }

            var jobTitles = await LookupAsync("JobTitle", cancellationToken);
            var nationalities = await LookupAsync("Nationality", cancellationToken);
            var idTypes = await LookupAsync("IdType", cancellationToken);

            // The qualification catalogues (owner request 2026-08-27). Not guarded against below:
            // an unseeded one leaves the classification off the credential, which is exactly what a
            // school that has not authored its lists yet should see.
            var educationLevels = await LookupAsync("EducationLevel", cancellationToken);
            var academicGrades = await LookupAsync("AcademicGrade", cancellationToken);
            if (jobTitles.Count == 0 || nationalities.Count == 0) { return; }

            _audit.Reason = "تهيئة بيانات الكادر التجريبية";

            var units = await SeedOrgUnitsAsync(cancellationToken);
            var today = _clock.UtcNow.Date;
            var people = Roster(today);

            // ---- pass 1: the people themselves, with everything that needs nobody else -------
            //
            // Keyed on the national ID / passport number rather than on the employee number:
            // BR-EMP-001 makes EMP permanent and issued, so it cannot be known in advance, and
            // re-running the seeder must not hand the same human being a second one.
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var person in people)
            {
                var existing = await _db.Employees.AsNoTracking()
                    .SingleOrDefaultAsync(e => e.PrimaryIdNo == person.IdNo, cancellationToken);
                if (existing != null)
                {
                    ids[person.IdNo] = existing.Id;
                    continue;
                }

                var employee = await _employees.RegisterEmployeeAsync(
                    person.FirstAr, person.FatherAr, person.GrandAr, person.FamilyAr,
                    person.FirstEn, person.FatherEn, person.GrandEn, person.FamilyEn,
                    person.Gender, person.BirthDate,
                    Resolve(nationalities, person.NationalityCode),
                    userAccountId: null,
                    primaryIdTypeLookupId: idTypes.Count == 0 ? null : Resolve(idTypes, person.IdTypeCode),
                    primaryIdNo: person.IdNo,
                    primaryIdExpiry: person.IdExpiry,
                    whatsAppNumber: person.WhatsApp,
                    cancellationToken: cancellationToken);
                ids[person.IdNo] = employee.Id;

                // The personal block — marital status, the address, and every destination the pay
                // can be sent to. T1 with a required reason on the money fields, so the ambient
                // reason set above is doing real work here, not decoration.
                await _employees.UpdatePersonalDetailsAsync(
                    employee.Id, person.Marital, person.BankName, person.BankAccountNo,
                    person.Address, person.OriginTown,
                    person.SpouseIdNo == null || idTypes.Count == 0 ? null : Resolve(idTypes, "NationalId"),
                    person.SpouseIdNo, person.PalPay, person.JawwalPay, cancellationToken);

                foreach (var q in person.Qualifications)
                {
                    // The written title stays even where the catalogue names the qualification: the
                    // demo is showing a register that was typed before the lists existed and then
                    // classified, which is the state every real import lands in.
                    await _employees.AddQualificationAsync(
                        employee.Id, q.TitleAr, q.TitleEn, q.Awarded, q.TeachingRelevant, q.Institution,
                        educationLookupId: q.EducationCode == null || educationLevels.Count == 0 ? null : Resolve(educationLevels, q.EducationCode),
                        academicGradeLookupId: q.GradeCode == null || academicGrades.Count == 0 ? null : Resolve(academicGrades, q.GradeCode),
                        gpa: q.Gpa,
                        cancellationToken: cancellationToken);
                }

                foreach (var deal in person.Contracts)
                {
                    var contract = await _employees.DefineContractAsync(
                        employee.Id, deal.Type, deal.Start, deal.End, deal.Basic, deal.Allowances, cancellationToken);

                    // Draft -> Active -> Terminated: the transitions are a chain, so a contract that
                    // ends up Terminated is walked through Active rather than dropped into it.
                    if (deal.Status != ContractStatus.Draft)
                    {
                        await _employees.ChangeContractStatusAsync(contract.Id, ContractStatus.Active, cancellationToken);
                    }

                    if (deal.Status == ContractStatus.Terminated)
                    {
                        await _employees.ChangeContractStatusAsync(contract.Id, ContractStatus.Terminated, cancellationToken);
                    }
                }

                // Each person is a committed unit of its own; the tracker has no reason to carry the
                // previous six into the next one's saves.
                _db.ChangeTracker.Clear();
            }

            // ---- pass 2: postings, which name managers by person and so need every id ---------
            foreach (var person in people)
            {
                var employeeId = ids[person.IdNo];
                if (await _db.EmployeeAssignments.AnyAsync(a => a.EmployeeId == employeeId, cancellationToken))
                {
                    continue;
                }

                foreach (var posting in person.Postings)
                {
                    await _employees.AssignPositionAsync(
                        employeeId,
                        units[posting.UnitKey],
                        Resolve(jobTitles, posting.JobTitleCode),
                        posting.ManagerIdNo == null ? null : ids[posting.ManagerIdNo],
                        posting.From,
                        cancellationToken);
                }

                _db.ChangeTracker.Clear();
            }

            // ---- pass 3: teaching, then status -----------------------------------------------
            //
            // Teaching first because BR-TCH-001 wants a live contract, and status last because
            // that is the order the events happen in: hired, placed, taught, and only then
            // suspended or offboarded.
            await SeedTeachingAsync(people, ids, cancellationToken);

            foreach (var person in people.Where(p => p.FinalStatus != EmployeeStatus.Active))
            {
                var employeeId = ids[person.IdNo];
                var current = await _db.Employees.AsNoTracking().SingleAsync(e => e.Id == employeeId, cancellationToken);
                if (current.Status == person.FinalStatus) { continue; }

                _audit.Reason = person.StatusReason;
                await _employees.ChangeStatusAsync(employeeId, person.FinalStatus, cancellationToken);
                _db.ChangeTracker.Clear();
            }
        }

        private async Task<Dictionary<string, int>> SeedOrgUnitsAsync(CancellationToken cancellationToken)
        {
            var ids = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var unit in Units)
            {
                var existing = await _db.OrgUnits.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.NameEn == unit.NameEn, cancellationToken);
                ids[unit.Key] = existing?.Id
                    ?? (await _employees.DefineOrgUnitAsync(
                        unit.NameAr, unit.NameEn,
                        unit.ParentKey == null ? null : ids[unit.ParentKey],
                        cancellationToken)).Id;
            }

            return ids;
        }

        /// <summary>
        /// Designates the two teachers and puts them in front of classes, so Module 13's matrix and
        /// load board have something to draw and the employee file's Teaching tab is not an empty
        /// panel behind a badge.
        /// <para>
        /// Every step is conditional on the curriculum actually being there. A tenant seeded before
        /// anyone defined an offering still gets its teacher profiles — which is the part Module 12
        /// owns — and simply has nothing to assign them to yet.
        /// </para>
        /// </summary>
        private async Task SeedTeachingAsync(IReadOnlyList<Person> people, IReadOnlyDictionary<string, int> ids, CancellationToken cancellationToken)
        {
            var offerings = await _db.CurriculumOfferings.AsNoTracking().OrderBy(o => o.Id).Take(2).ToListAsync(cancellationToken);
            var teachers = people.Where(p => p.MaxWeeklyPeriods != null).ToList();

            for (var i = 0; i < teachers.Count; i++)
            {
                var employeeId = ids[teachers[i].IdNo];
                var profile = await _db.TeacherProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.EmployeeId == employeeId, cancellationToken)
                    ?? await _teachers.DesignateTeacherAsync(employeeId, teachers[i].MaxWeeklyPeriods!.Value, cancellationToken);
                _db.ChangeTracker.Clear();

                if (i >= offerings.Count) { continue; }
                var offering = offerings[i];

                var sections = await _db.Sections.AsNoTracking()
                    .Where(s => s.GradeYearProfileId == offering.GradeYearProfileId)
                    .OrderBy(s => s.Id).Take(2).ToListAsync(cancellationToken);

                foreach (var section in sections)
                {
                    // BR-TCH-005 allows one primary per offering×section. Re-running must find the
                    // seat taken and leave it alone rather than throw at the person re-seeding.
                    var taken = await _db.TeacherAssignments.AnyAsync(
                        a => a.CurriculumOfferingId == offering.Id && a.SectionId == section.Id
                            && a.Role == TeacherRole.Primary && a.EffectiveToUtc == null,
                        cancellationToken);
                    if (taken) { continue; }

                    await _teachers.AssignAsync(
                        profile.Id, offering.Id, section.Id, TeacherRole.Primary,
                        offering.EffectiveFromUtc, cancellationToken: cancellationToken);
                }

                _db.ChangeTracker.Clear();
            }
        }

        private async Task<Dictionary<string, int>> LookupAsync(string categoryCode, CancellationToken cancellationToken)
        {
            var category = await _db.LookupCategories.AsNoTracking().SingleOrDefaultAsync(c => c.Code == categoryCode, cancellationToken);
            if (category == null) { return new Dictionary<string, int>(StringComparer.Ordinal); }

            return await _db.LookupValues.AsNoTracking()
                .Where(v => v.LookupCategoryId == category.Id)
                .ToDictionaryAsync(v => v.Code, v => v.Id, StringComparer.Ordinal, cancellationToken);
        }

        /// <summary>
        /// The lookup this fixture asked for, or the first one the tenant does have. A country pack
        /// is free to carry a different set of nationalities than the one written here, and a demo
        /// that refuses to seed because a code is spelled differently helps nobody.
        /// </summary>
        private static int Resolve(IReadOnlyDictionary<string, int> values, string code)
            => values.TryGetValue(code, out var id) ? id : values.Values.First();

        // ============================================================ the roster
        //
        // Held as data rather than three hundred lines of statements: the whole point of the
        // fixture is the shape of the set — which buckets fill, which tabs have content, who
        // reports to whom — and that is legible in a table and invisible in a script.

        /// <summary>
        /// A degree or a licence. <paramref name="EducationCode"/>/<paramref name="GradeCode"/> name
        /// rows in the "EducationLevel" and "AcademicGrade" catalogues; both are left null on the
        /// entries that are licences rather than degrees, which is the case the qualifications tab
        /// has to keep rendering (BR-EMP-004 covers all three kinds). No university or
        /// specialization code, because those two catalogues ship empty for a school to author.
        /// </summary>
        private sealed record Credential(
            string TitleAr, string TitleEn, string Institution, DateTime Awarded, bool TeachingRelevant,
            string? EducationCode = null, string? GradeCode = null, decimal? Gpa = null);

        private sealed record Posting(string JobTitleCode, string UnitKey, string? ManagerIdNo, DateTime From);

        private sealed record Deal(ContractType Type, DateTime Start, DateTime End, decimal Basic, decimal? Allowances, ContractStatus Status);

        private sealed record Person(
            string IdNo, string IdTypeCode, DateTime IdExpiry,
            string FirstAr, string FatherAr, string GrandAr, string FamilyAr,
            string FirstEn, string FatherEn, string GrandEn, string FamilyEn,
            Gender Gender, DateTime BirthDate, string NationalityCode,
            MaritalStatus Marital, string BankName, string BankAccountNo,
            Posting[] Postings, Deal[] Contracts, Credential[] Qualifications,
            EmployeeStatus FinalStatus, string? StatusReason, int? MaxWeeklyPeriods,
            // The personal block (owner request 2026-08-27), defaulted so the roster above only
            // states it where it is interesting. Left empty on most of the staff deliberately: a
            // real register is patchy, and a demo where every field is filled hides the screens
            // that have to read "—" without falling over.
            string? Address = null, string? OriginTown = null, string? WhatsApp = null,
            string? SpouseIdNo = null, string? PalPay = null, string? JawwalPay = null);

        private const string Principal = "900100011";
        private const string ViceHead = "900100022";
        private const string MathTeacher = "900100033";
        private const string ScienceTeacher = "900100044";
        private const string Accountant = "A1174503";
        private const string HrOfficer = "900100066";
        private const string Driver = "900100077";

        private static IReadOnlyList<Person> Roster(DateTime today) => new[]
        {
            // ---------------------------------------------------------------- the principal
            new Person(
                Principal, "NationalId", today.AddDays(1800),
                "محمود", "إبراهيم", "صالح", "النجار", "Mahmoud", "Ibrahim", "Saleh", "Al-Najjar",
                Gender.Male, new DateTime(1972, 4, 11), "PS",
                MaritalStatus.Married, "بنك فلسطين", "PS92PALS045130001200000112233",
                new[] { new Posting("Administrator", "admin", null, today.AddDays(-700)) },
                new[] { new Deal(ContractType.FullTime, today.AddDays(-700), today.AddDays(372), 1450m, 300m, ContractStatus.Active) },
                new[]
                {
                    new Credential("ماجستير الإدارة التربوية", "MA, Educational Administration", "الجامعة الإسلامية بغزة", today.AddDays(-3650), false, "Master", "Excellent", 3.81m),
                    new Credential("بكالوريوس اللغة العربية", "BA, Arabic Language", "جامعة الأزهر - غزة", today.AddDays(-7300), true, "Bachelor", "VeryGood", 84.60m),
                },
                EmployeeStatus.Active, null, null,
                // The one record with the personal block filled end to end — it is the file a
                // reviewer opens first, and the one that has to show every new field rendering.
                Address: "غزة - حي الرمال الجنوبي - شارع الجلاء", OriginTown: "بيت دراس",
                WhatsApp: "0599100011", SpouseIdNo: "900100099",
                PalPay: "0599100011", JawwalPay: "0567100011"),

            // ------------------------------------- the deputy, promoted out of the classroom
            new Person(
                ViceHead, "NationalId", today.AddDays(1200),
                "هدى", "يوسف", "كامل", "أبو رمضان", "Huda", "Yousef", "Kamel", "Abu Ramadan",
                Gender.Female, new DateTime(1981, 9, 2), "PS",
                MaritalStatus.Married, "البنك الإسلامي الفلسطيني", "PS41ISPB045130002200000223344",
                // Two postings, so the file's position tab has a closed row above the open one —
                // the whole reason BR-EMP-002 dates assignments instead of overwriting them.
                new[]
                {
                    new Posting("Teacher", "academic", Principal, today.AddDays(-700)),
                    new Posting("Administrator", "academic", Principal, today.AddDays(-23)),
                },
                new[] { new Deal(ContractType.FullTime, today.AddDays(-700), today.AddDays(372), 1100m, 220m, ContractStatus.Active) },
                new[]
                {
                    new Credential("ماجستير المناهج وطرق التدريس", "MA, Curricula and Teaching Methods", "الجامعة الإسلامية بغزة", today.AddDays(-2555), true, "Master", "Excellent", 3.74m),
                    new Credential("بكالوريوس الرياضيات", "BSc, Mathematics", "جامعة الأزهر - غزة", today.AddDays(-6570), true, "Bachelor", "Good", 76.20m),
                },
                EmployeeStatus.Active, null, null,
                Address: "غزة - حي النصر", OriginTown: "المجدل", WhatsApp: "0598200022",
                SpouseIdNo: "900100088", JawwalPay: "0567200022"),

            // ------------------------------------------- mathematics, moved between two units
            new Person(
                MathTeacher, "NationalId", today.AddDays(900),
                "سامي", "خالد", "مصطفى", "الشوا", "Sami", "Khaled", "Mustafa", "Al-Shawa",
                Gender.Male, new DateTime(1988, 1, 19), "PS",
                MaritalStatus.Married, "بنك القدس", "PS77QUDS045130003300000334455",
                new[]
                {
                    new Posting("Teacher", "academic", ViceHead, today.AddDays(-340)),
                    new Posting("Teacher", "science", ViceHead, today.AddDays(-23)),
                },
                new[] { new Deal(ContractType.FullTime, today.AddDays(-23), today.AddDays(341), 780m, 120m, ContractStatus.Active) },
                new[]
                {
                    new Credential("بكالوريوس الرياضيات", "BSc, Mathematics", "جامعة الأزهر - غزة", today.AddDays(-4380), true, "Bachelor", "VeryGood", 82.10m),
                    new Credential("دبلوم التأهيل التربوي", "Postgraduate Diploma in Education", "الجامعة الإسلامية بغزة", today.AddDays(-3200), true, "Diploma", "Good", 3.05m),
                },
                EmployeeStatus.Active, null, 24,
                Address: "خان يونس - حي الأمل", OriginTown: "يبنا", WhatsApp: "0597300033",
                PalPay: "0597300033"),

            // ------------------------- science, on a term contract that is about to run out ⏰
            new Person(
                ScienceTeacher, "NationalId", today.AddDays(35),
                "رانيا", "نبيل", "حسن", "المصري", "Rania", "Nabil", "Hasan", "Al-Masri",
                Gender.Female, new DateTime(1993, 6, 27), "PS",
                MaritalStatus.Single, "بنك فلسطين", "PS15PALS045130004400000445566",
                new[] { new Posting("Teacher", "science", ViceHead, today.AddDays(-23)) },
                // An expiring contract with a drafted successor behind it: the pair the renewals
                // pipeline exists to show, and the only way the manager's "successor drafted"
                // marker ever appears.
                new[]
                {
                    new Deal(ContractType.Term, today.AddDays(-23), today.AddDays(52), 690m, 90m, ContractStatus.Active),
                    new Deal(ContractType.FullTime, today.AddDays(53), today.AddDays(400), 760m, 110m, ContractStatus.Draft),
                },
                new[]
                {
                    new Credential("بكالوريوس تعليم العلوم", "BSc, Science Education", "الجامعة الإسلامية بغزة", today.AddDays(-2200), true, "Bachelor", "Excellent", 91.30m),
                    // A licence: no qualification level, no classification, no GPA — the row that
                    // proves the tab still reads when the four catalogues have nothing to say.
                    new Credential("شهادة السلامة المخبرية", "Laboratory Safety Certificate", "وزارة التربية والتعليم العالي", today.AddDays(-600), false),
                },
                // Single, so no spouse document — the pairing the personal tab must not insist on.
                EmployeeStatus.Active, null, 20,
                Address: "غزة - حي الشجاعية", OriginTown: "حمامة", WhatsApp: "0592400044"),

            // ---------------------------- the accountant: a passport holder, not a national ID
            new Person(
                Accountant, "Passport", today.AddDays(400),
                "محمد", "عصام", "فتحي", "عبد الغني", "Mohamed", "Essam", "Fathy", "Abdel-Ghani",
                Gender.Male, new DateTime(1990, 11, 5), "EG",
                MaritalStatus.Married, "بنك فلسطين", "PS63PALS045130005500000556677",
                new[] { new Posting("Accountant", "finance", Principal, today.AddDays(-236)) },
                new[] { new Deal(ContractType.FullTime, today.AddDays(-236), today.AddDays(494), 850m, 150m, ContractStatus.Active) },
                new[]
                {
                    new Credential("بكالوريوس المحاسبة", "BA, Accounting", "جامعة الأقصى", today.AddDays(-3800), false),
                    new Credential("شهادة محاسب فني معتمد", "Certified Accounting Technician", "جمعية المحاسبين والمراجعين الفلسطينية", today.AddDays(-1500), false),
                },
                EmployeeStatus.Active, null, null),

            // ------------- HR, suspended, on a contract that lapsed with a renewal still drafted
            new Person(
                HrOfficer, "NationalId", today.AddDays(1500),
                "فاطمة", "عادل", "محمود", "قشطة", "Fatima", "Adel", "Mahmoud", "Qeshta",
                Gender.Female, new DateTime(1986, 3, 14), "PS",
                MaritalStatus.Divorced, "البنك الإسلامي الفلسطيني", "PS28ISPB045130006600000667788",
                new[] { new Posting("HrOfficer", "finance", Principal, today.AddDays(-700)) },
                new[]
                {
                    new Deal(ContractType.FullTime, today.AddDays(-700), today.AddDays(-55), 800m, 130m, ContractStatus.Active),
                    new Deal(ContractType.FullTime, today.AddDays(8), today.AddDays(372), 880m, 150m, ContractStatus.Draft),
                },
                new[]
                {
                    new Credential("بكالوريوس إدارة الأعمال", "BA, Business Administration", "الجامعة الإسلامية بغزة", today.AddDays(-4700), false),
                    new Credential("دبلوم إدارة الموارد البشرية", "Diploma in Human Resource Management", "غرفة تجارة وصناعة غزة", today.AddDays(-1100), false),
                },
                EmployeeStatus.Suspended, "إجازة بدون راتب — بانتظار اعتماد تجديد العقد", null),

            // ------------------------------------------- offboarded, with the contract ended too
            new Person(
                Driver, "NationalId", today.AddDays(220),
                "يوسف", "منير", "عبد الله", "بربخ", "Yousef", "Munir", "Abdullah", "Barbakh",
                Gender.Male, new DateTime(1979, 12, 8), "PS",
                MaritalStatus.Married, "بنك القدس", "PS94QUDS045130007700000778899",
                new[] { new Posting("Driver", "support", Principal, today.AddDays(-600)) },
                new[] { new Deal(ContractType.FullTime, today.AddDays(-600), today.AddDays(-55), 520m, 60m, ContractStatus.Terminated) },
                new[]
                {
                    new Credential("الثانوية العامة - الفرع الأدبي", "General Secondary Certificate (Literary)", "وزارة التربية والتعليم العالي", today.AddDays(-9000), false),
                    new Credential("رخصة قيادة عمومي", "Public Transport Driving Licence", "وزارة النقل والمواصلات", today.AddDays(-2900), false),
                },
                EmployeeStatus.Terminated, "استقالة بإشعار مسبق — انتهت الخدمة في نهاية العقد", null),
        };
    }
}
