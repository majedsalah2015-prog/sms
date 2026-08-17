using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Timetable
{
    /// <summary>
    /// core.Session (doc/Modules/15 §7, BR-TTB-006/009): a dated instance
    /// generated from a Placement x working day. Placement edits never
    /// mutate past sessions (snapshot semantics, same philosophy as
    /// BR-SCN-005) — a room/teacher change on a Session is recorded here,
    /// not by rewriting the Placement. The FK target for Module 14's
    /// period-mode attendance (not wired — that mode is still deferred
    /// from E-301, this just gives it the row shape it will need).
    /// </summary>
    [Audited(AuditTier.T2)]
    public class Session : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int PlacementId { get; set; }

        public DateTime Date { get; set; }

        public SessionStatus Status { get; set; } = SessionStatus.Held;

        /// <summary>BR-TTB-008: dated, temporary room override — the Placement's own RoomId is unchanged.</summary>
        public int? OverrideRoomId { get; set; }

        public string? ChangeReason { get; set; }
    }
}
