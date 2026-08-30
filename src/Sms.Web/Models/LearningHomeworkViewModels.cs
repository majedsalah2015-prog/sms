using System;
using System.Collections.Generic;
using Sms.Domain.Learning;

namespace Sms.Web.Models
{
    /// <summary>doc/Modules/37 §8.3 — one (offering, section) pair the signed-in user may set work for (BR-LRN-002).</summary>
    public sealed record SectionOption(int OfferingId, int SectionId, string Label)
    {
        /// <summary>The picker's value carries both halves because reach is measured in the pair, not in either alone.</summary>
        public string Key => $"{OfferingId}:{SectionId}";
    }

    /// <summary>doc/Modules/37 §8.3 — a Module 17 component a graded homework may feed (BR-LRN-004/012).</summary>
    public sealed record ComponentOption(int Id, string Label, decimal MaxScore);

    public sealed record HomeworkRow(
        Homework Homework,
        string Title,
        bool IsOverdue);

    /// <summary>
    /// doc/Modules/37 §8.3, pattern P-LIST: pick the class, see what is set for
    /// it in due-date order, set more.
    /// </summary>
    public sealed class HomeworkDeskViewModel
    {
        public IReadOnlyList<SectionOption> Sections { get; set; } = Array.Empty<SectionOption>();

        public int? SelectedOfferingId { get; set; }

        public int? SelectedSectionId { get; set; }

        public string? SelectedKey =>
            SelectedOfferingId is int o && SelectedSectionId is int s ? $"{o}:{s}" : null;

        /// <summary>BR-LRN-004: only the components of the selected offering's blueprint — a homework cannot feed another subject's component.</summary>
        public IReadOnlyList<ComponentOption> Components { get; set; } = Array.Empty<ComponentOption>();

        public IReadOnlyList<HomeworkRow> Rows { get; set; } = Array.Empty<HomeworkRow>();

        /// <summary>
        /// True when the signed-in user holds no placement and heads no
        /// department. The screen then explains BR-LRN-002 rather than showing an
        /// empty dropdown that looks broken.
        /// </summary>
        public bool HasNoReach => Sections.Count == 0;

        /// <summary>The default due date the form offers — tomorrow, which is nearly always what a teacher means.</summary>
        public DateTime DefaultDueDate { get; set; }
    }
}
