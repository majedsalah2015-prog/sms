using System;

namespace Sms.Domain.Attachments
{
    /// <summary>BR-ATT-002 default allowed formats + module-justified extras (DOCX/XLSX).</summary>
    [Flags]
    public enum DocumentFormat : short
    {
        Pdf = 1,
        Jpg = 2,
        Png = 4,
        Docx = 8,
        Xlsx = 16,
    }
}
