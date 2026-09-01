using System;
using System.IO;
using System.Text;
using Microsoft.AspNetCore.Http;
using Sms.Domain.Attachments;
using Sms.Web.Models;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// doc 10 §2/§5: one gate for every file the product takes. What it decides has to hold the
    /// same whether the file arrived on a registration form, a documents tab or a portal upload —
    /// the whole reason the gate exists is that those three used to disagree.
    /// </summary>
    public class AttachmentIntakeTests
    {
        private const DocumentFormat Documents = DocumentFormat.Pdf | DocumentFormat.Jpg | DocumentFormat.Png;

        private sealed class FakeFormFile : IFormFile
        {
            public FakeFormFile(string fileName, string contentType, long length)
            {
                FileName = fileName;
                ContentType = contentType;
                Length = length;
            }

            public string ContentType { get; }

            public string ContentDisposition => string.Empty;

            public IHeaderDictionary Headers => new HeaderDictionary();

            public long Length { get; }

            public string Name => "file";

            public string FileName { get; }

            public void CopyTo(Stream target) => throw new NotSupportedException();

            public System.Threading.Tasks.Task CopyToAsync(Stream target, System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Stream OpenReadStream() => throw new NotSupportedException();
        }

        // --- naming a file --------------------------------------------------

        [Theory]
        [InlineData("birth.pdf", "application/pdf", DocumentFormat.Pdf)]
        [InlineData("BIRTH.PDF", "application/pdf", DocumentFormat.Pdf)]
        [InlineData("face.jpeg", "image/jpeg", DocumentFormat.Jpg)]
        [InlineData("face.png", "image/png", DocumentFormat.Png)]
        [InlineData("contract.docx", "", DocumentFormat.Docx)]
        [InlineData("marks.xlsx", "", DocumentFormat.Xlsx)]
        public void The_extension_names_the_format(string name, string contentType, DocumentFormat expected)
        {
            Assert.Equal(expected, AttachmentIntake.FormatOf(name, contentType));
        }

        [Fact]
        public void The_browser_content_type_answers_when_the_name_carries_no_extension()
        {
            // Phones and scanners both send files called things like "scan" and "image".
            Assert.Equal(DocumentFormat.Pdf, AttachmentIntake.FormatOf("scan", "application/pdf"));
            Assert.Null(AttachmentIntake.FormatOf("scan", "application/octet-stream"));
        }

        // --- BR-ATT-002 content inspection ----------------------------------

        /// <summary>
        /// The rule says "rejected by content inspection, not extension alone". An extension is a
        /// claim; the first bytes are the evidence. Renaming a file is the whole attack, and it is
        /// also the commonest honest mistake.
        /// </summary>
        [Fact]
        public void A_file_renamed_to_a_format_it_is_not_fails_content_inspection()
        {
            var executable = Encoding.ASCII.GetBytes("MZ\u0090\0\u0003");

            Assert.False(AttachmentIntake.ContentMatches(DocumentFormat.Pdf, executable));
            Assert.False(AttachmentIntake.ContentMatches(DocumentFormat.Jpg, executable));
            Assert.False(AttachmentIntake.ContentMatches(DocumentFormat.Png, executable));
        }

        [Fact]
        public void The_real_signatures_pass()
        {
            Assert.True(AttachmentIntake.ContentMatches(DocumentFormat.Pdf, Encoding.ASCII.GetBytes("%PDF-1.7")));
            Assert.True(AttachmentIntake.ContentMatches(DocumentFormat.Jpg, new byte[] { 0xFF, 0xD8, 0xFF, 0xE0 }));
            Assert.True(AttachmentIntake.ContentMatches(DocumentFormat.Png, new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00 }));

            // Both Office formats are zip containers; what is inside is the package's business.
            Assert.True(AttachmentIntake.ContentMatches(DocumentFormat.Docx, new byte[] { 0x50, 0x4B, 0x03, 0x04 }));
            Assert.True(AttachmentIntake.ContentMatches(DocumentFormat.Xlsx, new byte[] { 0x50, 0x4B, 0x05, 0x06 }));
        }

        [Fact]
        public void A_file_shorter_than_its_own_signature_does_not_pass_by_running_out()
        {
            Assert.False(AttachmentIntake.ContentMatches(DocumentFormat.Png, new byte[] { 0x89, 0x50 }));
            Assert.False(AttachmentIntake.ContentMatches(DocumentFormat.Pdf, Array.Empty<byte>()));
        }

        // --- BR-ATT-003 limits ----------------------------------------------

        [Fact]
        public void A_type_with_no_limit_of_its_own_gets_the_product_default()
        {
            var type = new DocumentType { MaxSizeBytes = null };
            Assert.Equal(10L * 1024 * 1024, AttachmentIntake.EffectiveMaxBytes(type));
        }

        [Fact]
        public void A_type_configured_above_the_product_ceiling_is_held_to_the_ceiling()
        {
            var type = new DocumentType { MaxSizeBytes = int.MaxValue };
            Assert.Equal(25L * 1024 * 1024, AttachmentIntake.EffectiveMaxBytes(type));
        }

        [Fact]
        public void Inspection_separates_a_format_it_cannot_name_from_one_this_slot_will_not_take()
        {
            Assert.Equal(
                FileRejection.UnknownFormat,
                AttachmentIntake.Inspect(new FakeFormFile("thing.bin", "application/octet-stream", 10), Documents, 1024));

            Assert.Equal(
                FileRejection.FormatNotAllowed,
                AttachmentIntake.Inspect(new FakeFormFile("sheet.xlsx", "", 10), Documents, 1024));
        }

        // --- what the screen is told ----------------------------------------

        /// <summary>
        /// The box on the page and the rule on the server have to agree, or the picker offers a
        /// file the upload then refuses. Both are derived from the same flags for that reason.
        /// </summary>
        [Fact]
        public void The_accept_attribute_and_the_readable_format_list_come_from_the_same_flags()
        {
            var accept = Labels.AcceptAttribute(Documents);
            Assert.Contains(".pdf", accept, StringComparison.Ordinal);
            Assert.Contains(".jpg", accept, StringComparison.Ordinal);
            Assert.Contains(".jpeg", accept, StringComparison.Ordinal);
            Assert.Contains(".png", accept, StringComparison.Ordinal);
            Assert.DoesNotContain(".docx", accept, StringComparison.Ordinal);

            var readable = Labels.FormatList(Documents);
            Assert.Equal("PDF · JPEG · PNG", readable);
        }

        [Fact]
        public void Only_the_formats_a_browser_can_show_earn_a_thumbnail()
        {
            Assert.True(AttachmentIntake.IsImage(DocumentFormat.Jpg));
            Assert.True(AttachmentIntake.IsImage(DocumentFormat.Png));
            Assert.False(AttachmentIntake.IsImage(DocumentFormat.Pdf));
        }

        [Fact]
        public void A_stored_file_is_served_as_what_it_is()
        {
            Assert.Equal("application/pdf", AttachmentIntake.ContentTypeOf(DocumentFormat.Pdf));
            Assert.Equal("image/png", AttachmentIntake.ContentTypeOf(DocumentFormat.Png));
            Assert.Equal("image/jpeg", AttachmentIntake.ContentTypeOf(DocumentFormat.Jpg));
        }

        /// <summary>Some browsers still send the whole path; none of it may reach the store.</summary>
        [Theory]
        [InlineData("C:\\Users\\registrar\\Desktop\\birth.pdf", "birth.pdf")]
        [InlineData("/home/registrar/birth.pdf", "birth.pdf")]
        [InlineData("", "file")]
        public void The_stored_name_is_the_file_name_and_nothing_around_it(string sent, string expected)
        {
            Assert.Equal(expected, AttachmentIntake.SafeName(sent));
        }

        [Fact]
        public void A_size_reads_in_both_languages_without_becoming_a_different_number()
        {
            Assert.Equal("1.5 MB", Labels.FileSize(1_572_864, false));
            Assert.Contains("1.5", Labels.FileSize(1_572_864, true));
        }

        [Theory]
        [InlineData(AttachmentStatus.PendingScan)]
        [InlineData(AttachmentStatus.Active)]
        [InlineData(AttachmentStatus.Quarantined)]
        [InlineData(AttachmentStatus.Void)]
        public void A_documents_state_is_never_printed_as_its_enum_name(AttachmentStatus status)
        {
            var arabic = Labels.AttachmentStatus(status, true);

            Assert.NotEqual(status.ToString(), arabic);
            Assert.Contains(arabic, c => c is >= '؀' and <= 'ۿ');
            Assert.False(string.IsNullOrWhiteSpace(Labels.AttachmentStatus(status, false)));
        }
    }
}
