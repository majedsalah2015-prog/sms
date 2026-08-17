using System;

namespace Sms.Domain.Reports
{
    /// <summary>BR-RPT-001: a definition supports a set of output formats; a single execution picks one.</summary>
    [Flags]
    public enum OutputFormat : short
    {
        Html = 1,
        Pdf = 2,
        Xlsx = 4,
        Csv = 8,
    }
}
