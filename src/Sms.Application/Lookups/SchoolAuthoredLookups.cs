using System;
using System.Collections.Generic;
using Sms.Domain.Lookups;

namespace Sms.Application.Lookups
{
    /// <summary>
    /// Which product-tier lookup categories a school is nonetheless allowed to author
    /// (doc/Modules/01 §8.2, BR-SET-001).
    /// <para>
    /// BR-SET-001 splits lookups two ways and names the product-tier examples it means:
    /// "nationalities, ISO currencies, blood types, ID types, relationship types". The tier says
    /// <b>the product ships the values</b> — it does not say the school may never have any. Several
    /// categories carry the product tier for a different reason: they are shared across modules, so
    /// the taxonomy is product-defined, while the values themselves are a local list nobody but the
    /// school can write. Specialization, University and Bank ship deliberately <b>empty</b> for
    /// exactly that reason — those three are declared by the staff-catalogue work and are named
    /// here ahead of it, because an allowlist keyed by code costs nothing for a category that does
    /// not exist yet and is the one place a reader will look for the answer when it does.
    /// </para>
    /// <para>
    /// The list exists because the product had already made this exception three times, in three
    /// places that disagreed: <c>SetupController.Nationalities</c> is a dedicated editor over a
    /// product-tier list, <c>EmployeesController.Reference</c> is another over four more, and the
    /// lookup screen's quick-add carried its own two-code allowlist. Meanwhile the generic
    /// <c>/setup/lookups</c> grid refused all of them on tier alone — so a school opening
    /// Specialization found an empty table it could neither use nor fill, while the same list was
    /// editable one screen away. One list, consulted by every path, is what makes the answer the
    /// same wherever it is asked.
    /// </para>
    /// <para>
    /// What is <b>not</b> here is the point of it: Currency (the wizard and
    /// <c>School.CurrencyCode</c> validate against the ISO set, BR-GLB-112), BloodType, IdType,
    /// RelationshipType, AcademicGrade, RoomType, RoomFeature and Curriculum stay product-owned.
    /// Adding to this set is a product decision, not a screen's.
    /// </para>
    /// </summary>
    public static class SchoolAuthoredLookups
    {
        /// <summary>
        /// The category codes, each with the reason it is here:
        /// <list type="bullet">
        /// <item>Nationality — the school's own catchment decides it; already editable on Setup → Nationalities.</item>
        /// <item>JobTitle — every school's org chart is its own; already quick-addable from the employee form.</item>
        /// <item>EducationLevel — what counts as a qualification is local ("Tawjihi" in one country, "Secondary" in the next).</item>
        /// <item>University — ships empty on purpose; the awarding bodies a school recognises are a local list.</item>
        /// <item>Specialization — ships empty on purpose; a school's specializations follow its own subjects.</item>
        /// <item>Bank — ships empty on purpose; the banks salaries are paid into are local.</item>
        /// </list>
        /// </summary>
        private static readonly HashSet<string> Authored = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Nationality",
            "JobTitle",
            "EducationLevel",
            "University",
            "Specialization",
            "Bank",
        };

        /// <summary>The codes, for a screen that wants to explain the set rather than test one member of it.</summary>
        public static IReadOnlyCollection<string> All => Authored;

        /// <summary>
        /// True when <paramref name="categoryCode"/> is a product-tier list a school may author.
        /// Case-insensitive: the code arrives from a query string as often as from the database.
        /// </summary>
        public static bool Includes(string? categoryCode)
            => !string.IsNullOrWhiteSpace(categoryCode) && Authored.Contains(categoryCode!.Trim());

        /// <summary>
        /// Whether a school may add, rename, reorder, deactivate and reactivate the values of
        /// <paramref name="category"/> — the single question every lookup write path asks.
        /// <para>
        /// A null category is not editable rather than throwing: the caller's next line is a
        /// translated "no such list" refusal, which is a better answer than an exception.
        /// </para>
        /// </summary>
        public static bool IsEditableBySchool(LookupCategory? category)
            => category != null
                && (category.Tier == LookupCategoryTier.SchoolManaged || Includes(category.Code));
    }
}
