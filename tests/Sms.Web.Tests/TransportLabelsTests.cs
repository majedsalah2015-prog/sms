using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Security;
using Sms.Domain.Security;
using Sms.Domain.Transport;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The transport screens print eight enums, and every one of them falls back to the enum name
    /// when a value has no label. That fallback is deliberate — a missing translation should show
    /// something rather than nothing — but it means an enum member added later would quietly start
    /// printing "NotCollectedPm" to an Arabic user and nothing would fail.
    /// <para>
    /// These tests are that failure. They assert the Arabic side is a real translation for every
    /// value the enum defines, so adding one without labelling it is a red build.
    /// </para>
    /// </summary>
    public class TransportLabelsTests
    {
        public static IEnumerable<object[]> ArabicLabels() => new[]
        {
            Case<BusType>(v => TransportLabels.BusType(v, true)),
            Case<LicenseClass>(v => TransportLabels.LicenseClass(v, true)),
            Case<BusDocumentKind>(v => TransportLabels.DocumentKind(v, true)),
            Case<TransportStaffKind>(v => TransportLabels.StaffKind(v, true)),
            Case<RouteDirection>(v => TransportLabels.Direction(v, true)),
            Case<TransportSubscriptionStatus>(v => TransportLabels.SubscriptionStatus(v, true)),
            Case<TripStatus>(v => TransportLabels.TripStatus(v, true)),
            Case<TripLogEvent>(v => TransportLabels.TripEvent(v, true)),
            Case<SafetyEventKind>(v => TransportLabels.SafetyKind(v, true)),
            Case<SafetyEventState>(v => TransportLabels.SafetyState(v, true)),
        }.SelectMany(x => x);

        private static IEnumerable<object[]> Case<TEnum>(Func<TEnum, string> label) where TEnum : struct, Enum =>
            Enum.GetValues(typeof(TEnum)).Cast<TEnum>()
                .Select(v => new object[] { typeof(TEnum).Name, v.ToString(), label(v) });

        [Theory]
        [MemberData(nameof(ArabicLabels))]
        public void Every_enum_value_has_an_Arabic_label(string enumName, string value, string arabic)
        {
            Assert.False(string.IsNullOrWhiteSpace(arabic), $"{enumName}.{value} has no Arabic label.");

            // The fallback returns the enum name itself, so a label equal to it is a value nobody
            // translated. Latin letters anywhere are the same signal.
            Assert.NotEqual(value, arabic);
            Assert.DoesNotContain(arabic, c => c is >= 'A' and <= 'Z');
        }

        /// <summary>
        /// The English side is deliberately the enum name, spaced out. It has to stay recognisable as
        /// the name the module doc, the permission catalogue and a support answer all use.
        /// </summary>
        [Theory]
        [InlineData(TripLogEvent.NotBoarded, "Not boarded")]
        [InlineData(TripLogEvent.Boarded, "Boarded")]
        public void The_English_label_is_the_enum_name_made_readable(TripLogEvent value, string expected)
        {
            Assert.Equal(expected, TransportLabels.TripEvent(value, false));
        }

        /// <summary>
        /// The six transport screens exist in the catalogue with the verbs the controller's guards
        /// name. Opening a trip is Post, not Create — it runs an engine that builds a roster and can
        /// refuse — and both the Principal decisions are Approve.
        /// </summary>
        [Theory]
        [InlineData(ScreenCatalog.Transport.Fleet, ActionVerb.View)]
        [InlineData(ScreenCatalog.Transport.Fleet, ActionVerb.Create)]
        [InlineData(ScreenCatalog.Transport.Staff, ActionVerb.View)]
        [InlineData(ScreenCatalog.Transport.Routes, ActionVerb.Create)]
        [InlineData(ScreenCatalog.Transport.Subscriptions, ActionVerb.Approve)]
        [InlineData(ScreenCatalog.Transport.Trips, ActionVerb.Post)]
        [InlineData(ScreenCatalog.Transport.Trips, ActionVerb.Approve)]
        [InlineData(ScreenCatalog.Transport.Safety, ActionVerb.Approve)]
        public void The_transport_screens_are_catalogued_with_the_verbs_their_guards_name(
            string screenCode, ActionVerb verb)
        {
            Assert.True(
                ScreenCatalog.Defines(ScreenCatalog.Modules.Transport, screenCode, verb),
                $"TRN/{screenCode}/{verb} is not in the screen catalogue.");
        }

        /// <summary>
        /// Opening a trip must not be reachable with a Create grant. Designing routes and running the
        /// morning are different jobs, and the whole reason the verbs differ is so they can be given
        /// to different people.
        /// </summary>
        [Fact]
        public void Opening_a_trip_is_not_a_Create()
        {
            Assert.False(ScreenCatalog.Defines(
                ScreenCatalog.Modules.Transport, ScreenCatalog.Transport.Trips, ActionVerb.Create));
        }
    }
}
