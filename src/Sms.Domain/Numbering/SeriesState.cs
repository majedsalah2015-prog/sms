using Sms.Domain.Common;

namespace Sms.Domain.Numbering
{
    /// <summary>
    /// core.SeriesState (doc 08/Database 04 §3): one row per series version
    /// per reset scope ("ALL" for Never, the academic year or calendar year
    /// label otherwise). <see cref="LastIssuedSequence"/> is an EF Core
    /// concurrency token (BR-NUM-003) — two racing issuers both bump it in
    /// memory, but only the SaveChanges that still sees the original value
    /// commits; the loser's whole transaction (including its business row)
    /// fails atomically and never burns a number. Not
    /// <see cref="Audit.AuditedAttribute"/>-tagged: it changes on every
    /// single issuance, which would flood the audit store for no security
    /// value (same reasoning as UserSession/UserAccount in E-003).
    /// </summary>
    public class SeriesState : AuditableEntity, ISchoolScoped
    {
        public int SchoolId { get; set; }

        public int NumberingSeriesId { get; set; }

        public string ResetKey { get; set; } = string.Empty;

        public int LastIssuedSequence { get; set; }
    }
}
