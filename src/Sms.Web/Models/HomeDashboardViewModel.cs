using System.Collections.Generic;
using Sms.Web.Navigation;

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
        /// <summary>
        /// The departments this user may enter — the page's main content. Empty for an account with
        /// no screen permissions at all, which the view answers with a plain explanation rather than
        /// a blank page.
        /// </summary>
        public IReadOnlyList<WorkspaceBuilder.VisibleWorkspace> Workspaces { get; set; } =
            new List<WorkspaceBuilder.VisibleWorkspace>();

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

        public bool HasSchool => SchoolNameEn != null;
    }
}
