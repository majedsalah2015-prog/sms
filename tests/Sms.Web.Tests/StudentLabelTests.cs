using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Common;
using Sms.Domain.Students;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The two enums a student list prints, held to the bilingual rule.
    /// <para>
    /// Status and sex are the places a student screen most easily leaks an English enum name into
    /// an Arabic page — <c>Views/Students/Index.cshtml</c> prints both through
    /// <see cref="Labels"/>, and a value added to either enum without an Arabic word for it would
    /// otherwise reach a school before anyone noticed. A red build here instead.
    /// </para>
    /// </summary>
    public class StudentLabelTests
    {
        public static IEnumerable<object[]> StudentStatuses() =>
            Enum.GetValues(typeof(StudentStatus)).Cast<StudentStatus>().Select(v => new object[] { v });

        [Theory]
        [MemberData(nameof(StudentStatuses))]
        public void Every_student_status_has_a_real_Arabic_label(StudentStatus status)
        {
            var arabic = Labels.StudentStatus(status, arabic: true);

            Assert.NotEqual(status.ToString(), arabic);
            Assert.Contains(arabic, c => c >= '؀' && c <= 'ۿ');
            Assert.NotEqual(Labels.StudentStatus(status, arabic: false), arabic);
        }

        [Theory]
        [InlineData(Gender.Male)]
        [InlineData(Gender.Female)]
        public void A_childs_sex_is_labelled_in_Arabic_and_is_not_the_grades_admission_policy(Gender gender)
        {
            var arabic = Labels.Gender(gender, arabic: true);

            Assert.NotEqual(gender.ToString(), arabic);
            Assert.Contains(arabic, c => c >= '؀' && c <= 'ۿ');

            // "بنين"/"بنات" describe a grade's intake, not a person. Printing either against a child
            // is the wrong noun, and the overload exists precisely so a screen cannot pick it.
            Assert.NotEqual(Labels.Gender(Sms.Domain.Grades.GenderPolicy.Boys, true), arabic);
            Assert.NotEqual(Labels.Gender(Sms.Domain.Grades.GenderPolicy.Girls, true), arabic);
        }
    }
}
