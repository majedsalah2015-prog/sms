using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Sms.Domain.Attachments;
using Sms.Domain.Schools;
using Sms.Web.Models;
using Sms.Web.Services;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// BR-SCH-006: the school's logo and seal are attachments in two named slots, and the slot is
    /// what decides how narrow the rules are. These pin the narrowing — a PDF is a perfectly good
    /// attachment and a perfectly bad logo — and the two deviations from doc/Modules/02 §9 that
    /// this build makes deliberately: SVG is refused, JPG is accepted.
    /// </summary>
    public class SchoolBrandingTests
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

            public string Name => "file";

            public string FileName { get; }

            public void CopyTo(Stream target) => throw new NotSupportedException();

            public System.Threading.Tasks.Task CopyToAsync(Stream target, System.Threading.CancellationToken cancellationToken = default)
                => throw new NotSupportedException();

            public Stream OpenReadStream() => throw new NotSupportedException();
        }

        // --- what may be a branding mark -------------------------------------

        [Theory]
        [InlineData("crest.png", "image/png")]
        [InlineData("CREST.PNG", "image/png")]
        // JPG is a deviation from doc 02 §9's "PNG/SVG", taken deliberately: it is verifiable from
        // its first bytes, and it is the format a school's existing logo usually arrives in.
        [InlineData("crest.jpg", "image/jpeg")]
        [InlineData("crest.jpeg", "image/jpeg")]
        public void A_png_or_jpg_within_the_limit_is_accepted(string name, string contentType)
        {
            Assert.Equal(FileRejection.None, SchoolBrandingService.Inspect(new FakeFormFile(name, contentType, 50_000)));
        }

        /// <summary>
        /// doc 02 §9 lists SVG and this build refuses it, which is a stated deviation rather than an
        /// oversight: an SVG is active content — it can carry script — and branding is served inline
        /// to every signed-in reader, so admitting one would be a stored-XSS decision taken in
        /// passing. The product has no SVG member in <see cref="DocumentFormat"/> to admit it with.
        /// </summary>
        [Fact]
        public void An_svg_is_refused_although_the_doc_lists_it()
        {
            Assert.Equal(
                FileRejection.UnknownFormat,
                SchoolBrandingService.Inspect(new FakeFormFile("crest.svg", "image/svg+xml", 50_000)));
        }

        [Fact]
        public void Nothing_chosen_is_refused_rather_than_stored_as_an_empty_file()
        {
            Assert.Equal(FileRejection.NoFile, SchoolBrandingService.Inspect(null));
            Assert.Equal(FileRejection.NoFile, SchoolBrandingService.Inspect(new FakeFormFile("crest.png", "image/png", 0)));
        }

        /// <summary>doc 02 §9 names two megabytes, and BR-ATT-003 is what enforces it.</summary>
        [Fact]
        public void A_file_over_the_two_megabyte_limit_is_refused()
        {
            Assert.Equal(2 * 1024 * 1024, SchoolBrandingService.MaxBrandingBytes);
            Assert.Equal(
                FileRejection.TooLarge,
                SchoolBrandingService.Inspect(new FakeFormFile("crest.png", "image/png", SchoolBrandingService.MaxBrandingBytes + 1)));
        }

        /// <summary>
        /// A scanned letterhead is the commonest wrong answer here: a real file carrying the real
        /// mark, in a format nothing can draw into a document header. "Not in this slot" is a
        /// different refusal from "no idea what this is", and reads differently to the operator.
        /// </summary>
        [Fact]
        public void A_format_the_product_stores_but_the_branding_slot_does_not_take_is_refused_as_such()
        {
            Assert.Equal(
                FileRejection.FormatNotAllowed,
                SchoolBrandingService.Inspect(new FakeFormFile("letterhead.pdf", "application/pdf", 50_000)));
        }

        // --- the slots ---------------------------------------------------------

        /// <summary>
        /// Two document types, not one: a school that files a seal must not overwrite the logo it
        /// filed last week, and the attachment store keys a slot by (owning entity, document type).
        /// </summary>
        [Fact]
        public void The_logo_and_the_seal_are_different_document_types()
        {
            Assert.Equal(SchoolBrandingService.LogoType, SchoolBrandingService.TypeCodeOf(SchoolBrandingAsset.Logo));
            Assert.Equal(SchoolBrandingService.SealType, SchoolBrandingService.TypeCodeOf(SchoolBrandingAsset.Seal));
            Assert.NotEqual(SchoolBrandingService.LogoType, SchoolBrandingService.SealType);
        }

        // --- and the wording, in both languages --------------------------------

        /// <summary>
        /// Every refusal an operator can trigger is translated at the Web boundary; the service
        /// throws a reason, never a sentence. An English wall on an Arabic screen is a defect here,
        /// not a cosmetic complaint.
        /// </summary>
        [Theory]
        [InlineData(FileRejection.NoFile)]
        [InlineData(FileRejection.TooLarge)]
        [InlineData(FileRejection.UnknownFormat)]
        [InlineData(FileRejection.FormatNotAllowed)]
        [InlineData(FileRejection.ContentMismatch)]
        public void Every_branding_refusal_reads_in_both_languages(FileRejection rejection)
        {
            var english = Labels.FileRejection(rejection, arabic: false, SchoolBrandingService.BrandingFormats, SchoolBrandingService.MaxBrandingBytes);
            var arabic = Labels.FileRejection(rejection, arabic: true, SchoolBrandingService.BrandingFormats, SchoolBrandingService.MaxBrandingBytes);

            Assert.False(string.IsNullOrWhiteSpace(english));
            Assert.False(string.IsNullOrWhiteSpace(arabic));
            Assert.NotEqual(english, arabic);
        }
    }
}
