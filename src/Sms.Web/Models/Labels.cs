using System.Collections.Generic;
using Sms.Domain.Attachments;
using Sms.Domain.Grades;
using Sms.Domain.Schools;

namespace Sms.Web.Models
{
    /// <summary>
    /// Shared bilingual display labels for enums that many screens print
    /// (year status in year pickers, gender policy, …) so the Arabic UI never
    /// shows raw enum names.
    /// </summary>
    public static class Labels
    {
        /// <summary>
        /// Why an upload was refused, said as a fact and with the way out rather than as a rule
        /// (BR-ATT-002/003 set the limits; the person holding the file needs to know what to do
        /// next). One wording for every upload in the product — the photograph on a registration
        /// form and the contract on a documents tab are refused in the same voice.
        /// </summary>
        public static string FileRejection(Sms.Web.Services.FileRejection r, bool arabic, DocumentFormat allowedFormats = 0, long maxBytes = 0)
        {
            var formats = FormatList(allowedFormats);
            var megabytes = maxBytes <= 0 ? 0 : (maxBytes + (1024 * 1024) - 1) / (1024 * 1024);

            return r switch
            {
                Services.FileRejection.NoFile => arabic ? "لم يتم اختيار ملف." : "No file was chosen.",
                Services.FileRejection.TooLarge => arabic
                    ? $"يجب ألا يتجاوز حجم الملف {megabytes} ميغابايت."
                    : $"The file must be {megabytes} MB or smaller.",
                Services.FileRejection.UnknownFormat or Services.FileRejection.FormatNotAllowed => arabic
                    ? $"الصيغ المقبولة هنا: {formats}."
                    : $"The formats accepted here are: {formats}.",

                // Named plainly rather than as an accusation: the commonest cause is a file renamed
                // by hand, not an attack, and the reader can only act on the plain reading.
                Services.FileRejection.ContentMismatch => arabic
                    ? "محتوى الملف لا يطابق امتداده — أعد حفظه بالصيغة الصحيحة ثم أرفقه."
                    : "The file contents do not match its extension — save it in the right format and attach it again.",
                Services.FileRejection.ExpiryDateRequired => arabic
                    ? "هذا المستند يتتبَّع تاريخ الانتهاء — أدخل التاريخ قبل الرفع."
                    : "This document is expiry-tracked — enter the expiry date before uploading.",
                Services.FileRejection.UnknownDocumentType => arabic
                    ? "نوع المستند غير معرَّف أو موقوف — اختر نوعاً آخر."
                    : "That document type is not defined, or has been retired — choose another.",
                _ => arabic ? "تعذَّر قبول هذا الملف." : "That file cannot be accepted.",
            };
        }

        /// <summary>The formats a document type accepts, written the way a reader names them rather than as flag values.</summary>
        public static string FormatList(DocumentFormat formats)
        {
            var names = new List<string>();
            if ((formats & DocumentFormat.Pdf) != 0) { names.Add("PDF"); }
            if ((formats & DocumentFormat.Jpg) != 0) { names.Add("JPEG"); }
            if ((formats & DocumentFormat.Png) != 0) { names.Add("PNG"); }
            if ((formats & DocumentFormat.Docx) != 0) { names.Add("DOCX"); }
            if ((formats & DocumentFormat.Xlsx) != 0) { names.Add("XLSX"); }
            return names.Count == 0 ? "—" : string.Join(" · ", names);
        }

        /// <summary>The <c>accept</c> attribute for a file box, so the picker offers what the server would take.</summary>
        public static string AcceptAttribute(DocumentFormat formats)
        {
            var parts = new List<string>();
            if ((formats & DocumentFormat.Pdf) != 0) { parts.Add(".pdf,application/pdf"); }
            if ((formats & DocumentFormat.Jpg) != 0) { parts.Add(".jpg,.jpeg,image/jpeg"); }
            if ((formats & DocumentFormat.Png) != 0) { parts.Add(".png,image/png"); }
            if ((formats & DocumentFormat.Docx) != 0) { parts.Add(".docx"); }
            if ((formats & DocumentFormat.Xlsx) != 0) { parts.Add(".xlsx"); }
            return string.Join(",", parts);
        }

        /// <summary>doc 10 §2: where a stored document stands, for the badge on its row.</summary>
        public static string AttachmentStatus(Sms.Domain.Attachments.AttachmentStatus s, bool arabic) => s switch
        {
            Sms.Domain.Attachments.AttachmentStatus.PendingScan => arabic ? "قيد الفحص" : "Pending scan",
            Sms.Domain.Attachments.AttachmentStatus.Active => arabic ? "سليم" : "Stored",
            Sms.Domain.Attachments.AttachmentStatus.Quarantined => arabic ? "محجوز" : "Quarantined",
            Sms.Domain.Attachments.AttachmentStatus.Void => arabic ? "ملغى" : "Voided",
            _ => s.ToString(),
        };

        /// <summary>A stored size the way a person reads one, LTR digits in both directions.</summary>
        public static string FileSize(long bytes, bool arabic)
        {
            if (bytes >= 1024 * 1024)
            {
                var mb = bytes / (double)(1024 * 1024);
                return arabic ? $"{mb:0.#} م.ب" : $"{mb:0.#} MB";
            }

            var kb = bytes / 1024d;
            return arabic ? $"{kb:0.#} ك.ب" : $"{kb:0.#} KB";
        }

