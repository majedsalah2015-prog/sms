using System;
using System.Linq;
using System.Collections.Generic;
using Sms.Domain.Transport;

namespace Sms.Web.Models
{
    /// <summary>
    /// The transport enums as a person reads them. Arabic only — the English is the enum name, which
    /// is what the module doc, the permissions and a support answer all use, so translating it would
    /// give the same thing two names.
    /// </summary>
    public static class TransportLabels
    {
        public static string BusType(Sms.Domain.Transport.BusType t, bool ar) => !ar ? t.ToString() : t switch
        {
            Sms.Domain.Transport.BusType.Minibus => "حافلة صغيرة",
            Sms.Domain.Transport.BusType.Standard => "حافلة عادية",
            Sms.Domain.Transport.BusType.Large => "حافلة كبيرة",
            _ => t.ToString(),
        };

        public static string LicenseClass(Sms.Domain.Transport.LicenseClass c, bool ar) => !ar ? c.ToString() : c switch
        {
            Sms.Domain.Transport.LicenseClass.Light => "خفيفة",
            Sms.Domain.Transport.LicenseClass.Medium => "متوسطة",
            Sms.Domain.Transport.LicenseClass.Heavy => "ثقيلة",
            _ => c.ToString(),
        };

        public static string DocumentKind(BusDocumentKind k, bool ar) => !ar ? SpaceOut(k.ToString()) : k switch
        {
            BusDocumentKind.Registration => "رخصة السير",
            BusDocumentKind.Insurance => "التأمين",
            BusDocumentKind.SafetyInspection => "الفحص الفني",
            _ => k.ToString(),
        };

        public static string StaffKind(TransportStaffKind k, bool ar) => !ar ? k.ToString() : k switch
        {
            TransportStaffKind.Driver => "سائق",
            TransportStaffKind.Attendant => "مرافق",
            _ => k.ToString(),
        };

        public static string Direction(RouteDirection d, bool ar) => !ar
            ? (d == RouteDirection.Am ? "AM pickup" : "PM drop")
            : (d == RouteDirection.Am ? "صباحي (ذهاب)" : "مسائي (عودة)");

        public static string SubscriptionStatus(TransportSubscriptionStatus s, bool ar) => !ar ? s.ToString() : s switch
        {
            TransportSubscriptionStatus.Active => "نشط",
            TransportSubscriptionStatus.Ended => "منتهٍ",
            TransportSubscriptionStatus.Suspended => "موقوف",
            TransportSubscriptionStatus.Waitlisted => "قائمة انتظار",
            _ => s.ToString(),
        };

        public static string TripStatus(Sms.Domain.Transport.TripStatus s, bool ar) => !ar ? s.ToString() : s switch
        {
            Sms.Domain.Transport.TripStatus.InProgress => "جارية",
            Sms.Domain.Transport.TripStatus.Closed => "مغلقة",
            Sms.Domain.Transport.TripStatus.Escalated => "مُصعَّدة",
            _ => s.ToString(),
        };

        public static string TripEvent(TripLogEvent e, bool ar) => !ar ? SpaceOut(e.ToString()) : e switch
        {
            TripLogEvent.Boarded => "صعد",
            TripLogEvent.Alighted => "نزل",
            TripLogEvent.AbsentDeclared => "غائب",
            TripLogEvent.NotBoarded => "لم يصعد",
            TripLogEvent.NotCollected => "لم يُستلَم",
            _ => e.ToString(),
        };

        public static string SafetyKind(SafetyEventKind k, bool ar) => !ar ? SpaceOut(k.ToString()) : k switch
        {
            SafetyEventKind.NotBoardedAm => "لم يصعد صباحاً",
            SafetyEventKind.NotCollectedPm => "لم يُستلَم مساءً",
            SafetyEventKind.UnclosedTrip => "رحلة لم تُغلق",
            SafetyEventKind.UnauthorizedHandover => "تسليم غير مُصرَّح",
            SafetyEventKind.UnroadworthyOverride => "تجاوز عدم الصلاحية",
            _ => k.ToString(),
        };

        public static string SafetyState(SafetyEventState s, bool ar) => !ar ? s.ToString() : s switch
        {
            SafetyEventState.Open => "مفتوح",
            SafetyEventState.Escalated => "مُصعَّد",
            SafetyEventState.Resolved => "مُعالَج",
            _ => s.ToString(),
        };

