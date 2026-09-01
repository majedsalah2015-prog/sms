using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sms.Application.Installments;
using Sms.Application.ReadModels;
using Sms.Domain.Grades;
using Sms.Domain.Installments;
using Sms.Domain.Schools;
using Sms.Domain.Sections;

namespace Sms.Web.Models
{
    /// <summary>
    /// The collection roll as a file somebody opens somewhere else.
    /// <para>
    /// Both halves are the sort of thing that is wrong in a way nobody notices: a
    /// guardian's name containing a comma shifts every column after it, and an
    /// Arabic name without a byte-order mark arrives in Excel as mojibake — which
    /// reads as the system having mangled the school's arrears rather than as
    /// three missing bytes.
    /// </para>
    /// </summary>
    public static class CollectionExport
    {
        /// <summary>
        /// One CSV cell, always quoted with the quote itself doubled. Quoting only
        /// the cells that look dangerous means the escaping is decided by whoever
        /// typed the name.
        /// </summary>
        public static string Cell(string? value)
            => "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

        public static string Line(IEnumerable<string?> cells)
            => string.Join(",", (cells ?? Array.Empty<string?>()).Select(Cell));

        /// <summary>UTF-8 <b>with</b> a byte-order mark — the first thing anybody does with this download is open it in Excel.</summary>
        public static byte[] Bytes(IEnumerable<IEnumerable<string?>> records)
        {
            var text = new StringBuilder();
            foreach (var record in records ?? Array.Empty<IEnumerable<string?>>())
            {
                text.Append(Line(record)).Append("\r\n");
            }

            return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text.ToString())).ToArray();
        }
    }

    /// <summary>
    /// doc/Modules/20 §8.5 / §10 — the collection follow-up screen, its CSV export
    /// and its printable notice batch.
    /// <para>
    /// In its own file rather than in <c>InstallmentsViewModels.cs</c> for the
    /// reason CLAUDE.md gives about shared files: that one is edited by whoever is
    /// working on plan templates or the family schedule, and a screen added to the
    /// bottom of it is a merge conflict waiting for someone.
    /// </para>
    /// </summary>
    public sealed class CollectionRollViewModel
    {
        public IReadOnlyList<AcademicYear> Years { get; set; } = Array.Empty<AcademicYear>();

        public AcademicYear? Year { get; set; }

        /// <summary>Live grades only — this is the picker, not the lookup (SoftActiveLookupTests' distinction).</summary>
        public IReadOnlyList<GradeLevel> Grades { get; set; } = Array.Empty<GradeLevel>();

        public IReadOnlyList<Section> Sections { get; set; } = Array.Empty<Section>();

        public CollectionFilter Filter { get; set; } = new();

        public IReadOnlyList<CollectionRow> Rows { get; set; } = Array.Empty<CollectionRow>();

        public int MatchCount { get; set; }

        public bool IsTruncated { get; set; }

        /// <summary>Over everything the filter matched, not over the page — see <see cref="CollectionRoll"/>.</summary>
        public decimal TotalOutstanding { get; set; }

        /// <summary>What may actually be chased once BR-INS-009's cheque-covered balances are set aside.</summary>
        public decimal TotalNotifiable { get; set; }

        /// <summary>Drives the Export/Print/Send buttons: offered only to a user who holds the verb (BR-SEC-010 hides rather than refuses).</summary>
        public bool CanExport { get; set; }

        public bool CanPrint { get; set; }

        public bool CanSend { get; set; }

        /// <summary>The window as the officer typed it, echoed back so a refused range is corrected rather than retyped.</summary>
        public string? WindowError { get; set; }
    }

    /// <summary>
    /// One printed batch — doc/Modules/20 §8.5's "pending letter batches", rendered
    /// as one bilingual page per family.
    /// <para>
    /// <b>Deviation from doc/UI/02 §5 "print", stated rather than substituted.</b>
    /// BR-INS-008 calls the letter stage a *numbered* formal document in Module 18's
    /// pattern, which in that catalogue means a server-rendered PDF with template
    /// slots. The PDF engine is still an open owner decision — QuestPDF fails at
    /// bidi on .NET 5 and the fix is net6+ (docs/Status, gap O6), which is why
    /// certificates, statements and receipts all print through the browser here
    /// too. The number is real and issued from series DUN; the sheet is a print
    /// stylesheet over a bilingual layout, and it is not a sealed document.
    /// </para>
    /// </summary>
    public sealed class CollectionNoticesViewModel
    {
        public IReadOnlyList<IssuedNotice> Notices { get; set; } = Array.Empty<IssuedNotice>();

        public string SchoolNameAr { get; set; } = string.Empty;

        public string SchoolNameEn { get; set; } = string.Empty;

        public DateTime PrintedAtUtc { get; set; }

        public DateTime? WindowFrom { get; set; }

        public DateTime? WindowTo { get; set; }

        /// <summary>Selected but not written to, and why — the counts <see cref="NoticeBatch"/> reports.</summary>
        public int SkippedNothingOutstanding { get; set; }

        public int SkippedPdcCovered { get; set; }
    }

    /// <summary>
    /// The collection screen's own labels. Kept out of <c>Labels.cs</c> and
    /// <c>FinanceLabels</c> for the same reason the view models are kept out of
    /// <c>InstallmentsViewModels.cs</c> — both of those are shared, and both are hot.
    /// </summary>
    public static class CollectionLabels
    {
        public static string Channel(CollectionNoticeChannel c, bool ar) => c switch
        {
            CollectionNoticeChannel.Paper => ar ? "إشعار ورقي" : "Paper notice",
            CollectionNoticeChannel.Portal => ar ? "إشعار عبر البوابة" : "Portal notification",
            _ => c.ToString(),
        };

        /// <summary>
        /// Where a due item came from, said plainly. A parent reading a printed
        /// notice does not know what an "unscheduled charge" is; a collection
        /// officer reading the roll needs to know that this family has no plan, so
        /// only the roll shows it.
        /// </summary>
        public static string Source(DueItemSource s, bool ar) => s switch
        {
            DueItemSource.Installment => ar ? "قسط" : "Installment",
            DueItemSource.UnscheduledCharge => ar ? "فاتورة بلا خطة تقسيط" : "Charge with no plan",
            _ => s.ToString(),
        };

        /// <summary>
        /// The severity a bucket is drawn at. Current is not a problem, ninety days
        /// is; colouring them the same makes the officer read every row to find the
        /// two that matter.
        /// </summary>
        public static string BucketBadge(AgingBucket b) => b switch
        {
            AgingBucket.Current => "bg-secondary",
            AgingBucket.Days1To30 => "bg-info text-dark",
            AgingBucket.Days31To60 => "bg-warning text-dark",
            AgingBucket.Days61To90 => "bg-danger",
            _ => "bg-dark",
        };
    }
}
