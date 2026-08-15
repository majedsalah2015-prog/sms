namespace Sms.Application.Numbering
{
    /// <summary>Values available to a doc 08 §2 format template at render time.</summary>
    public sealed class NumberFormatContext
    {
        public NumberFormatContext(string schoolCode, string academicYearLabel, int gregorianYear, int sequence)
        {
            SchoolCode = schoolCode;
            AcademicYearLabel = academicYearLabel;
            GregorianYear = gregorianYear;
            Sequence = sequence;
        }

        public string SchoolCode { get; }

        public string AcademicYearLabel { get; }

        public int GregorianYear { get; }

        public int Sequence { get; }
    }
}
