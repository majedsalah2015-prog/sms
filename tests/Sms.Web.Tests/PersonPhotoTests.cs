using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Sms.Domain.Attachments;
using Sms.Web.Models;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The photograph a person's file carries is chosen on the *create* forms as well as the file
    /// screen (doc/Modules/10 §8 screen 1, BR-STU-008), which puts a refusal in front of a
    /// registrar with a family waiting at the counter. Two things have to hold there: the file is
    /// judged before anything is written, and the refusal is readable in the language they are
    /// working in.
    /// <para>
    /// The photo slot is one configuration of <see cref="AttachmentIntake"/> now, so these tests
    /// also pin that the slot keeps its own narrow rules — a PDF is a perfectly good attachment
    /// and a perfectly bad face.
    /// </para>
    /// </summary>
    public class PersonPhotoTests
    {
        /// <summary>Enough of IFormFile to be inspected — nothing here is ever read or stored.</summary>
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

            public string Name => "photo";

            public string FileName { get; }

            public void CopyTo(Stream target) => throw new NotSupportedException();

            public System.Threading.Tasks.Task CopyToAsync(Stream target, System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Stream OpenReadStream() => throw new NotSupportedException();
        }

        // --- what may be a photograph (BR-ATT-002/003 via the photo slot) ----

        [Theory]
        [InlineData("face.jpg", "image/jpeg")]
        [InlineData("FACE.JPEG", "image/jpeg")]
        [InlineData("face.png", "image/png")]
        // The browser's content type is the second opinion when the name carries no extension —
        // phones and scanners both send files called things like "image" and "scan".
        [InlineData("image", "image/png")]
        public void A_jpeg_or_png_within_the_limit_is_accepted(string name, string contentType)
        {
            Assert.Equal(FileRejection.None, PersonPhotoService.Inspect(new FakeFormFile(name, contentType, 50_000)));
        }

        [Fact]
        public void Nothing_chosen_is_refused_rather_than_stored_as_an_empty_file()
        {
            Assert.Equal(FileRejection.NoFile, PersonPhotoService.Inspect(null));
            Assert.Equal(FileRejection.NoFile, PersonPhotoService.Inspect(new FakeFormFile("face.jpg", "image/jpeg", 0)));
        }

        [Fact]
        public void A_file_over_the_limit_is_refused()
        {
            Assert.Equal(
                FileRejection.TooLarge,
                PersonPhotoService.Inspect(new FakeFormFile("face.jpg", "image/jpeg", PersonPhotoService.MaxPhotoBytes + 1)));
        }

        /// <summary>
        /// A document scan is the commonest wrong answer here: a real file of a real person that is
        /// not a face. The frame on every roster and ID card assumes it is one. The intake knows
        /// what a PDF is — it simply is not allowed in this slot, which is a different refusal from
        /// "no idea what this file is" and reads differently to the registrar.
        /// </summary>
        [Fact]
        public void A_format_the_product_stores_but_the_photo_slot_does_not_take_is_refused_as_such()
        {
            Assert.Equal(
                FileRejection.FormatNotAllowed,
                PersonPhotoService.Inspect(new FakeFormFile("scan.pdf", "application/pdf", 50_000)));
        }

        [Theory]
        [InlineData("payload.exe", "application/octet-stream")]
        [InlineData("certificate.tiff", "image/tiff")]
        public void Anything_the_product_does_not_store_at_all_is_refused(string name, string contentType)
        {
            Assert.Equal(FileRejection.UnknownFormat, PersonPhotoService.Inspect(new FakeFormFile(name, contentType, 50_000)));
        }

        /// <summary>
        /// The refusal is thrown as a reason, not a sentence. A service that threw English text
        /// would put it straight onto an Arabic screen, which is the defect this shape prevents.
        /// It carries the limits too, so the wording can name the way out rather than only the wall.
        /// </summary>
        [Fact]
        public void The_refusal_carries_its_reason_and_limits_rather_than_its_wording()
        {
            var ex = new FileRejectedException(FileRejection.TooLarge, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes);

            Assert.Equal(FileRejection.TooLarge, ex.Rejection);
            Assert.Equal(PersonPhotoService.PhotoFormats, ex.AllowedFormats);
            Assert.Equal(PersonPhotoService.MaxPhotoBytes, ex.MaxBytes);
            Assert.IsAssignableFrom<InvalidOperationException>(ex);
        }

        // --- and the wording, in both languages -----------------------------

        [Theory]
        [InlineData(FileRejection.NoFile)]
        [InlineData(FileRejection.TooLarge)]
        [InlineData(FileRejection.UnknownFormat)]
        [InlineData(FileRejection.FormatNotAllowed)]
        [InlineData(FileRejection.ContentMismatch)]
        [InlineData(FileRejection.ExpiryDateRequired)]
        [InlineData(FileRejection.UnknownDocumentType)]
        public void Every_refusal_reads_in_both_languages(FileRejection rejection)
        {
            var arabic = Labels.FileRejection(rejection, true, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes);
            var english = Labels.FileRejection(rejection, false, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes);

            Assert.False(string.IsNullOrWhiteSpace(arabic), $"{rejection} has no Arabic text.");
            Assert.False(string.IsNullOrWhiteSpace(english), $"{rejection} has no English text.");
            Assert.NotEqual(arabic, english);

            // Not the enum name leaking through, and actually written in Arabic script — "JPEG" and
            // "2 MB" are allowed to stay Latin inside it, which is why this looks for the script
            // rather than forbidding Latin letters.
            Assert.DoesNotContain(rejection.ToString(), arabic, StringComparison.Ordinal);
            Assert.Contains(arabic, c => c is >= '؀' and <= 'ۿ');
        }

        /// <summary>
        /// A refusal that says "too large" without saying how large is a dead end. The size the
        /// message quotes has to be the slot's own, in both languages.
        /// </summary>
        [Fact]
        public void The_size_refusal_names_the_limit_it_enforced()
        {
            Assert.Contains("2", Labels.FileRejection(FileRejection.TooLarge, false, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes));
            Assert.Contains("2", Labels.FileRejection(FileRejection.TooLarge, true, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes));
        }

        /// <summary>
        /// And a format refusal has to name what would have been taken. The same sentence serves a
        /// photo slot and a contract slot, so it reads the type's formats rather than hard-coding
        /// "JPEG or PNG".
        /// </summary>
        [Fact]
        public void The_format_refusal_names_what_the_slot_would_have_taken()
        {
            var photo = Labels.FileRejection(FileRejection.FormatNotAllowed, false, PersonPhotoService.PhotoFormats, PersonPhotoService.MaxPhotoBytes);
            Assert.Contains("JPEG", photo, StringComparison.Ordinal);
            Assert.Contains("PNG", photo, StringComparison.Ordinal);
            Assert.DoesNotContain("PDF", photo, StringComparison.Ordinal);

            var contract = Labels.FileRejection(FileRejection.FormatNotAllowed, false, DocumentFormat.Pdf, 10L * 1024 * 1024);
            Assert.Contains("PDF", contract, StringComparison.Ordinal);
            Assert.DoesNotContain("JPEG", contract, StringComparison.Ordinal);
        }
    }
}
