using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Lookups;

namespace Sms.Application.Students
{
    /// <summary>
    /// Which column of somebody else's Access register holds which field of ours (doc/Modules/10 §8,
    /// import path; owner request 2026-08-22, guardians added 2026-08-24).
    /// <para>
    /// A guess and nothing more. The operator sees every choice in a picker and the consequences in
    /// a preview before a row is written, so being wrong here is cheap and being silent would not
    /// be: a mapping screen that opens with eighteen empty dropdowns over a register with sixty-seven
    /// columns is a screen nobody finishes.
    /// </para>
    /// <para>
    /// Two rules carry most of the weight, and both were paid for by a real register:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Order. A guardian's column names say whose field it is — <c>owner_idno</c>, <c>mother_job</c> —
    /// while the child's own says only <c>idno</c>. Guess the guardians first, or the student's
    /// identity guess takes the father's column in every register that names the child's plainly.
    /// </item>
    /// <item>
    /// Exclusivity. A column already claimed is not offered again, so <c>name2</c> cannot be both the
    /// father's name and the family name, and a bare "family" guess cannot land on <c>family_no</c>
    /// after <c>name4</c> was there to be had.
    /// </item>
    /// </list>
    /// </summary>
    public static class RegisterMappingGuesser
    {
        /// <summary>
        /// One register's columns read as our fields. Every member is null when nothing in the file
        /// looked like it — an unmapped field is a field the operator picks, not a field we invent.
        /// </summary>
        public sealed record RegisterMapping
        {
            public string? FirstName { get; init; }

            public string? FatherName { get; init; }

            public string? GrandfatherName { get; init; }

            public string? FamilyName { get; init; }

            public string? FullName { get; init; }

            public string? DateOfBirth { get; init; }

            public string? Gender { get; init; }

            public string? IdNumber { get; init; }

            /// <summary>مكان الميلاد.</summary>
            public string? PlaceOfBirth { get; init; }

            /// <summary>عدد الأخوة — siblings, not household size.</summary>
            public string? SiblingCount { get; init; }

            /// <summary>ترتيب الطالب بين الأخوة.</summary>
            public string? BirthOrder { get; init; }

            /// <summary>The student's own line, which in an Arabic register is usually spelled جوال.</summary>
            public string? Mobile { get; init; }

            public string? FatherFullName { get; init; }

            public string? FatherIdNumber { get; init; }

            public string? FatherOccupation { get; init; }

            public string? FatherMobile { get; init; }

            public string? FatherEducation { get; init; }

            public string? MotherFullName { get; init; }

            public string? MotherIdNumber { get; init; }

            public string? MotherOccupation { get; init; }

            public string? MotherMobile { get; init; }

            public string? MotherEducation { get; init; }

            /// <summary>A table in the same file translating occupation codes into words.</summary>
            public string? OccupationCodeTable { get; init; }

            public string? EducationCodeTable { get; init; }
        }

