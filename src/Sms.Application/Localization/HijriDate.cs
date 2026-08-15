namespace Sms.Application.Localization
{
    /// <summary>Y/M/D triple in the Hijri calendar — never stored (doc 02 §6: Gregorian is the stored form), only rendered.</summary>
    public readonly struct HijriDate
    {
        public HijriDate(int year, int month, int day)
        {
            Year = year;
            Month = month;
            Day = day;
        }

        public int Year { get; }

        public int Month { get; }

        public int Day { get; }
    }
}
