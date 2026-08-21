using System;
using System.Globalization;
using System.IO;
using System.Text;
using QRCoder;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace O6Spike
{
    public static class Program
    {
        const string Font = "Tahoma";

        public static void Main()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            var outDir = Path.Combine(AppContext.BaseDirectory, "out");
            Directory.CreateDirectory(outDir);

            var reportCard = BuildReportCard();
            reportCard.GeneratePdf(Path.Combine(outDir, "report-card-ar.pdf"));
            reportCard.GenerateImages(i => Path.Combine(outDir, $"report-card-ar-{i}.png"));

            var invoice = BuildTaxInvoice();
            invoice.GeneratePdf(Path.Combine(outDir, "tax-invoice-zatca.pdf"));
            invoice.GenerateImages(i => Path.Combine(outDir, $"tax-invoice-zatca-{i}.png"));

            Console.WriteLine("OK: " + outDir);
        }

        // ----- fixture helpers -----

        static string ArabicIndic(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
                sb.Append(c >= '0' && c <= '9' ? (char)('٠' + (c - '0')) : c);
            return sb.ToString();
        }

        static string HijriDate(DateTime g)
        {
            var h = new HijriCalendar();
            return ArabicIndic($"{h.GetDayOfMonth(g)}/{h.GetMonth(g)}/{h.GetYear(g)} هـ");
        }

        static byte[] ZatcaQrPng()
        {
            // TLV tags 1..5 per ZATCA Phase-1 simplified invoice
            static byte[] Tlv(byte tag, string value)
            {
                var v = Encoding.UTF8.GetBytes(value);
                var buf = new byte[2 + v.Length];
                buf[0] = tag; buf[1] = (byte)v.Length;
                Array.Copy(v, 0, buf, 2, v.Length);
                return buf;
            }
            using var ms = new MemoryStream();
            ms.Write(Tlv(1, "مدارس الرياض النموذجية"));
            ms.Write(Tlv(2, "310123456700003"));
            ms.Write(Tlv(3, "2026-09-01T10:30:00Z"));
            ms.Write(Tlv(4, "13800.00"));
            ms.Write(Tlv(5, "1800.00"));
            var payload = Convert.ToBase64String(ms.ToArray());

            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
            return new PngByteQRCode(data).GetGraphic(10);
        }

        // ----- document (a): Arabic report card -----

        static Document BuildReportCard() => Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(11).DirectionAuto());

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("مدارس الرياض النموذجية").FontSize(18).Bold();
                    col.Item().AlignCenter().Text("Riyadh Model Schools").FontSize(10);
                    col.Item().AlignCenter().Text("إشعار نتيجة الفصل الدراسي الأول — العام الدراسي ١٤٤٨هـ / 2026-2027م").FontSize(12);
                    col.Item().PaddingTop(8).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("اسم الطالب: ").SemiBold(); t.Span("أحمد محمد عبدالله الغامدي"); });
                            c.Item().Text(t => { t.Span("الصف: ").SemiBold(); t.Span("الرابع الابتدائي — فصل (أ)"); });
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("رقم الطالب: ").SemiBold(); t.Span(ArabicIndic("STU/1448/00042")); });
                            c.Item().Text(t => { t.Span("تاريخ الإصدار: ").SemiBold(); t.Span($"{HijriDate(new DateTime(2027, 1, 14))} — 14/01/2027م"); });
                        });
                    });

                    col.Item().PaddingTop(12).Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });

                        void HeaderCell(string txt) => table.Cell().Background("#0D3B66").Padding(5)
                            .Text(txt).FontColor("#FFFFFF").SemiBold();
                        HeaderCell("المادة"); HeaderCell("الدرجة"); HeaderCell("النهاية العظمى"); HeaderCell("التقدير");

                        void BodyRow(string subject, int mark, string gradeLabel)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(subject);
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(ArabicIndic(mark.ToString()));
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(ArabicIndic("100"));
                            table.Cell().BorderBottom(0.5f).Padding(5).Text(gradeLabel);
                        }
                        BodyRow("القرآن الكريم والدراسات الإسلامية", 95, "ممتاز");
                        BodyRow("اللغة العربية", 88, "جيد جداً");
                        BodyRow("الرياضيات — Mathematics", 92, "ممتاز");
                        BodyRow("العلوم — Science", 76, "جيد");
                        BodyRow("اللغة الإنجليزية — English Language", 84, "جيد جداً");
                        BodyRow("الدراسات الاجتماعية", 90, "ممتاز");
                    });

                    col.Item().PaddingTop(10).Row(row =>
                    {
                        row.RelativeItem().Text(t => { t.Span("المجموع: ").SemiBold(); t.Span(ArabicIndic("525") + " من " + ArabicIndic("600")); });
                        row.RelativeItem().Text(t => { t.Span("النسبة المئوية: ").SemiBold(); t.Span(ArabicIndic("87.5") + " ٪"); });
                        row.RelativeItem().Text(t => { t.Span("التقدير العام: ").SemiBold(); t.Span("جيد جداً"); });
                    });

                    col.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("راصد الدرجات");
                            c.Item().PaddingTop(24).LineHorizontal(0.5f);
                        });
                        row.ConstantItem(60);
                        row.RelativeItem().AlignCenter().Column(c =>
                        {
                            c.Item().AlignCenter().Text("مدير المدرسة");
                            c.Item().PaddingTop(24).LineHorizontal(0.5f);
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.DefaultTextStyle(x => x.FontSize(8).FontColor("#666666"));
                    t.Span("وثيقة إلكترونية صادرة من نظام إدارة المدارس — للتحقق: ");
                    t.Span("VRF-8C41-KM29");
                });
            });
        });

        // ----- document (b): ZATCA simplified tax invoice -----

        static Document BuildTaxInvoice() => Document.Create(container =>
        {
            var qr = ZatcaQrPng();

            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.ContentFromRightToLeft();
                page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9.5f).DirectionAuto());

                page.Header().Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("فاتورة ضريبية مبسطة").FontSize(14).Bold();
                        c.Item().Text("Simplified Tax Invoice").FontSize(9).FontColor("#555555");
                    });
                    row.ConstantItem(90).Image(qr);
                });

                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Item().LineHorizontal(1);
                    col.Item().PaddingVertical(6).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("البائع: ").SemiBold(); t.Span("مدارس الرياض النموذجية"); });
                            c.Item().Text(t => { t.Span("الرقم الضريبي: ").SemiBold(); t.Span(ArabicIndic("310123456700003")); });
                        });
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text(t => { t.Span("رقم الفاتورة: ").SemiBold(); t.Span("INV/1448/000123"); });
                            c.Item().Text(t => { t.Span("التاريخ: ").SemiBold(); t.Span(ArabicIndic("01/09/2026") + " 10:30"); });
                        });
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3);
                            c.RelativeColumn();
                            c.RelativeColumn();
                            c.RelativeColumn();
                        });
                        void H(string s) => table.Cell().Background("#EEEEEE").Padding(4).Text(s).SemiBold();
                        H("البيان"); H("المبلغ"); H("الضريبة ١٥٪"); H("الإجمالي");

                        void L(string desc, string net, string vat, string gross)
                        {
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(desc);
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(ArabicIndic(net));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(ArabicIndic(vat));
                            table.Cell().BorderBottom(0.5f).Padding(4).Text(ArabicIndic(gross));
                        }
                        L("رسوم دراسية — الفصل الدراسي الأول", "10,000.00", "1,500.00", "11,500.00");
                        L("رسوم نقل مدرسي — المنطقة (ب)", "2,000.00", "300.00", "2,300.00");
                    });

                    col.Item().PaddingTop(8).AlignLeft().Column(c =>
                    {
                        c.Item().Text(t => { t.Span("الإجمالي قبل الضريبة: ").SemiBold(); t.Span(ArabicIndic("12,000.00") + " ر.س"); });
                        c.Item().Text(t => { t.Span("ضريبة القيمة المضافة (١٥٪): ").SemiBold(); t.Span(ArabicIndic("1,800.00") + " ر.س"); });
                        c.Item().Text(t => { t.Span("الإجمالي شامل الضريبة: ").SemiBold().FontSize(11); t.Span(ArabicIndic("13,800.00") + " ر.س").FontSize(11).Bold(); });
                    });
                });

                page.Footer().AlignCenter().Text("هذه الفاتورة صادرة وفق متطلبات هيئة الزكاة والضريبة والجمارك — المرحلة الأولى")
                    .FontSize(7.5f).FontColor("#666666");
            });
        });
    }
}
