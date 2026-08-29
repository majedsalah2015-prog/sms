using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Employees
{
    /// <summary>
    /// ppl.Qualification (doc/Modules/12 §7, BR-EMP-004): degrees,
    /// certifications, licenses. IsTeachingRelevant flags entries that
    /// should feed the BR-SUB-006 qualification matrix — the actual feed
    /// (auto-populating Sms.Domain.Subjects.TeacherSubjectQualification)
    /// is deferred, same as this slice's other cross-module wiring.
    /// TrainingRecord (PD hours) is deferred entirely — no ministry
    /// PD-hour reporting consumer exists yet.
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Qualification : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int EmployeeId { get; set; }

        /// <summary>
        /// The written title, kept for what the catalogues below cannot name: BR-EMP-004 covers
        /// certifications and licences as well as degrees, and a first-aid certificate has no
        /// university and no classification. Either the title or
        /// <see cref="EducationLookupId"/> identifies the entry — <c>EmployeeAdmin</c> refuses one
        /// with neither, and the free-text pair is what the Excel import still writes.
        /// </summary>
        public string TitleAr { get; set; } = string.Empty;

        public string TitleEn { get; set; } = string.Empty;

        /// <summary>
        /// The awarding body as free text. Superseded by <see cref="UniversityLookupId"/> for
        /// degrees, and still the only way to name the training centre that issued a licence.
        /// </summary>
        public string? InstitutionName { get; set; }

        /// <summary>
        /// تاريخ التخرج / تاريخ المنح — one date, because for a degree they are the same date and
        /// a second column meaning the same thing is a second column to disagree.
        /// </summary>
        public DateTime DateAwarded { get; set; }

        /// <summary>
        /// المؤهل — core.LookupValue, category "EducationLevel": the catalogue the parent's own
        /// qualification is chosen from, so "بكالوريوس" means one thing across the product
        /// (owner request, 2026-08-27).
        /// <para>
        /// Lookups rather than enums for all four catalogues here, for the reason the category was
        /// created with: what counts as a qualification, and which universities and classifications
        /// a school recognises, is a local decision that must be changeable without a code change.
        /// </para>
        /// </summary>
        public int? EducationLookupId { get; set; }

        /// <summary>الجامعة — core.LookupValue, category "University".</summary>
        public int? UniversityLookupId { get; set; }

        /// <summary>التخصص — core.LookupValue, category "Specialization".</summary>
        public int? SpecializationLookupId { get; set; }

        /// <summary>التقدير — core.LookupValue, category "AcademicGrade" (ممتاز / جيد جداً / …).</summary>
        public int? AcademicGradeLookupId { get; set; }

        /// <summary>
        /// المعدل. Stored as the certificate states it and not converted: one school's register
        /// holds 3.62 out of 4 beside another's 87.40 out of 100, and a system that silently
        /// normalised them would make both unreadable. decimal(5,2) covers either.
        /// </summary>
        public decimal? Gpa { get; set; }

        public bool IsTeachingRelevant { get; set; }

        public int? DocumentAttachmentId { get; set; }
    }
}
