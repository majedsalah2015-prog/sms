namespace Sms.Domain.Common
{
    /// <summary>
    /// Bilingual name pair, mandatory on named master data (ADR-5).
    /// Mapped as an EF owned type (docs/Database/01 §5).
    /// </summary>
    public class LocalizedName
    {
        public LocalizedName()
        {
        }

        public LocalizedName(string nameAr, string nameEn)
        {
            NameAr = nameAr;
            NameEn = nameEn;
        }

        public string NameAr { get; set; } = string.Empty;

        public string NameEn { get; set; } = string.Empty;
    }
}
