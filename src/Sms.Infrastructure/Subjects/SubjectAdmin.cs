using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.Application.Subjects;
using Sms.Domain.Common;
using Sms.Domain.Subjects;
using Sms.Infrastructure.Persistence;

namespace Sms.Infrastructure.Subjects
{
    /// <summary>Standalone admin operations — save themselves, no larger transaction to ride.</summary>
    public class SubjectAdmin : ISubjectAdmin
    {
        private readonly AppDbContext _db;
        private readonly IUsageInspector<CurriculumOffering> _usage;

        public SubjectAdmin(AppDbContext db, IUsageInspector<CurriculumOffering> usage)
        {
            _db = db;
            _usage = usage;
        }

        public async Task<Department> DefineDepartmentAsync(
            string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default)
        {
            var department = new Department { Name = new LocalizedName(nameAr, nameEn), HeadTeacherUserId = headTeacherUserId };
            _db.Departments.Add(department);

            await _db.SaveChangesAsync(cancellationToken);
            return department;
        }

        public async Task<Subject> DefineSubjectAsync(
            string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default)
        {
            var codeTaken = await _db.Subjects.AnyAsync(s => s.Code == code, cancellationToken);
            if (codeTaken)
            {
                throw new DuplicateSubjectCodeException(code);
            }

            var subject = new Subject
            {
                Code = code,
                Name = new LocalizedName(nameAr, nameEn),
                Category = category,
                DepartmentId = departmentId,
            };
            _db.Subjects.Add(subject);

            await _db.SaveChangesAsync(cancellationToken);
            return subject;
        }

        public async Task<Subject> UpdateSubjectAsync(
            int subjectId, string code, string nameAr, string nameEn, string category, int? departmentId = null, CancellationToken cancellationToken = default)
        {
            var subject = await _db.Subjects.SingleAsync(s => s.Id == subjectId, cancellationToken);
            if (await _db.Subjects.AnyAsync(s => s.Code == code && s.Id != subjectId, cancellationToken))
            {
                throw new DuplicateSubjectCodeException(code);
            }

            subject.Code = code;
            subject.Name = new LocalizedName(nameAr, nameEn);
            subject.Category = category;
            subject.DepartmentId = departmentId;
            await _db.SaveChangesAsync(cancellationToken);
            return subject;
        }

        public async Task DeactivateSubjectAsync(int subjectId, CancellationToken cancellationToken = default)
        {
            var subject = await _db.Subjects.SingleAsync(s => s.Id == subjectId, cancellationToken);
            var current = await _db.CurriculumOfferings.CountAsync(o => o.SubjectId == subjectId && o.EffectiveToUtc == null, cancellationToken);
            if (current > 0)
            {
                throw new SubjectInUseException(UsageReport.From(new UsageReference("current curriculum plan(s) — end-date those offerings first (BR-SUB-004)", "خطة دراسية سارية — أنهِ تلك المقررات بتاريخ أولاً (BR-SUB-004)", current)));
            }

            subject.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<Department> UpdateDepartmentAsync(int departmentId, string nameAr, string nameEn, int? headTeacherUserId = null, CancellationToken cancellationToken = default)
        {
            var department = await _db.Departments.SingleAsync(d => d.Id == departmentId, cancellationToken);
            department.Name = new LocalizedName(nameAr, nameEn);
            department.HeadTeacherUserId = headTeacherUserId;
            await _db.SaveChangesAsync(cancellationToken);
            return department;
        }

        public async Task DeactivateDepartmentAsync(int departmentId, CancellationToken cancellationToken = default)
        {
            var department = await _db.Departments.SingleAsync(d => d.Id == departmentId, cancellationToken);
            var subjects = await _db.Subjects.CountAsync(s => s.DepartmentId == departmentId, cancellationToken);
            if (subjects > 0)
            {
                throw new SubjectInUseException(UsageReport.From(new UsageReference("active subject(s) in this department — move them first", "مادة فعّالة في هذا القسم — انقلها أولاً", subjects)));
            }

            department.IsActive = false;
            await _db.SaveChangesAsync(cancellationToken);
        }

        public async Task<CurriculumOffering> DefineOfferingAsync(
            int gradeYearProfileId, int subjectId, int weeklyPeriods, bool isAssessable, decimal gpaWeight,
            bool isElective, string? electiveGroupTag, DateTime effectiveFromUtc, CancellationToken cancellationToken = default)
        {
            if (!CurriculumPlanValidator.HasValidWeight(isAssessable, gpaWeight))
            {
                throw new InvalidOfferingWeightException();
            }

            var profile = await _db.GradeYearProfiles.SingleAsync(p => p.Id == gradeYearProfileId, cancellationToken);

            var currentExists = await _db.CurriculumOfferings.AnyAsync(
                o => o.GradeYearProfileId == gradeYearProfileId && o.SubjectId == subjectId && o.EffectiveToUtc == null, cancellationToken);
            if (currentExists)
            {
                throw new DuplicateOfferingException(gradeYearProfileId, subjectId);
            }

            var offering = new CurriculumOffering
            {
                AcademicYearId = profile.AcademicYearId,
                GradeYearProfileId = gradeYearProfileId,
                SubjectId = subjectId,
                WeeklyPeriods = weeklyPeriods,
                IsAssessable = isAssessable,
                GpaWeight = gpaWeight,
                IsElective = isElective,
                ElectiveGroupTag = electiveGroupTag,
                EffectiveFromUtc = effectiveFromUtc,
            };
            _db.CurriculumOfferings.Add(offering);

            await _db.SaveChangesAsync(cancellationToken);
            return offering;
        }

        public async Task EndDateOfferingAsync(int offeringId, DateTime effectiveToUtc, CancellationToken cancellationToken = default)
        {
            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == offeringId, cancellationToken);
            offering.EffectiveToUtc = effectiveToUtc;

            await _db.SaveChangesAsync(cancellationToken);
        }

