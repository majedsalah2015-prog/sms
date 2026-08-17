using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Domain.Transport;

namespace Sms.Application.Transport
{
    /// <summary>Pure BR-TRN-001: a bus is roadworthy on a date only if every mandatory document kind is present and unexpired (latest row per kind counts).</summary>
    public static class RoadworthinessEvaluator
    {
        public static readonly IReadOnlyCollection<BusDocumentKind> MandatoryKinds = new[]
        {
            BusDocumentKind.Registration, BusDocumentKind.Insurance, BusDocumentKind.SafetyInspection,
        };

        public sealed record DocumentInput(BusDocumentKind Kind, DateTime ExpiryDate);

        /// <summary>Returns the kinds that block (missing or expired); empty = roadworthy.</summary>
        public static IReadOnlyList<BusDocumentKind> Blockers(IReadOnlyCollection<DocumentInput> documents, DateTime onDate)
        {
            var latestByKind = documents.GroupBy(d => d.Kind).ToDictionary(g => g.Key, g => g.Max(d => d.ExpiryDate));
            return MandatoryKinds
                .Where(k => !latestByKind.TryGetValue(k, out var expiry) || expiry.Date < onDate.Date)
                .ToList();
        }

        public static bool IsRoadworthy(IReadOnlyCollection<DocumentInput> documents, DateTime onDate) => Blockers(documents, onDate).Count == 0;
    }

    /// <summary>Pure BR-TRN-002: a driver needs a licence of at least the bus's required class, unexpired on the trip date.</summary>
    public static class DriverEligibilityEvaluator
    {
        public static bool CanDrive(LicenseClass? driverClass, DateTime? licenseExpiryDate, LicenseClass requiredClass, DateTime onDate)
        {
            if (driverClass == null || licenseExpiryDate == null)
            {
                return false;
            }

            return driverClass.Value >= requiredClass && licenseExpiryDate.Value.Date >= onDate.Date;
        }
    }

    /// <summary>Pure BR-TRN-003: route student count ≤ bus capacity — hard; overflow is waitlisted.</summary>
    public static class RouteCapacityEvaluator
    {
        public static bool HasSeat(int activeSubscriptions, int busCapacity) => activeSubscriptions < busCapacity;
    }

    /// <summary>Pure BR-TRN-003 / doc §9: stop times must be strictly sequential along the route.</summary>
    public static class StopSequenceValidator
    {
        public static bool AreSequential(IReadOnlyList<TimeSpan> orderedTimes)
        {
            for (var i = 1; i < orderedTimes.Count; i++)
            {
                if (orderedTimes[i] <= orderedTimes[i - 1])
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Pure BR-TRN-005 close rule: every roster student must be resolved
    /// (AM: alighted, absent-declared or not-boarded-escalated; PM:
    /// alighted, absent-declared or not-collected-escalated) and the
    /// "bus empty" sweep confirmed. A student who boarded but never
    /// alighted is unresolved — that is exactly the child-left-on-bus
    /// case the sweep exists for.
    /// </summary>
    public static class TripCloseEvaluator
    {
        public sealed record StudentEvents(int StudentId, IReadOnlyCollection<TripLogEvent> Events);

        public static IReadOnlyList<int> UnresolvedStudents(IReadOnlyCollection<StudentEvents> roster)
            => roster.Where(s => !IsResolved(s.Events)).Select(s => s.StudentId).ToList();

        public static bool IsResolved(IReadOnlyCollection<TripLogEvent> events)
            => events.Contains(TripLogEvent.Alighted)
               || events.Contains(TripLogEvent.AbsentDeclared)
               || events.Contains(TripLogEvent.NotBoarded)
               || events.Contains(TripLogEvent.NotCollected);

        public static bool CanClose(IReadOnlyCollection<StudentEvents> roster, bool sweepConfirmed)
            => sweepConfirmed && UnresolvedStudents(roster).Count == 0;
    }

    /// <summary>Pure BR-TRN-006: PM handover — required per stage policy; a self-release consent (secondary) satisfies it; otherwise the receiver must be a pickup-authorized person.</summary>
    public static class HandoverPolicy
    {
        public static bool IsAcceptable(bool handoverRequired, bool selfReleaseAllowed, bool receiverIsPickupAuthorized)
            => !handoverRequired || selfReleaseAllowed || receiverIsPickupAuthorized;
    }
}
