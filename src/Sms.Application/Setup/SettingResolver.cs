using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Setup;

namespace Sms.Application.Setup
{
    /// <summary>
    /// Pure BR-SET-005 / BR-GLB-011 resolution: for a key, the row pinned to
    /// the requested academic year wins; otherwise the school-wide default
    /// (AcademicYearId = null); otherwise nothing. Callers displaying a
    /// historical transaction pass that transaction's academic year, so
    /// "the setting in force at their date" is what resolves.
    /// </summary>
    public static class SettingResolver
    {
        public static SchoolSetting? Resolve(IEnumerable<SchoolSetting> rowsForKey, int? academicYearId)
        {
            var rows = rowsForKey.ToList();
            if (academicYearId is int year)
            {
                var pinned = rows.FirstOrDefault(r => r.AcademicYearId == year);
                if (pinned != null)
                {
                    return pinned;
                }
            }

            return rows.FirstOrDefault(r => r.AcademicYearId == null);
        }
    }
}
