using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sms.Application.Audit;
using Sms.Application.Common.Interfaces;
using Sms.Application.Security;
using Sms.Application.Students;
using Sms.Domain.Common;
using Sms.Domain.Security;
using Sms.Domain.Students;
using Sms.Infrastructure.Persistence;
using Sms.Web.Api.Models;
using Sms.Web.Security;

namespace Sms.Web.Api.Controllers
{
    /// <summary>
    /// doc/Modules/10 §8 for the app — the directory, the student file, and the
    /// writes that file supports, over the same <see cref="IStudentAdmin"/> the
    /// browser screens use.
    /// <para>
    /// The social profile (BR-GLB-072's restricted category — religion, family
    /// circumstances, ration card) is <b>not</b> exposed here. It has its own
    /// screen permission precisely so it can be withheld from roles that hold
    /// the rest of the file, and putting it behind <c>Students/File/View</c> on
    /// a second transport would hand it to everyone the browser withholds it
    /// from. If the app needs it, it needs its own endpoint under
    /// <c>Students/SocialProfile</c>.
    /// </para>
    /// </summary>
    [Route(V1 + "/students")]
    public sealed class StudentsApiController : ApiControllerBase
    {
        private readonly IStudentAdmin _students;
        private readonly AppDbContext _db;
        private readonly IAuditContext _audit;
        private readonly IClock _clock;
        private readonly IWorkingYearContext _workingYear;

        public StudentsApiController(
            IStudentAdmin students,
            AppDbContext db,
            IAuditContext audit,
            IClock clock,
            IWorkingYearContext workingYear)
        {
            _students = students;
            _db = db;
            _audit = audit;
            _clock = clock;
            _workingYear = workingYear;
        }

