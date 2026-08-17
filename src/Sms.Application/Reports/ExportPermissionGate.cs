using Sms.Domain.Reports;

namespace Sms.Application.Reports
{
    /// <summary>Pure BR-RPT-003: personal-data/restricted reports require the Export permission for file output; view-only rendering never needs it.</summary>
    public static class ExportPermissionGate
    {
        public static bool CanExport(ReportSensitivity sensitivity, bool hasExportPermission)
            => sensitivity == ReportSensitivity.Normal || hasExportPermission;
    }
}
