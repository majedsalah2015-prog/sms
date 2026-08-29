using System;
using System.Collections.Generic;
using Sms.Domain.Attachments;
using Sms.Web.Services;

namespace Sms.Web.Models
{
    /// <summary>How large the preview frame is drawn — a face is a portrait, a document is a strip.</summary>
    public enum FileUploadShape
    {
        /// <summary>A wide, short frame: enough to recognise the page that was chosen, not enough to read it.</summary>
        Strip = 0,

        /// <summary>The person-photograph frame, the same one the file screens already use.</summary>
        Portrait = 1,
    }

    /// <summary>
    /// The one file box in this product (doc 10 §5 "Upload widget … embedded in every owning
    /// screen"). Drag-and-drop or click, the chosen file named and measured back to the person who
    /// chose it, and an image shown before anything is sent — because the commonest upload mistake
    /// is not a bad file, it is the wrong one, and only the person choosing it can catch that.
    /// <para>
    /// The limits carried here are the document type's own, read from the catalogue, so the box on
    /// the page and the rule on the server say the same thing. Nothing here is a check: the client
    /// warns early, <see cref="AttachmentIntake"/> decides.
    /// </para>
    /// </summary>
    public sealed class FileUploadFieldViewModel
    {
        /// <summary>DOM id prefix; must be unique on the page.</summary>
        public string Id { get; set; } = "file";

        /// <summary>The posted form field name.</summary>
        public string Name { get; set; } = "file";

        public string? Label { get; set; }

        public DocumentFormat AllowedFormats { get; set; } = DocumentFormat.Pdf | DocumentFormat.Jpg | DocumentFormat.Png;

        public long MaxBytes { get; set; } = 10L * 1024 * 1024;

        public bool Required { get; set; }

        public FileUploadShape Shape { get; set; } = FileUploadShape.Strip;

        /// <summary>Shown in the frame until something is chosen — the photograph already on file, where there is one.</summary>
        public string? CurrentImageUrl { get; set; }

        /// <summary>An extra line under the box: what this particular slot is for.</summary>
        public string? Hint { get; set; }

        /// <summary>
        /// The id of a select whose chosen option carries the real rules, for a form where the
        /// document type is picked beside the file. Without it the box would have to advertise the
        /// widest type on offer and then let the server refuse what it had just promised.
        /// </summary>
        public string? RulesFrom { get; set; }

        /// <summary>
        /// The id of the form this box posts to, when that form cannot enclose it. HTML has no
        /// nested forms — a parser meeting a second <c>&lt;form&gt;</c> inside an open one drops the
        /// tag and quietly adopts its controls into the outer form — so a file box that lives inside
        /// a larger editing form must name its own form instead of being wrapped by it.
        /// Null (the default) leaves the attribute off and the box posts with whatever encloses it.
        /// </summary>
        public string? FormId { get; set; }
    }

    /// <summary>
    /// doc 10 §5 "Entity documents tab": everything filed against one record, what may still be
    /// filed, and the two things staff do to a stored document — sight it (verify) or retire it
    /// (void, BR-ATT-007). One partial for every owning screen, so a student file and an employee
    /// file do not grow two different vocabularies for the same act.
    /// </summary>
    public sealed class EntityDocumentsViewModel
    {
        /// <summary>The controller that owns these documents — its actions are named by convention (UploadDocument / DownloadDocument / VoidDocument / VerifyDocument).</summary>
        public string Controller { get; set; } = string.Empty;

        /// <summary>The owning record, as its own controller routes to it.</summary>
        public int OwnerId { get; set; }

        /// <summary>Names the person the files belong to, so a confirmation prompt can say whose document it is about.</summary>
        public string OwnerName { get; set; } = string.Empty;

        public IReadOnlyList<AttachmentIntake.DocumentRow> Rows { get; set; } = Array.Empty<AttachmentIntake.DocumentRow>();

        /// <summary>What may be filed here now — the active types for this module, restricted ones only for a reader who holds the category.</summary>
        public IReadOnlyList<DocumentType> Types { get; set; } = Array.Empty<DocumentType>();

        public bool CanEdit { get; set; }

        /// <summary>Sighting a document is a separate act from filing one; a clerk may upload without being the person who confirms it is genuine.</summary>
        public bool CanVerify { get; set; }
    }
}