        /// <summary>
        /// The directory. <paramref name="q"/> runs through
        /// <see cref="StudentSearch"/> — every name part in both languages plus
        /// the student number, each word narrowing further — so the app's search
        /// box behaves exactly like the registrar's.
        /// </summary>
        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.View)]
        public async Task<ActionResult<ApiPage<ApiStudentRow>>> Directory(
            string? q = null, string? status = null, int? gradeLevelId = null, int? sectionId = null,
            int? page = null, int? pageSize = null)
        {
            var (p, size) = ApiPaging.Clamp(page, pageSize);

            // IgnoreQueryFilters + an explicit school predicate, as the browser directory
            // does: a withdrawn student is IsActive = false and still belongs in a search,
            // and the tenant guard is restated by hand rather than lost with the filter.
            var query = _db.Students.IgnoreQueryFilters().AsNoTracking()
                .Where(s => s.SchoolId == _db.CurrentSchoolId);

            query = StudentSearch.Matching(query, q);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StudentStatus>(status, ignoreCase: true, out var wanted))
            {
                query = query.Where(s => s.Status == wanted);
            }

            if (gradeLevelId.HasValue || sectionId.HasValue)
            {
                var live = _db.Enrollments.AsNoTracking().Where(e => e.ExitDate == null && e.AcademicYearId == _workingYear.AcademicYearId);

                if (gradeLevelId.HasValue)
                {
                    var profileIds = await _db.GradeYearProfiles.AsNoTracking()
                        .Where(gp => gp.GradeLevelId == gradeLevelId.Value)
                        .Select(gp => gp.Id)
                        .ToListAsync(Ct);
                    live = live.Where(e => profileIds.Contains(e.GradeYearProfileId));
                }

                if (sectionId.HasValue)
                {
                    var seated = await _db.SectionMemberships.AsNoTracking()
                        .Where(m => m.SectionId == sectionId.Value && m.EffectiveToUtc == null)
                        .Select(m => m.EnrollmentId)
                        .ToListAsync(Ct);
                    live = live.Where(e => seated.Contains(e.Id));
                }

                var studentIds = await live.Select(e => e.StudentId).Distinct().ToListAsync(Ct);
                query = query.Where(s => studentIds.Contains(s.Id));
            }

            var total = await query.CountAsync(Ct);
            var students = await query
                .OrderBy(s => s.StudentNo)
                .Skip(ApiPaging.Skip(p, size))
                .Take(size)
                .ToListAsync(Ct);

            var placements = await PlacementsAsync(students.Select(s => s.Id).ToList());

            var rows = students
                .Select(s =>
                {
                    placements.TryGetValue(s.Id, out var placement);
                    return new ApiStudentRow
                    {
                        StudentId = s.Id,
                        StudentNo = s.StudentNo,
                        NameAr = Join(s.FirstNameAr, s.FatherNameAr, s.GrandfatherNameAr, s.FamilyNameAr),
                        NameEn = Join(s.FirstNameEn, s.FatherNameEn, s.GrandfatherNameEn, s.FamilyNameEn),
                        Status = s.Status.ToString(),
                        GradeCode = placement?.GradeCode,
                        GradeName = placement?.GradeName,
                        SectionName = placement?.SectionName,
                        Mobile = s.Mobile,
                    };
                })
                .ToList();

            return Page<ApiStudentRow>(rows, p, size, total);
        }

        /// <summary>The student file: identity, this year's placement, guardians and emergency contacts.</summary>
        [HttpGet("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.View)]
        public async Task<ActionResult<ApiStudentFile>> File(int id)
        {
            var student = await _db.Students.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.SchoolId == _db.CurrentSchoolId, Ct);
            if (student == null)
            {
                return NotFoundError();
            }

            var placements = await PlacementsAsync(new[] { id });
            placements.TryGetValue(id, out var placement);

            var links = await _db.StudentGuardianLinks.AsNoTracking()
                .Where(l => l.StudentId == id && l.EffectiveToUtc == null)
                .ToListAsync(Ct);
            var parentIds = links.Select(l => l.ParentId).ToList();
            var parents = await _db.Parents.IgnoreQueryFilters().AsNoTracking()
                .Where(p => parentIds.Contains(p.Id) && p.SchoolId == _db.CurrentSchoolId)
                .Select(p => new { p.Id, p.NameAr, p.NameEn, p.PrimaryMobile })
                .ToListAsync(Ct);

            var lookupIds = links.Select(l => l.RelationshipLookupId)
                .Append(student.NationalityLookupId)
                .Distinct()
                .ToList();
            var lookups = await LookupNamesAsync(lookupIds);

            var contacts = await _db.EmergencyContacts.AsNoTracking()
                .Where(c => c.StudentId == id)
                .ToListAsync(Ct);

            return new ApiStudentFile
            {
                StudentId = student.Id,
                StudentNo = student.StudentNo,
                FirstNameAr = student.FirstNameAr,
                FatherNameAr = student.FatherNameAr,
                GrandfatherNameAr = student.GrandfatherNameAr,
                FamilyNameAr = student.FamilyNameAr,
                FirstNameEn = student.FirstNameEn,
                FatherNameEn = student.FatherNameEn,
                GrandfatherNameEn = student.GrandfatherNameEn,
                FamilyNameEn = student.FamilyNameEn,
                Gender = student.Gender.ToString(),
                DateOfBirth = student.DateOfBirth,
                NationalityLookupId = student.NationalityLookupId,
                NationalityName = lookups.TryGetValue(student.NationalityLookupId, out var nationality) ? nationality : null,
                PrimaryIdTypeLookupId = student.PrimaryIdTypeLookupId,
                PrimaryIdNo = student.PrimaryIdNo,
                PrimaryIdExpiry = student.PrimaryIdExpiry,
                Status = student.Status.ToString(),
                Mobile = student.Mobile,
                HasPhoto = student.PhotoAttachmentId != null,
                Placement = placement,
                Guardians = links
                    .Select(l =>
                    {
                        var parent = parents.FirstOrDefault(p => p.Id == l.ParentId);
                        return new ApiStudentGuardian
                        {
                            LinkId = l.Id,
                            ParentId = l.ParentId,
                            NameAr = parent?.NameAr ?? string.Empty,
                            NameEn = parent?.NameEn ?? string.Empty,
                            Mobile = parent?.PrimaryMobile,
                            RelationshipLookupId = l.RelationshipLookupId,
                            Relationship = lookups.TryGetValue(l.RelationshipLookupId, out var name) ? name : null,
                            IsPrimaryContact = l.IsPrimaryContact,
                            IsFinanciallyResponsible = l.IsFinanciallyResponsible,
                            IsPickupAuthorized = l.IsPickupAuthorized,
                            IsPortalVisible = l.IsPortalVisible,
                            EffectiveFromUtc = l.EffectiveFromUtc,
                        };
                    })
                    .ToList(),
                EmergencyContacts = contacts
                    .Select(c => new ApiEmergencyContact
                    {
                        Id = c.Id,
                        NameAr = c.NameAr,
                        NameEn = c.NameEn,
                        Phone = c.Phone,
                        IsPickupAuthorized = c.IsPickupAuthorized,
                        RelationshipLookupId = c.RelationshipLookupId,
                    })
                    .ToList(),
            };
        }

        /// <summary>
        /// Registers a student directly — the non-admissions path. The student
        /// number is issued by the numbering series on this call's own commit
        /// (BR-NUM-003), which is why none is accepted in the request.
        /// </summary>
        [HttpPost("")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Directory, ActionVerb.Create)]
        public async Task<ActionResult<ApiStudentFile>> Register([FromBody] ApiRegisterStudentRequest request)
        {
            if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
            {
                return Refuse(422, "invalid_gender", "Gender must be Male or Female.", "الجنس يجب أن يكون ذكر أو أنثى.");
            }

            var student = await _students.RegisterStudentAsync(
                request.FirstNameAr.Trim(), request.FatherNameAr.Trim(), request.GrandfatherNameAr.Trim(), request.FamilyNameAr.Trim(),
                request.FirstNameEn.Trim(), request.FatherNameEn.Trim(), request.GrandfatherNameEn.Trim(), request.FamilyNameEn.Trim(),
                gender, request.DateOfBirth, request.NationalityLookupId,
                request.PrimaryIdTypeLookupId, request.PrimaryIdNo, request.PrimaryIdExpiry, Ct);

            return await File(student.Id);
        }

        /// <summary>
        /// Corrects identity fields. BR-STU-002 makes these T1-audited with a
        /// mandatory reason: the reason is put on the ambient audit context
        /// before the save, exactly as the browser screen does, and a blank one
        /// is refused by the capture pipeline rather than by this method.
        /// </summary>
        [HttpPut("{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<ActionResult<ApiStudentFile>> Update(int id, [FromBody] ApiUpdateStudentRequest request)
        {
            if (!Enum.TryParse<Gender>(request.Gender, ignoreCase: true, out var gender))
            {
                return Refuse(422, "invalid_gender", "Gender must be Male or Female.", "الجنس يجب أن يكون ذكر أو أنثى.");
            }

            _audit.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();

            await _students.UpdateStudentAsync(
                id,
                request.FirstNameAr.Trim(), request.FatherNameAr.Trim(), request.GrandfatherNameAr.Trim(), request.FamilyNameAr.Trim(),
                request.FirstNameEn.Trim(), request.FatherNameEn.Trim(), request.GrandfatherNameEn.Trim(), request.FamilyNameEn.Trim(),
                gender, request.DateOfBirth, request.NationalityLookupId,
                request.PrimaryIdTypeLookupId, request.PrimaryIdNo, request.PrimaryIdExpiry, Ct);

            return await File(id);
        }

        /// <summary>BR-WF-001: the engine decides which transitions are legal, not the caller.</summary>
        [HttpPost("{id:int}/status")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Approve)]
        public async Task<IActionResult> ChangeStatus(int id, [FromBody] ApiChangeStudentStatusRequest request)
        {
            if (!Enum.TryParse<StudentStatus>(request.Status, ignoreCase: true, out var status))
            {
                return Refuse(422, "invalid_student_status",
                    "That is not a student status.", "هذه ليست حالة طالب.");
            }

            _audit.Reason = string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim();
            await _students.ChangeStatusAsync(id, status, Ct);
            return NoContent();
        }

        /// <summary>Links a guardian to the student.</summary>
        [HttpPost("{id:int}/guardians")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Guardians, ActionVerb.Edit)]
        public async Task<IActionResult> LinkGuardian(int id, [FromBody] ApiLinkGuardianRequest request)
        {
            var link = await _students.LinkGuardianAsync(
                id, request.ParentId, request.RelationshipLookupId,
                request.IsPrimaryContact, request.IsFinanciallyResponsible, request.IsPickupAuthorized,
                request.IsPortalVisible, request.EffectiveFromUtc ?? _clock.UtcNow,
                request.GuardianshipDocAttachmentId, Ct);

            return Ok(new { linkId = link.Id });
        }

        /// <summary>
        /// Ends a guardian link. BR-GLB-004: refused when it would leave the
        /// student with nobody financially responsible.
        /// </summary>
        [HttpPost("guardians/{linkId:int}/unlink")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Guardians, ActionVerb.Deactivate)]
        public async Task<IActionResult> UnlinkGuardian(int linkId, [FromBody] ApiUnlinkGuardianRequest? request = null)
        {
            await _students.UnlinkGuardianAsync(linkId, request?.EffectiveToUtc ?? _clock.UtcNow, Ct);
            return NoContent();
        }

        /// <summary>Adds somebody to call who is not a guardian.</summary>
        [HttpPost("{id:int}/emergency-contacts")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.File, ActionVerb.Edit)]
        public async Task<IActionResult> AddEmergencyContact(int id, [FromBody] ApiEmergencyContactRequest request)
        {
            var contact = await _students.AddEmergencyContactAsync(
                id, request.NameAr.Trim(), request.NameEn.Trim(), request.Phone.Trim(),
                request.IsPickupAuthorized, request.RelationshipLookupId, Ct);

            return Ok(new { emergencyContactId = contact.Id });
        }

        /// <summary>BR-GLB-024: a second live enrollment in the same year is refused.</summary>
        [HttpPost("{id:int}/enrollments")]
        [RequirePermission(ScreenCatalog.Modules.Students, ScreenCatalog.Students.Enrollment, ActionVerb.Create)]
        public async Task<IActionResult> Enroll(int id, [FromBody] ApiEnrollRequest request)
        {
            var source = Enum.TryParse<EnrollmentSourceType>(request.SourceType, ignoreCase: true, out var parsed)
                ? parsed
                : EnrollmentSourceType.Admission;

            var enrollment = await _students.EnrollAsync(
                id, request.GradeYearProfileId, request.EnrollmentDate ?? _clock.UtcNow.Date, source, Ct);

            return Ok(new { enrollmentId = enrollment.Id });
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>
        /// This year's grade and section for a set of students, in four queries
        /// rather than four per student — a directory page of 200 that walked
        /// each row would issue 800.
        /// </summary>
        private async Task<Dictionary<int, ApiStudentPlacement>> PlacementsAsync(IReadOnlyList<int> studentIds)
        {
            var result = new Dictionary<int, ApiStudentPlacement>();
            if (studentIds.Count == 0)
            {
                return result;
            }

            var enrollments = await _db.Enrollments.AsNoTracking()
                .Where(e => studentIds.Contains(e.StudentId) && e.ExitDate == null && e.AcademicYearId == _workingYear.AcademicYearId)
                .ToListAsync(Ct);
            if (enrollments.Count == 0)
            {
                return result;
            }

            var profileIds = enrollments.Select(e => e.GradeYearProfileId).Distinct().ToList();
            var profiles = await _db.GradeYearProfiles.IgnoreQueryFilters().AsNoTracking()
                .Where(gp => profileIds.Contains(gp.Id))
                .Select(gp => new { gp.Id, gp.GradeLevelId })
                .ToListAsync(Ct);

            // IgnoreQueryFilters on the grade lookup: a retired grade level still names the
            // year a child is sitting in, and reading it through the soft-active filter is
            // how a directory page dies the day a school retires one.
            var gradeIds = profiles.Select(gp => gp.GradeLevelId).Distinct().ToList();
            var grades = await _db.GradeLevels.IgnoreQueryFilters().AsNoTracking()
                .Where(g => gradeIds.Contains(g.Id) && g.SchoolId == _db.CurrentSchoolId)
                .Select(g => new { g.Id, g.Code, g.Name.NameAr, g.Name.NameEn })
                .ToListAsync(Ct);

            var enrollmentIds = enrollments.Select(e => e.Id).ToList();
            var memberships = await _db.SectionMemberships.AsNoTracking()
                .Where(m => enrollmentIds.Contains(m.EnrollmentId) && m.EffectiveToUtc == null)
                .Select(m => new { m.EnrollmentId, m.SectionId })
                .ToListAsync(Ct);

            var sectionIds = memberships.Select(m => m.SectionId).Distinct().ToList();
            var sections = await _db.Sections.IgnoreQueryFilters().AsNoTracking()
                .Where(s => sectionIds.Contains(s.Id) && s.SchoolId == _db.CurrentSchoolId)
                .Select(s => new { s.Id, s.NameAr, s.NameEn })
                .ToListAsync(Ct);

            foreach (var enrollment in enrollments)
            {
                var profile = profiles.FirstOrDefault(gp => gp.Id == enrollment.GradeYearProfileId);
                var grade = profile == null ? null : grades.FirstOrDefault(g => g.Id == profile.GradeLevelId);
                var membership = memberships.FirstOrDefault(m => m.EnrollmentId == enrollment.Id);
                var section = membership == null ? null : sections.FirstOrDefault(s => s.Id == membership.SectionId);

                result[enrollment.StudentId] = new ApiStudentPlacement
                {
                    EnrollmentId = enrollment.Id,
                    AcademicYearId = enrollment.AcademicYearId,
                    GradeLevelId = profile?.GradeLevelId,
                    GradeCode = grade?.Code,
                    GradeName = grade == null ? null : T(grade.NameEn, grade.NameAr),
                    SectionId = membership?.SectionId,
                    SectionName = section == null ? null : T(section.NameEn, section.NameAr),
                    EnrollmentDate = enrollment.EnrollmentDate,
                };
            }

            return result;
        }

        /// <summary>
        /// Lookup id → display name in the caller's language. Read through
        /// <c>IgnoreQueryFilters</c>: a deactivated nationality still names the
        /// students already recorded under it (the soft-active lookup trap).
        /// </summary>
        private async Task<Dictionary<int, string>> LookupNamesAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0)
            {
                return new Dictionary<int, string>();
            }

            var rows = await _db.LookupValues.IgnoreQueryFilters().AsNoTracking()
                .Where(v => ids.Contains(v.Id) && v.SchoolId == _db.CurrentSchoolId)
                .Select(v => new { v.Id, v.Name.NameAr, v.Name.NameEn })
                .ToListAsync(Ct);

            return rows.ToDictionary(r => r.Id, r => T(r.NameEn, r.NameAr));
        }

        private static string Join(params string[] parts)
            => string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }
}
