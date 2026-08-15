using System;
using System.Globalization;

namespace Sms.Application.Localization
{
    /// <summary>
    /// doc 02 §6: "Hijri conversion is a domain service (Umm al-Qura), never
    /// duplicated per screen." One shared, testable conversion point instead
    /// of every module doing its own date math — that part is delivered.
    ///
    /// KNOWN GAP, flagged rather than silently substituted: the doc names
    /// <c>System.Globalization.UmmAlQuraCalendar</c> specifically (the
    /// astronomically-calculated calendar KSA uses officially), but that
    /// type is absent from this net5.0 ref pack (compiles against
    /// <see cref="HijriCalendar"/>, resolves fine; UmmAlQuraCalendar does
    /// not — confirmed by build error, not assumption). Shipped instead
    /// with the BCL's tabular <see cref="HijriCalendar"/> (arithmetic
    /// 30-year-cycle Islamic calendar), which can disagree with the
    /// official Umm al-Qura date by ±1 day near a month boundary. Same
    /// decision category as O6/O7/O9 — revisit before any KSA-01 screen
    /// that prints an official Hijri date (report cards, certificates,
    /// ZATCA-adjacent documents) ships; do not silently "fix" this by
    /// swapping calendars without re-confirming the target framework can
    /// actually resolve UmmAlQuraCalendar (may require a later TFM, or a
    /// vetted third-party package — neither confirmed here).
    /// </summary>
    public static class HijriCalendarConverter
    {
        private static readonly HijriCalendar Calendar = new();

        public static HijriDate ToHijri(DateTime gregorianDate)
        {
            var date = gregorianDate.Date;
            return new HijriDate(Calendar.GetYear(date), Calendar.GetMonth(date), Calendar.GetDayOfMonth(date));
        }

        public static DateTime ToGregorian(HijriDate hijri)
            => Calendar.ToDateTime(hijri.Year, hijri.Month, hijri.Day, 0, 0, 0, 0);
    }
}
