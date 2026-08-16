using System;
using Sms.Domain.Audit;
using Sms.Domain.Common;

namespace Sms.Domain.Classrooms
{
    /// <summary>core.RoomBooking (doc/Modules/08 §2/4): light event-use booking — "keep the light version" per doc §14 Q2.</summary>
    [Audited(AuditTier.T2)]
    public class RoomBooking : AuditableEntity, ISchoolScoped, IYearScoped
    {
        public int SchoolId { get; set; }

        public int AcademicYearId { get; set; }

        public int RoomId { get; set; }

        public string Purpose { get; set; } = string.Empty;

        public DateTime StartUtc { get; set; }

        public DateTime EndUtc { get; set; }

        public int RequestedByUserId { get; set; }

        public RoomBookingStatus Status { get; set; } = RoomBookingStatus.Requested;
    }
}
