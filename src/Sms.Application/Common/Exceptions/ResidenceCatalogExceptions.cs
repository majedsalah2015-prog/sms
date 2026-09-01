using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Which level of the residence hierarchy a refusal is about — محافظة → منطقة → حي.
    /// <para>
    /// Carried as a value so the boundary can name the level in the reader's own language. The
    /// three levels are one table each and one screen between them, and a refusal that said
    /// "row already exists" without saying which of the three was being edited would be read
    /// against whichever list the operator happened to be looking at.
    /// </para>
    /// </summary>
    public enum ResidenceLevel
    {
        /// <summary>محافظة — the outermost level.</summary>
        Governorate = 1,

        /// <summary>منطقة — an area within a governorate.</summary>
        Locality = 2,

        /// <summary>حي — a quarter within a locality.</summary>
        Quarter = 3,
    }

    /// <summary>
    /// The code offered for a new residence row is already taken at that level.
    /// <para>
    /// Codes are unique per school for a governorate, per governorate for a locality and per
    /// locality for a quarter — see <c>GeographyConfigurations</c>. Without this the insert died
    /// on the unique index and reached the operator as a raw <c>DbUpdateException</c>, which does
    /// not say which of the two rows was the duplicate, let alone in Arabic.
    /// </para>
    /// </summary>
    public sealed class DuplicateResidenceCodeException : InvalidOperationException
    {
        public DuplicateResidenceCodeException(ResidenceLevel level, string code)
            : base($"A {level} with code {code} already exists at that level.")
        {
            Level = level;
            Code = code;
        }

        public ResidenceLevel Level { get; }

        public string Code { get; }
    }

    /// <summary>
    /// The row an edit or a deactivation names is not there — a stale page, or a level whose
    /// parent was edited in another tab while this one was open.
    /// </summary>
    public sealed class ResidenceRowNotFoundException : InvalidOperationException
    {
        public ResidenceRowNotFoundException(ResidenceLevel level, int id)
            : base($"No {level} with id {id} in this school.")
        {
            Level = level;
            Id = id;
        }

        public ResidenceLevel Level { get; }

        public int Id { get; }
    }
}
