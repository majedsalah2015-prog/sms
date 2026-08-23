using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Sms.Web.Models;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The photograph a person's file carries is now chosen on the *create* forms as well as the
    /// file screen (doc/Modules/10 §8 screen 1, BR-STU-008), which puts a refusal in front of a
    /// registrar with a family waiting at the counter. Two things have to hold there: the file is
    /// judged before anything is written, and the refusal is readable in the language they are
    /// working in.
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
            Assert.Equal(PhotoRejection.None, PersonPhotoService.Inspect(new FakeFormFile(name, contentType, 50_000)));
        }

        [Fact]
        public void Nothing_chosen_is_refused_rather_than_stored_as_an_empty_file()
        {
            Assert.Equal(PhotoRejection.NoFile, PersonPhotoService.Inspect(null));
            Assert.Equal(PhotoRejection.NoFile, PersonPhotoService.Inspect(new FakeFormFile("face.jpg", "image/jpeg", 0)));
        }

        [Fact]
        public void A_file_over_the_limit_is_refused()
        {
            Assert.Equal(
                PhotoRejection.TooLarge,
                PersonPhotoService.Inspect(new FakeFormFile("face.jpg", "image/jpeg", PersonPhotoService.MaxPhotoBytes + 1)));
        }

        [Theory]
        [InlineData("scan.pdf", "application/pdf")]
        [InlineData("payload.exe", "application/octet-stream")]
        // A document scan is the commonest wrong answer here: it is a real file of a real person,
        // and it is not a face. The frame on every roster and ID card assumes it is.
        [InlineData("certificate.tiff", "image/tiff")]
        public void Anything_that_is_not_a_jpeg_or_a_png_is_refused(string name, string contentType)
        {
            Assert.Equal(PhotoRejection.NotAnImage, PersonPhotoService.Inspect(new FakeFormFile(name, contentType, 50_000)));
        }

        /// <summary>
        /// The refusal is thrown as a reason, not a sentence. A service that threw English text
        /// would put it straight onto an Arabic screen, which is the defect this shape prevents.
        /// </summary>
        [Fact]
        public void The_refusal_carries_its_reason_rather_than_its_wording()
        {
            var ex = new PhotoRejectedException(PhotoRejection.TooLarge);

            Assert.Equal(PhotoRejection.TooLarge, ex.Rejection);
            Assert.IsAssignableFrom<InvalidOperationException>(ex);
        }

        // --- and the wording, in both languages -----------------------------

        [Theory]
        [InlineData(PhotoRejection.NoFile)]
        [InlineData(PhotoRejection.TooLarge)]
        [InlineData(PhotoRejection.NotAnImage)]
        public void Every_refusal_reads_in_both_languages(PhotoRejection rejection)
        {
            var arabic = Labels.PhotoRejection(rejection, true);
            var english = Labels.PhotoRejection(rejection, false);

            Assert.False(string.IsNullOrWhiteSpace(arabic), $"{rejection} has no Arabic text.");
            Assert.False(string.IsNullOrWhiteSpace(english), $"{rejection} has no English text.");
            Assert.NotEqual(arabic, english);

            // Not the enum name leaking through, and actually written in Arabic script — "JPEG" and
            // "2 MB" are allowed to stay Latin inside it, which is why this looks for the script
            // rather than forbidding Latin letters.
            Assert.DoesNotContain(rejection.ToString(), arabic, StringComparison.Ordinal);
            Assert.Contains(arabic, c => c is >= '؀' and <= 'ۿ');
        }
    }
}
