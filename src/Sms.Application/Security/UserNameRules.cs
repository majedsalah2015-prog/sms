using System;
using System.Linq;
using System.Text;

namespace Sms.Application.Security
{
    /// <summary>
    /// What a user name may be, and what one is proposed to be when an account is provisioned for a
    /// person (doc 06 §2, BR-SYS-001).
    /// <para>
    /// The name is what somebody types at a keyboard at 07:00 with a queue behind them, so the rules
    /// are about typing rather than about storage: one case, no spaces, and no character that has to
    /// be described over a telephone. Everything is folded to lower case rather than compared case
    /// -insensitively, because the two accounts <c>Ahmed</c> and <c>ahmed</c> looking identical on a
    /// list is worse than either name being unavailable.
    /// </para>
    /// <para>
    /// The proposal is built from the person's own reference number — employee, file or student
    /// number — and not from their name. Arabic names do not survive the fold at all, transliteration
    /// is not a decision this system should be making silently, and two brothers in the same school
    /// share far more of a name than they share of a student number. The proposal is a starting
    /// point regardless: the screen offers it in an editable field, because a school that already
    /// names its staff accounts some other way should not have to fight this one.
    /// </para>
    /// </summary>
    public static class UserNameRules
    {
        public const int MinLength = 3;

        public const int MaxLength = 64;

        /// <summary>Everything a name may contain past the first character. Deliberately narrow.</summary>
        private const string Allowed = "abcdefghijklmnopqrstuvwxyz0123456789._-@";

        /// <summary>
        /// The reader's form of a typed name: trimmed, lower-cased, inner runs of whitespace closed
        /// up to a single dot. Anything the name may not contain is dropped rather than rejected
        /// here — <see cref="IsWellFormed"/> is what refuses, and it refuses what is left.
        /// </summary>
        public static string Normalize(string? candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return string.Empty;
            }

            var lowered = candidate.Trim().ToLowerInvariant();
            var builder = new StringBuilder(lowered.Length);
            var pendingSeparator = false;

            foreach (var c in lowered)
            {
                if (char.IsWhiteSpace(c))
                {
                    pendingSeparator = builder.Length > 0;
                    continue;
                }

                if (!Allowed.Contains(c))
                {
                    continue;
                }

                if (pendingSeparator)
                {
                    builder.Append('.');
                    pendingSeparator = false;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Whether a already-<see cref="Normalize"/>d name may be used. The first character must be
        /// a letter or a digit: a name beginning with a dot or a hyphen reads as a typing accident
        /// on every list it appears in, and one beginning with '@' reads as an e-mail address.
        /// </summary>
        public static bool IsWellFormed(string? userName)
            => !string.IsNullOrEmpty(userName)
               && userName.Length >= MinLength
               && userName.Length <= MaxLength
               && userName.All(c => Allowed.Contains(c))
               && (char.IsLetterOrDigit(userName[0]));

        /// <summary>
        /// The name offered for a new account: the account type's prefix and the person's own
        /// reference number, e.g. <c>emp-1042</c>. The prefix is what keeps the three directories
        /// from colliding — an employee number and a student number are each unique only inside
        /// their own register, and a user name is unique across all of them.
        /// <para>
        /// Returns an empty string when the reference yields nothing typeable, rather than a bare
        /// prefix that every such person would collide on. The screen then asks for a name instead
        /// of offering one.
        /// </para>
        /// </summary>
        public static string Propose(ProvisionableAccountType accountType, string? reference)
        {
            var tail = Normalize(reference).Trim('.', '-', '_', '@');
            if (tail.Length == 0)
            {
                return string.Empty;
            }

            var prefix = Prefix(accountType);

            // A school whose employee numbers already read "EMP-00007" should not be offered
            // "emp-emp-00007". The prefix exists to keep the three registers apart, and a reference
            // that already carries it is already doing that — but only when what follows is the
            // number itself, so a real name beginning with those letters survives untouched.
            if (tail.StartsWith(prefix, StringComparison.Ordinal))
            {
                var rest = tail.Substring(prefix.Length).TrimStart('.', '-', '_');
                if (rest.Length > 0 && rest.All(char.IsDigit))
                {
                    tail = rest;
                }
            }

            var name = prefix + "-" + tail;
            return name.Length <= MaxLength ? name : name.Substring(0, MaxLength);
        }

        private static string Prefix(ProvisionableAccountType accountType) => accountType switch
        {
            ProvisionableAccountType.Staff => "emp",
            ProvisionableAccountType.Parent => "par",
            ProvisionableAccountType.Student => "stu",
            _ => throw new ArgumentOutOfRangeException(nameof(accountType), accountType, null),
        };
    }
}
