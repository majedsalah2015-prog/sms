namespace Sms.Application.Security
{
    /// <summary>
    /// BR-SEC-001 policy shape. Per-school configurability is future work
    /// (doc 06 §3 note) — pends the settings framework (E-010); v1 enforces
    /// the product minimums everywhere.
    /// </summary>
    public sealed class PasswordPolicy
    {
        public static readonly PasswordPolicy ProductMinimum = new();

        public int MinLength { get; init; } = 10;

        public bool RequireUpper { get; init; } = true;

        public bool RequireLower { get; init; } = true;

        public bool RequireDigit { get; init; } = true;

        public bool RequireSymbol { get; init; } = true;

        /// <summary>How many previous passwords (this one included, once set) block reuse.</summary>
        public int HistoryCount { get; init; } = 5;
    }
}
