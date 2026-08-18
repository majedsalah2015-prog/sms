namespace Sms.Web.Models
{
    /// <summary>
    /// Sys-admin landing tiles (doc/Modules/01 §11 names setup completeness
    /// and recent config changes; the widget registry of E-702 is the real
    /// dashboard engine — this is the shell's plain landing until Module 31
    /// screens land).
    /// </summary>
    public sealed class HomeDashboardViewModel
    {
        public string? SchoolNameEn { get; set; }

        public string? SchoolNameAr { get; set; }

        public string? SchoolStatus { get; set; }

        public string? ActiveYearLabelEn { get; set; }

        public string? ActiveYearLabelAr { get; set; }

        public string? ActiveYearStatus { get; set; }

        public int Students { get; set; }

        public int Employees { get; set; }

        public int Sections { get; set; }

        public int Parents { get; set; }

        public int AuditEntries { get; set; }

        public bool HasSchool => SchoolNameEn != null;
    }
}
