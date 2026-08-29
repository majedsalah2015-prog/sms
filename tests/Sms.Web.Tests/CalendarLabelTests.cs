using System;
using System.Linq;
using Sms.Domain.Calendar;
using Sms.TestSupport;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Day types and event categories are named to the reader, never printed as enum values.
    /// <para>
    /// The board had its own private name table in the view while the paint confirmation
    /// interpolated the enum straight into the flash, so an Arabic registrar who painted an exam
    /// week read «تم تعيين ٧ أيام كـ ExamPeriodWorking» under a legend that said فترة اختبارات.
    /// Both now read from <see cref="CalendarLabels"/>, and these tests hold every value of both
    /// enums to a real translation rather than only the ones a screen happens to use today —
    /// a new <c>DayType</c> would otherwise leak its name the first time someone painted it.
    /// </para>
    /// </summary>
    public class CalendarLabelTests
    {
        public static TheoryData<DayType> DayTypes()
        {
            var data = new TheoryData<DayType>();
            foreach (DayType value in Enum.GetValues(typeof(DayType)))
            {
                data.Add(value);
            }

            return data;
        }

        public static TheoryData<CalendarEventCategory> Categories()
        {
            var data = new TheoryData<CalendarEventCategory>();
            foreach (CalendarEventCategory value in Enum.GetValues(typeof(CalendarEventCategory)))
            {
                data.Add(value);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(DayTypes))]
        [BusinessRule("BR-CAL-001")]
        public void Every_day_type_is_named_in_both_languages(DayType dayType)
        {
            var ar = CalendarLabels.DayType(dayType, isRtl: true);
            var en = CalendarLabels.DayType(dayType, isRtl: false);

            Assert.False(string.IsNullOrWhiteSpace(ar));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(dayType.ToString(), ar);
            Assert.NotEqual(ar, en);
        }

        [Theory]
        [MemberData(nameof(Categories))]
        [BusinessRule("BR-CAL-002")]
        public void Every_event_category_is_named_in_both_languages(CalendarEventCategory category)
        {
            var ar = CalendarLabels.Category(category, isRtl: true);
            var en = CalendarLabels.Category(category, isRtl: false);

            Assert.False(string.IsNullOrWhiteSpace(ar));
            Assert.False(string.IsNullOrWhiteSpace(en));
            Assert.NotEqual(category.ToString(), ar);
            Assert.NotEqual(ar, en);
        }

        [Theory]
        [MemberData(nameof(DayTypes))]
        [BusinessRule("BR-CAL-001")]
        public void Day_type_names_do_not_collide(DayType dayType)
        {
            // Two types sharing a name is worse than an untranslated one: the legend and the
            // confirmation would agree, and both would be wrong about which days were painted.
            var others = Enum.GetValues(typeof(DayType)).Cast<DayType>().Where(t => t != dayType);
            foreach (var other in others)
            {
                Assert.NotEqual(CalendarLabels.DayType(other, isRtl: true), CalendarLabels.DayType(dayType, isRtl: true));
                Assert.NotEqual(CalendarLabels.DayType(other, isRtl: false), CalendarLabels.DayType(dayType, isRtl: false));
            }
        }
    }

    /// <summary>
    /// BR-CAL-006 put the ministry minimum in the settings hub rather than in shipped reference
    /// data (doc/Modules/04 §14 Q1 leaves the per-country values open), and the hub renders every
    /// key generically — so a key added without a label reaches an Arabic screen as
    /// "Regional.MinimumInstructionalDays" and no sentence saying what it does.
    /// </summary>
    public class SettingLabelCoverageTests
    {
        public static TheoryData<string> Keys()
        {
            var data = new TheoryData<string>();
            foreach (var definition in Sms.Application.Setup.SettingKeys.All)
            {
                data.Add(definition.Key);
            }

            return data;
        }

        [Theory]
        [MemberData(nameof(Keys))]
        public void Every_setting_key_is_named_and_explained_in_both_languages(string key)
        {
            foreach (var arabic in new[] { true, false })
            {
                var name = SettingLabels.Name(key, arabic);
                Assert.False(string.IsNullOrWhiteSpace(name));
                Assert.NotEqual(key, name);
                Assert.False(string.IsNullOrWhiteSpace(SettingLabels.Hint(key, arabic)));
            }

            Assert.NotEqual(SettingLabels.Name(key, arabic: true), SettingLabels.Name(key, arabic: false));
        }
    }
}