        public static string YearStatus(AcademicYearStatus s, bool arabic) => s switch
        {
            AcademicYearStatus.Preparation => arabic ? "إعداد" : "Preparation",
            AcademicYearStatus.Active => arabic ? "نشط" : "Active",
            AcademicYearStatus.Closing => arabic ? "قيد الإغلاق" : "Closing",
            AcademicYearStatus.Closed => arabic ? "مغلق" : "Closed",
            AcademicYearStatus.Archived => arabic ? "مؤرشف" : "Archived",
            _ => s.ToString(),
        };

        public static string ApplicationStatus(Sms.Domain.Admissions.ApplicationStatus s, bool arabic) => s switch
        {
            Sms.Domain.Admissions.ApplicationStatus.Draft => arabic ? "مسودة" : "Draft",
            Sms.Domain.Admissions.ApplicationStatus.Submitted => arabic ? "مقدَّم" : "Submitted",
            Sms.Domain.Admissions.ApplicationStatus.UnderReview => arabic ? "قيد المراجعة" : "Under review",
            Sms.Domain.Admissions.ApplicationStatus.Recommended => arabic ? "موصى به" : "Recommended",
            Sms.Domain.Admissions.ApplicationStatus.Approved => arabic ? "معتمد" : "Approved",
            Sms.Domain.Admissions.ApplicationStatus.Waitlisted => arabic ? "قائمة الانتظار" : "Waitlisted",
            Sms.Domain.Admissions.ApplicationStatus.Registered => arabic ? "مسجَّل" : "Registered",
            Sms.Domain.Admissions.ApplicationStatus.Rejected => arabic ? "مرفوض" : "Rejected",
            Sms.Domain.Admissions.ApplicationStatus.Lapsed => arabic ? "ساقط" : "Lapsed",
            _ => s.ToString(),
        };

        public static string Gender(GenderPolicy g, bool arabic) => g switch
        {
            GenderPolicy.Boys => arabic ? "بنين" : "Boys",
            GenderPolicy.Girls => arabic ? "بنات" : "Girls",
            _ => arabic ? "مختلط" : "Mixed",
        };

        /// <summary>
        /// One child's sex, which is not the same word as a grade's admission policy above — a
        /// grade is "بنين", a boy is "ذكر". The overload exists so a screen printing both cannot
        /// pick the wrong noun by accident.
        /// </summary>
        public static string Gender(Sms.Domain.Common.Gender g, bool arabic) => g switch
        {
            Sms.Domain.Common.Gender.Male => arabic ? "ذكر" : "Male",
            Sms.Domain.Common.Gender.Female => arabic ? "أنثى" : "Female",
            _ => g.ToString(),
        };

        /// <summary>
        /// BR-STU-002's statuses as a register prints them. Withdrawn and Transferred are not
        /// synonyms to a school — one left, the other left for a named school — and the Arabic
        /// keeps them apart the way the enum does.
        /// </summary>
        public static string StudentStatus(Sms.Domain.Students.StudentStatus s, bool arabic) => s switch
        {
            Sms.Domain.Students.StudentStatus.Enrolled => arabic ? "مقيّد" : "Enrolled",
            Sms.Domain.Students.StudentStatus.Suspended => arabic ? "موقوف" : "Suspended",
            Sms.Domain.Students.StudentStatus.Withdrawn => arabic ? "منسحب" : "Withdrawn",
            Sms.Domain.Students.StudentStatus.Graduated => arabic ? "متخرّج" : "Graduated",
            Sms.Domain.Students.StudentStatus.Transferred => arabic ? "منقول" : "Transferred",
            Sms.Domain.Students.StudentStatus.Alumni => arabic ? "خرّيج" : "Alumni",
            _ => s.ToString(),
        };

        /// <summary>
        /// The permission verbs (doc 06 §4.1) as the role designer prints them. The English is the
        /// enum name deliberately — an administrator reading a permission here should see the same
        /// word the catalogue and the <c>[RequirePermission]</c> attributes use, so a support answer
        /// and the screen agree.
        /// </summary>
        public static string Verb(Sms.Domain.Security.ActionVerb v, bool arabic) => !arabic ? v.ToString() : v switch
        {
            Sms.Domain.Security.ActionVerb.View => "عرض",
            Sms.Domain.Security.ActionVerb.Create => "إضافة",
            Sms.Domain.Security.ActionVerb.Edit => "تعديل",
            Sms.Domain.Security.ActionVerb.Deactivate => "تعطيل",
            Sms.Domain.Security.ActionVerb.Submit => "رفع",
            Sms.Domain.Security.ActionVerb.Approve => "اعتماد",
            Sms.Domain.Security.ActionVerb.Post => "ترحيل",
            Sms.Domain.Security.ActionVerb.Print => "طباعة",
            Sms.Domain.Security.ActionVerb.Export => "تصدير",
            Sms.Domain.Security.ActionVerb.Import => "استيراد",
            Sms.Domain.Security.ActionVerb.Configure => "ضبط",
            _ => v.ToString(),
        };

        /// <summary>
        /// Which kind of person a login belongs to (doc 06 §2). The account list printed the enum
        /// name itself, so an Arabic screen said "Staff" — and the distinction the column exists to
        /// draw, between a staff login and a portal one, is the one BR-SEC-010 routes on.
        /// </summary>
        public static string AccountType(Sms.Domain.Security.AccountType t, bool arabic) => t switch
        {
            Sms.Domain.Security.AccountType.Staff => arabic ? "موظف" : "Staff",
            Sms.Domain.Security.AccountType.Parent => arabic ? "ولي أمر" : "Parent",
            Sms.Domain.Security.AccountType.Student => arabic ? "طالب" : "Student",
            Sms.Domain.Security.AccountType.System => arabic ? "حساب نظام" : "System",
            _ => t.ToString(),
        };
    }
}
