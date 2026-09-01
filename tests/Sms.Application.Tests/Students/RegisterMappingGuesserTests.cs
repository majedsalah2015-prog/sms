using System;
using System.Linq;
using Sms.Application.Students;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Students
{
    /// <summary>
    /// Reading somebody else's Access register (doc/Modules/10 §8, import path).
    /// <para>
    /// The columns below are not invented: they are the sixty-seven of <c>student_table</c> in the
    /// register this import was built for — 1,398 children, Office-era Arabic school software. A
    /// guesser tested only against tidy names is a guesser that works only on tidy registers, and
    /// nobody who needs this feature has one.
    /// </para>
    /// </summary>
    public class RegisterMappingGuesserTests
    {
        /// <summary>The real thing, in the order Access reports it.</summary>
        private static readonly string[] RealRegister =
        {
            "app_no", "reg_no", "class", "st_no", "idno", "birth_date", "birth_place",
            "name1", "name2", "name3", "name4", "SEX", "religion", "un_no", "insurance_no",
            "address", "area", "bus", "bus2", "phone",
            "owner_name", "owner_rel", "owner_job", "owner_address", "owner_phone", "owner_idno",
            "mother_name", "mother_qualification", "mother_job", "mother_idno", "mother_address", "mother_phone",
            "is_transport", "reg_date", "fees_type", "morning_sort", "afternoon_sort", "update_date",
            "refuge", "jwal", "TRANS_OPTION", "DURATION", "CHILD_HANDED", "del_date", "fees_no",
            "final_result", "total_grade", "average", "note", "vilage", "dain_notes", "brother_no",
            "arrange_no", "revenue_level", "long", "weight", "blood", "sship", "withdraw",
            "withdraw_date", "social_level", "social_level_t", "french", "std_mobile", "family_no",
            "father_state", "mother_state",
        };

        private static readonly string[] RealTables =
        {
            "student_table", "qualification_code", "job_code", "relation_code", "class_table", "SEX_CODE",
        };

        [Fact]
        public void The_students_own_fields_are_found_in_a_real_register()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.Equal("name1", m.FirstName);
            Assert.Equal("name2", m.FatherName);
            Assert.Equal("name3", m.GrandfatherName);
            Assert.Equal("name4", m.FamilyName);
            Assert.Equal("birth_date", m.DateOfBirth);
            Assert.Equal("SEX", m.Gender);
        }

        /// <summary>
        /// The trap this ordering exists for. <c>idno</c> is the child's and <c>owner_idno</c> is the
        /// father's; guess the child's first and it takes whichever the file lists first, which in a
        /// register that puts the guardian block above the student block is the father's.
        /// </summary>
        [Fact]
        public void The_childs_identity_number_is_not_the_fathers()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.Equal("idno", m.IdNumber);
            Assert.Equal("owner_idno", m.FatherIdNumber);
            Assert.Equal("mother_idno", m.MotherIdNumber);
            Assert.Equal(3, new[] { m.IdNumber, m.FatherIdNumber, m.MotherIdNumber }.Distinct().Count());
        }

        /// <summary>An Arabic register calls the father ولي الأمر and names his columns owner_*.</summary>
        [Fact]
        public void The_guardian_columns_of_a_real_register_are_read_as_the_fathers()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.Equal("owner_name", m.FatherFullName);
            Assert.Equal("owner_job", m.FatherOccupation);
            Assert.Equal("owner_phone", m.FatherMobile);

            // This register has no column for the father's qualification. Guessing one would attach a
            // stranger's data to him; leaving it null is the honest answer.
            Assert.Null(m.FatherEducation);
        }

        [Fact]
        public void The_mothers_columns_are_read_including_her_qualification()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.Equal("mother_name", m.MotherFullName);
            Assert.Equal("mother_job", m.MotherOccupation);
            Assert.Equal("mother_phone", m.MotherMobile);
            Assert.Equal("mother_qualification", m.MotherEducation);
        }

        /// <summary>
        /// <c>father_state</c> is a life status, not a name, and <c>family_no</c> is a household
        /// number, not a surname. Both sit in this register beside the columns that are, and a bare
        /// "father"/"family" guess reaches them first unless the numbered names are tried first.
        /// </summary>
        [Fact]
        public void A_status_column_is_not_mistaken_for_a_name()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.NotEqual("father_state", m.FatherName);
            Assert.NotEqual("mother_state", m.MotherFullName);
            Assert.NotEqual("family_no", m.FamilyName);
        }

        [Fact]
        public void The_code_tables_are_found_among_the_files_tables()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            Assert.Equal("job_code", m.OccupationCodeTable);
            Assert.Equal("qualification_code", m.EducationCodeTable);
        }

        /// <summary>No column may answer to two fields, or one cell becomes two different facts.</summary>
        [Fact]
        public void No_column_is_mapped_to_two_fields()
        {
            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables);

            var mapped = new[]
            {
                m.FirstName, m.FatherName, m.GrandfatherName, m.FamilyName, m.FullName,
                m.DateOfBirth, m.Gender, m.IdNumber,
                m.FatherFullName, m.FatherIdNumber, m.FatherOccupation, m.FatherMobile, m.FatherEducation,
                m.MotherFullName, m.MotherIdNumber, m.MotherOccupation, m.MotherMobile, m.MotherEducation,
            }.Where(c => c != null).ToList();

            Assert.Equal(mapped.Count, mapped.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        }

        /// <summary>A choice the operator already made is a fact, not a starting position.</summary>
        [Fact]
        public void An_operators_own_choice_is_kept_and_not_re_used()
        {
            var chosen = new RegisterMappingGuesser.RegisterMapping { FatherMobile = "jwal" };

            var m = RegisterMappingGuesser.Guess(RealRegister, RealTables, chosen);

            Assert.Equal("jwal", m.FatherMobile);
            Assert.NotEqual("jwal", m.MotherMobile);
        }

        /// <summary>Arabic column names, spelled the way whoever built the register happened to spell them.</summary>
        [Fact]
        public void Arabic_column_names_are_read_whichever_hamza_was_typed()
        {
            var columns = new[]
            {
                "الاسم الأول", "اسم الأب", "اسم الجد", "اسم العائلة", "تاريخ الميلاد", "الجنس", "رقم الهوية",
                "رقم هوية الاب", "مهنة الأب", "جوال الأب", "اسم الأم", "رقم هويه الام", "مهنة الام", "جوال الام", "مؤهل الأم",
            };

            var m = RegisterMappingGuesser.Guess(columns, Array.Empty<string>());

            Assert.Equal("الاسم الأول", m.FirstName);
            Assert.Equal("اسم الجد", m.GrandfatherName);
            Assert.Equal("اسم العائلة", m.FamilyName);
            Assert.Equal("رقم الهوية", m.IdNumber);
            Assert.Equal("رقم هوية الاب", m.FatherIdNumber);
            Assert.Equal("رقم هويه الام", m.MotherIdNumber);
            Assert.Equal("مؤهل الأم", m.MotherEducation);
            Assert.Equal("جوال الام", m.MotherMobile);
        }

        [Fact]
        public void A_file_with_nothing_recognisable_maps_nothing_rather_than_guessing()
        {
            var m = RegisterMappingGuesser.Guess(new[] { "f1", "f2", "f3" }, new[] { "t1" });

            Assert.Null(m.FirstName);
            Assert.Null(m.IdNumber);
            Assert.Null(m.MotherEducation);
            Assert.Null(m.OccupationCodeTable);
        }
    }
}
