using Sms.Application.Reports;
using Sms.Domain.Reports;
using Sms.TestSupport;
using Xunit;

namespace Sms.Application.Tests.Reports
{
    public class ExportPermissionGateTests
    {
        [Fact]
        [BusinessRule("BR-RPT-003")]
        public void Normal_reports_can_always_export()
        {
            Assert.True(ExportPermissionGate.CanExport(ReportSensitivity.Normal, hasExportPermission: false));
        }

        [Theory]
        [InlineData(ReportSensitivity.PersonalData)]
        [InlineData(ReportSensitivity.Restricted)]
        [BusinessRule("BR-RPT-003")]
        public void Sensitive_reports_need_the_export_permission(ReportSensitivity sensitivity)
        {
            Assert.False(ExportPermissionGate.CanExport(sensitivity, hasExportPermission: false));
            Assert.True(ExportPermissionGate.CanExport(sensitivity, hasExportPermission: true));
        }
    }
}