        /// <summary>
        /// Guesses only what <paramref name="current"/> leaves unanswered, and never re-uses a column
        /// the operator has already assigned by hand. Pass the mapping back in after an edit and the
        /// choices already made are respected.
        /// </summary>
        public static RegisterMapping Guess(
            IReadOnlyList<string> columns, IReadOnlyList<string> tables, RegisterMapping? current = null)
        {
            var m = current ?? new RegisterMapping();
            var taken = new HashSet<string>(
                new[]
                {
                    m.FirstName, m.FatherName, m.GrandfatherName, m.FamilyName, m.FullName,
                    m.DateOfBirth, m.Gender, m.IdNumber,
                    m.PlaceOfBirth, m.SiblingCount, m.BirthOrder, m.Mobile,
                    m.FatherFullName, m.FatherIdNumber, m.FatherOccupation, m.FatherMobile, m.FatherEducation,
                    m.MotherFullName, m.MotherIdNumber, m.MotherOccupation, m.MotherMobile, m.MotherEducation,
                }.Where(c => c != null).Select(c => c!),
                StringComparer.OrdinalIgnoreCase);

            string? Find(string? already, params string[] needles)
            {
                if (already != null)
                {
                    return already;
                }

                foreach (var needle in needles)
                {
                    var key = Key(needle);
                    var hit = columns.FirstOrDefault(c => !taken.Contains(c) && Key(c).Contains(key, StringComparison.Ordinal));
                    if (hit != null)
                    {
                        taken.Add(hit);
                        return hit;
                    }
                }

                return null;
            }

            // The student's name columns first: they are what everything else in the file is named
            // relative to, and "name2" before "father" because a register that numbers its names also
            // tends to carry father_state.
            var firstName = Find(m.FirstName, "الاسمالاول", "الأول", "name1", "firstname", "fname", "first");
            var fatherName = Find(m.FatherName, "اسمالاب", "الأب", "الاب", "name2", "fathername");
            var grandfatherName = Find(m.GrandfatherName, "اسمالجد", "الجد", "name3", "grandfather", "grand");
            var familyName = Find(m.FamilyName, "العائلة", "العايلة", "name4", "familyname", "lastname", "family");
            var fullName = Find(m.FullName, "الاسمالرباعي", "الاسمكامل", "fullname", "الاسم");
            // Before the date of birth, not after: the date's last-resort needle is a bare "birth",
            // and in a register that spells the place birth_place and nothing else does, that needle
            // would take the place column and leave the date unmapped.
            var placeOfBirth = Find(m.PlaceOfBirth, "مكان الميلاد", "محل الميلاد", "birthplace", "placeofbirth", "pob");
            var dateOfBirth = Find(m.DateOfBirth, "تاريخالميلاد", "الميلاد", "birthdate", "dateofbirth", "birth", "dob");
            var gender = Find(m.Gender, "الجنس", "النوع", "gender", "sex");

            // owner_* is what an Arabic school register calls ولي الأمر, and in all but a handful of
            // rows that is the father. Checked alongside father_*, never instead of it.
            var fatherIdNumber = Find(m.FatherIdNumber, "رقمهويةالاب", "هويةالاب", "هويةولي", "fatheridno", "fatherid", "owneridno", "ownerid");
            var fatherOccupation = Find(m.FatherOccupation, "مهنةالاب", "عملالاب", "وظيفةالاب", "fatheroccupation", "fatherjob", "owneroccupation", "ownerjob");
            var fatherMobile = Find(m.FatherMobile, "جوالالاب", "هاتفالاب", "موبايلالاب", "جوالولي", "fathermobile", "fatherphone", "ownermobile", "ownerphone");
            var fatherEducation = Find(m.FatherEducation, "مؤهلالاب", "المؤهلالعلميللاب", "تعليمالاب", "fathereducation", "fatherqualification", "ownerqualification", "ownereducation");
            var fatherFullName = Find(m.FatherFullName, "اسمولياالمر", "اسموليالامر", "الاسمالرباعيللاب", "fatherfullname", "guardianname", "ownername");

            var motherIdNumber = Find(m.MotherIdNumber, "رقمهويةالام", "هويةالام", "motheridno", "motherid");
            var motherOccupation = Find(m.MotherOccupation, "مهنةالام", "عملالام", "وظيفةالام", "motheroccupation", "motherjob");
            var motherMobile = Find(m.MotherMobile, "جوالالام", "هاتفالام", "موبايلالام", "mothermobile", "motherphone");
            var motherEducation = Find(m.MotherEducation, "مؤهلالام", "المؤهلالعلميللام", "تعليمالام", "mothereducation", "motherqualification");
            var motherFullName = Find(m.MotherFullName, "اسمالام", "اسمالوالده", "mothername");

            // Last, and only from what nobody else claimed: a bare "الهوية" in a register that also
            // names the parents' is the child's.
            var idNumber = Find(m.IdNumber, "هويةالطالب", "رقمالطالب", "الهوية", "رقمالهوية", "identity", "idno", "nationalid");

            // Same reason, and the mobile most of all: "جوال" alone would take the father's column in
            // a register that writes his as جوال_الاب, and "mobile" would take the mother's.
            //
            // jwal before std_mobile deliberately. Both are the student's line by name, but the
            // register this was built against fills jwal in all 1,398 rows and std_mobile in none —
            // std_mobile is the column such files gained in a later version and never populated. The
            // picker and the preview both show the choice, so a register where it is the other way
            // round costs one click.
            var mobile = Find(m.Mobile, "جوال الطالب", "موبايل الطالب", "studentmobile", "jwal", "جوال", "الجوال", "stdmobile", "mobile");
            var siblingCount = Find(m.SiblingCount, "عدد الأخوة", "عدد الإخوة", "الأخوة", "brotherno", "brothers", "siblings", "siblingcount");
            var birthOrder = Find(m.BirthOrder, "ترتيب الطالب", "الترتيب", "ترتيب", "arrangeno", "arrange", "birthorder");

            string? FindTable(string? already, params string[] needles)
            {
                if (already != null)
                {
                    return already;
                }

                foreach (var needle in needles)
                {
                    var key = Key(needle);
                    var hit = tables.FirstOrDefault(t => Key(t).Contains(key, StringComparison.Ordinal));
                    if (hit != null) { return hit; }
                }

                return null;
            }

            return m with
            {
                FirstName = firstName,
                FatherName = fatherName,
                GrandfatherName = grandfatherName,
                FamilyName = familyName,
                FullName = fullName,
                DateOfBirth = dateOfBirth,
                Gender = gender,
                IdNumber = idNumber,
                PlaceOfBirth = placeOfBirth,
                SiblingCount = siblingCount,
                BirthOrder = birthOrder,
                Mobile = mobile,
                FatherFullName = fatherFullName,
                FatherIdNumber = fatherIdNumber,
                FatherOccupation = fatherOccupation,
                FatherMobile = fatherMobile,
                FatherEducation = fatherEducation,
                MotherFullName = motherFullName,
                MotherIdNumber = motherIdNumber,
                MotherOccupation = motherOccupation,
                MotherMobile = motherMobile,
                MotherEducation = motherEducation,

                // Chosen from the file's table list rather than its columns, so these can never
                // collide with a column mapping.
                OccupationCodeTable = FindTable(m.OccupationCodeTable, "jobcode", "occupationcode", "كودالمهنة", "المهن"),
                EducationCodeTable = FindTable(m.EducationCodeTable, "qualificationcode", "educationcode", "كودالمؤهل", "المؤهلات"),
            };
        }

        /// <summary>
        /// One spelling for a column name. Hamza shapes, the taa marbuta, spaces and the underscores
        /// separate "هوية الأب" in one register from "هويه_الاب" in the next; none of those
        /// differences means anything about which column it is.
        /// </summary>
        private static string Key(string text) => LookupTextMatcher.Normalize(text).Replace(" ", string.Empty);
    }
}