        /// <summary>"NotBoardedAm" → "Not boarded am". The enum names are PascalCase; a screen is not.</summary>
        private static string SpaceOut(string pascal) =>
            string.Concat(pascal.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + char.ToLowerInvariant(c) : c.ToString()));
    }

    /// <summary>A student as the transport screens print one: number first, because that is what a driver's sheet carries.</summary>
    public sealed record StudentLabel(string StudentNo, string NameAr, string NameEn)
    {
        public string Name(bool arabic) => arabic ? NameAr : NameEn;
    }

    /// <summary>One stop, carrying enough of its route to be picked out of a flat list.</summary>
    public sealed record StopLabel(
        int Id, int RouteId, string RouteNo, RouteDirection Direction,
        int SequenceNumber, string NameAr, string NameEn, TimeSpan ScheduledTime)
    {
        public string Name(bool arabic) => arabic ? NameAr : NameEn;
    }

    public sealed record FeeCategoryLabel(int Id, string NameAr, string NameEn)
    {
        public string Name(bool arabic) => arabic ? NameAr : NameEn;
    }

    /// <summary>
    /// One bus and what stops it going out. <see cref="Blockers"/> is derived from the documents on
    /// the day rather than stored — an expired insurance certificate is a fact about a date, and a
    /// stored "unroadworthy" flag would be wrong from the moment it was written.
    /// </summary>
    public sealed class BusRowViewModel
    {
        public Bus Bus { get; set; } = null!;

        /// <summary>Latest expiry per document kind. A missing kind is itself a blocker.</summary>
        public IReadOnlyDictionary<BusDocumentKind, DateTime> Documents { get; set; } =
            new Dictionary<BusDocumentKind, DateTime>();

        public IReadOnlyList<BusDocumentKind> Blockers { get; set; } = new List<BusDocumentKind>();

        public int RouteCount { get; set; }

        public bool IsRoadworthy => Blockers.Count == 0;
    }

    public sealed class FleetViewModel
    {
        public IReadOnlyList<BusRowViewModel> Buses { get; set; } = new List<BusRowViewModel>();

        public DateTime Today { get; set; }
    }

    public sealed class TransportStaffViewModel
    {
        public IReadOnlyList<TransportStaff> Staff { get; set; } = new List<TransportStaff>();

        public DateTime Today { get; set; }

        public IReadOnlyDictionary<int, int> RouteCountByStaffId { get; set; } = new Dictionary<int, int>();
    }

    public sealed class RoutesViewModel
    {
        public IReadOnlyList<Route> Routes { get; set; } = new List<Route>();

        public IReadOnlyList<Bus> Buses { get; set; } = new List<Bus>();

        public IReadOnlyList<TransportStaff> Drivers { get; set; } = new List<TransportStaff>();

        public IReadOnlyList<TransportStaff> Attendants { get; set; } = new List<TransportStaff>();

        /// <summary>Active riders per route id — the numerator of the capacity meter.</summary>
        public IReadOnlyDictionary<int, int> Riders { get; set; } = new Dictionary<int, int>();

        public IReadOnlyList<FeeCategoryLabel> FeeCategories { get; set; } = new List<FeeCategoryLabel>();
    }

    public sealed class RouteDetailViewModel
    {
        public Route Route { get; set; } = null!;

        public Bus? Bus { get; set; }

        public TransportStaff? Driver { get; set; }

        public TransportStaff? Attendant { get; set; }

        public int RiderCount { get; set; }

        public IReadOnlyList<FeeCategoryLabel> FeeCategories { get; set; } = new List<FeeCategoryLabel>();

        // The pickers for reassigning the standing crew, on the screen that shows what it currently is.
        public IReadOnlyList<Bus> Buses { get; set; } = new List<Bus>();

        public IReadOnlyList<TransportStaff> Drivers { get; set; } = new List<TransportStaff>();

        public IReadOnlyList<TransportStaff> Attendants { get; set; } = new List<TransportStaff>();
    }

    public sealed class SubscriptionsViewModel
    {
        public IReadOnlyList<TransportSubscription> Subscriptions { get; set; } = new List<TransportSubscription>();

        public IReadOnlyDictionary<int, StudentLabel> Students { get; set; } = new Dictionary<int, StudentLabel>();

        public IReadOnlyList<StopLabel> Stops { get; set; } = new List<StopLabel>();

        public TransportSubscriptionStatus? Status { get; set; }

        public string? Search { get; set; }
    }

    public sealed class TripsViewModel
    {
        public DateTime Date { get; set; }

        public IReadOnlyList<Trip> Trips { get; set; } = new List<Trip>();

        public IReadOnlyList<Route> Routes { get; set; } = new List<Route>();

        /// <summary>Routes with no trip on this date — the "not yet opened" half of the morning.</summary>
        public IReadOnlyList<Route> RoutesWithoutATrip { get; set; } = new List<Route>();

        public IReadOnlyList<TransportStaff> Drivers { get; set; } = new List<TransportStaff>();

        public IReadOnlyList<TransportStaff> Attendants { get; set; } = new List<TransportStaff>();

        /// <summary>How many roster students are resolved per trip — what stands between it and closing.</summary>
        public IReadOnlyDictionary<int, int> ResolvedByTrip { get; set; } = new Dictionary<int, int>();
    }

    /// <summary>One child on today's bus, and everything that has happened to them on it.</summary>
    public sealed class TripRosterRow
    {
        public int StudentId { get; set; }

        public StudentLabel Label { get; set; } = null!;

        public IReadOnlyList<TripLogEvent> Events { get; set; } = new List<TripLogEvent>();

        public bool IsSelfReleaseAllowed { get; set; }

        public bool HasBoarded => System.Linq.Enumerable.Contains(Events, TripLogEvent.Boarded);
    }

    public sealed class TripDetailViewModel
    {
        public Trip Trip { get; set; } = null!;

        public Route? Route { get; set; }

        public IReadOnlyList<TripRosterRow> Roster { get; set; } = new List<TripRosterRow>();

        /// <summary>Roster students with no terminal event yet. A trip cannot close while this is above zero.</summary>
        public int Unresolved { get; set; }
    }

    public sealed class SafetyViewModel
    {
        public IReadOnlyList<SafetyEvent> Events { get; set; } = new List<SafetyEvent>();

        public IReadOnlyDictionary<int, StudentLabel> Students { get; set; } = new Dictionary<int, StudentLabel>();

        public SafetyEventState? State { get; set; }
    }
}
