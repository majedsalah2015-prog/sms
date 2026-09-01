using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sms.Application.Notifications
{
    /// <summary>
    /// Which <c>{placeholders}</c> an event actually supplies, and whether a draft
    /// template only uses those.
    /// <para>
    /// doc/Modules/33 §9: "placeholders must resolve against the event payload
    /// (publish-blocked otherwise)". The reason is
    /// <see cref="TemplateRenderer"/>'s deliberate leniency — an unresolved token is
    /// left in the text rather than thrown on, because a malformed template must not
    /// take down the business transaction it rides. That is right at dispatch and
    /// wrong at authoring: without a check somewhere, a typo'd <c>{studentname}</c>
    /// reaches a parent as the literal word. This is that check, at the one moment it
    /// can still be fixed.
    /// </para>
    /// <para>
    /// <b>Keys are case-sensitive</b> because <see cref="TemplateRenderer"/>'s
    /// dictionary lookup is: <c>{StudentName}</c> and <c>{studentName}</c> are two
    /// different tokens at render time, so treating them as one here would bless a
    /// template that renders wrong.
    /// </para>
    /// </summary>
    public static class TemplatePlaceholderRules
    {
        private static readonly Regex Token = new(@"\{(\w+)\}", RegexOptions.Compiled);

        /// <summary>
        /// The keys each event's publisher actually puts in its payload — read off the
        /// <c>PublishAsync</c> call sites in <c>Sms.Infrastructure</c>, not off doc 09's
        /// prose. A key the publisher does not send is a key no template may use, however
        /// reasonable it sounds; a template naming <c>{StudentName}</c> on an event that
        /// only carries <c>{VisitNo}</c> would deliver the word to a parent.
        /// <para>
        /// <b>Case matters</b> and the publishers are PascalCase throughout, so the
        /// entries below are too. <c>{daysOverdue}</c> is not <c>{DaysOverdue}</c> and
        /// would render as itself.
        /// </para>
        /// <para>
        /// An event absent from this table is one no module publishes yet
        /// (<c>NotificationEventCatalog.NotificationEvent.HasPublisher</c> is false).
        /// Those are not validated — refusing every placeholder on an event that supplies
        /// none would block a school from writing content ahead of the module that will
        /// send it, which is a legitimate thing to do.
        /// </para>
        /// </summary>
        private static readonly IReadOnlyDictionary<string, string[]> ByEvent =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                // LibraryAdmin
                ["LibraryOverdue"] = new[] { "Barcode", "DaysOverdue" },
                ["LibraryReservationReady"] = new[] { "Barcode", "HoldUntil" },

                // InstallmentAdmin — one payload, two events, split on the dunning step
                ["InstallmentDueSoon"] = new[] { "InstallmentNo", "DueDate", "Amount", "Step" },
                ["InstallmentOverdue"] = new[] { "InstallmentNo", "DueDate", "Amount", "Step" },

                // CollectionFollowUp — a notice covers a window, not one installment, so it has no
                // InstallmentNo to offer. That is why it publishes under its own event code rather
                // than borrowing the ladder's: a template written for {InstallmentNo} would render
                // the word itself into a parent's inbox.
                ["DunningLetterIssued"] = new[] { "NoticeNo", "StudentNo", "Amount", "DueItems", "DueDate", "WindowFrom", "WindowTo" },

                // HealthAdmin
                ["ClinicStudentSentHome"] = new[] { "VisitNo", "Outcome" },
                ["SchoolEmergencyProtocol"] = new[] { "VisitNo", "Outcome" },
                ["MedicationAdministered"] = new[] { "Medication", "Status", "At" },
                ["HealthExposureNotice"] = new[] { "Disease", "From", "To" },

                // DisciplineAdmin
                ["DisciplineIncidentRecorded"] = new[] { "IncidentNo", "Severity" },
                ["DisciplineDecision"] = new[] { "Article", "Consequence" },

                // TransportAdmin
                ["TransportRouteChanged"] = new[] { "StudentId" },
                ["TransportSuspended"] = new[] { "EffectiveDate" },
                ["TransportStudentNotBoarded"] = new[] { "Date", "Urgent" },
            };

        /// <summary>Every placeholder the template body/subject names, in first-appearance order, without duplicates.</summary>
        public static IReadOnlyList<string> Used(params string?[] texts)
        {
            var seen = new List<string>();
            foreach (var text in texts)
            {
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                foreach (Match match in Token.Matches(text))
                {
                    var key = match.Groups[1].Value;
                    if (!seen.Contains(key, StringComparer.Ordinal))
                    {
                        seen.Add(key);
                    }
                }
            }

            return seen;
        }

        /// <summary>
        /// The keys a template for <paramref name="eventCode"/> may use — empty when the
        /// event's publisher does not exist yet, which <see cref="Unknown"/> reads as
        /// "do not validate" rather than "nothing is allowed". Also what the studio's
        /// placeholder picker offers, so a school inserts a token rather than typing one.
        /// </summary>
        public static IReadOnlyList<string> Available(string? eventCode)
            => eventCode != null && ByEvent.TryGetValue(eventCode, out var keys)
                ? keys
                : Array.Empty<string>();

        /// <summary>
        /// The placeholders in these texts that <paramref name="eventCode"/> will never
        /// supply. Empty when the template is sound — and empty, deliberately, for an
        /// event with no publisher yet: see <see cref="Available"/>.
        /// </summary>
        public static IReadOnlyList<string> Unknown(string? eventCode, params string?[] texts)
        {
            var available = Available(eventCode);
            if (available.Count == 0)
            {
                return Array.Empty<string>();
            }

            return Used(texts).Where(k => !available.Contains(k, StringComparer.Ordinal)).ToList();
        }
    }
}