        /// <summary>
        /// The plan line is corrected where it stands — no end-date-and-redefine dance for a typo.
        /// <para>
        /// BR-SUB-005's slot ceiling is deliberately not enforced here, exactly as it is not in
        /// <see cref="DefineOfferingAsync"/>: doc/Modules/07 §9 makes the plan total "blocking at
        /// confirmation, warning while drafting", and the plan editor is the drafting surface. The
        /// screen shows the overrun; year activation is what refuses it.
        /// </para>
        /// <para>
        /// <b>Deviation, stated:</b> §9's "periods/week ≥ 1" is enforced on this path and not on
        /// <see cref="DefineOfferingAsync"/>, which has never enforced it. Adding the guard there
        /// would change the contract of a method the demo seeder already calls, so it is left as a
        /// pre-existing gap rather than fixed in passing.
        /// </para>
        /// </summary>
        public async Task<CurriculumOffering> UpdateOfferingAsync(
            int offeringId, int weeklyPeriods, bool isAssessable, decimal gpaWeight,
            bool isElective, string? electiveGroupTag, CancellationToken cancellationToken = default)
        {
            if (weeklyPeriods < 1)
            {
                throw new InvalidOfferingPeriodsException(weeklyPeriods);
            }

            if (!CurriculumPlanValidator.HasValidWeight(isAssessable, gpaWeight))
            {
                throw new InvalidOfferingWeightException();
            }

            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == offeringId, cancellationToken);

            // BR-SUB-004. An ended line is what a taught term points at; editing it would restate
            // marks and timetables already issued rather than change anything going forward.
            if (offering.EffectiveToUtc != null)
            {
                throw new EndedOfferingNotEditableException(offeringId);
            }

            offering.WeeklyPeriods = weeklyPeriods;
            offering.IsAssessable = isAssessable;
            offering.GpaWeight = gpaWeight;
            offering.IsElective = isElective;
            offering.ElectiveGroupTag = electiveGroupTag;

            await _db.SaveChangesAsync(cancellationToken);
            return offering;
        }

        /// <summary>
        /// Removal, for the only case BR-SUB-004 leaves room for: a line nothing has been recorded
        /// against — added to the wrong grade, or to the wrong year, and noticed before it was used.
        /// <para>
        /// The guard is <see cref="CurriculumOfferingUsageInspector"/> rather than a second set of
        /// counts written here, because the plan screen asks that same inspector whether to draw the
        /// button at all. One implementation means the button and the refusal cannot drift into
        /// disagreeing — which is the failure mode where a screen offers an action that always
        /// fails, or hides one that would have worked.
        /// </para>
        /// <para>
        /// The <see cref="DbUpdateException"/> catch is not belt-and-braces. Every non-ownership
        /// cascade in this model is downgraded to <c>Restrict</c>, so a reference the inspector does
        /// not yet know about surfaces as a foreign-key violation; caught here it becomes the same
        /// refusal instead of a 500.
        /// </para>
        /// </summary>
        public async Task RemoveOfferingAsync(int offeringId, CancellationToken cancellationToken = default)
        {
            var offering = await _db.CurriculumOfferings.SingleAsync(o => o.Id == offeringId, cancellationToken);

            var usage = await _usage.InspectAsync(offeringId, cancellationToken);
            if (usage.IsInUse)
            {
                throw new RecordInUseException(usage);
            }

            _db.CurriculumOfferings.Remove(offering);
            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                throw new RecordInUseException(UsageReport.From(
                    new UsageReference("other record(s)", "سجل آخر", 1)));
            }
        }

        public async Task<TeacherSubjectQualification> DefineQualificationAsync(
            int teacherUserId, int subjectId, int? stageId, QualificationSource source, CancellationToken cancellationToken = default)
        {
            var qualification = new TeacherSubjectQualification
            {
                TeacherUserId = teacherUserId,
                SubjectId = subjectId,
                StageId = stageId,
                Source = source,
            };
            _db.TeacherSubjectQualifications.Add(qualification);

            await _db.SaveChangesAsync(cancellationToken);
            return qualification;
        }
    }
}
