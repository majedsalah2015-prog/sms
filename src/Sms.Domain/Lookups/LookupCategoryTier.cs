namespace Sms.Domain.Lookups
{
    /// <summary>BR-SET-001 two-tier lookup model.</summary>
    public enum LookupCategoryTier : short
    {
        /// <summary>Updatable by product releases; not editable by schools (e.g. nationalities, ISO currencies).</summary>
        ProductSeeded = 1,

        /// <summary>School-managed (e.g. housing types, referral sources, custom tags).</summary>
        SchoolManaged = 2,
    }
}
