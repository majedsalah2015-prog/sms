using System;
using System.Collections.Generic;
using System.Linq;
using Sms.Application.Security;
using Sms.Domain.Discipline;
using Sms.Domain.Security;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// doc/Modules/25 §8 screens. Two things are worth a red build here, and they are not the same
    /// kind of thing.
    /// <para>
    /// The first is bilingual coverage: the discipline screens print six enums, every label falls
    /// back to the enum name when a value has no translation, and that fallback means a member added
    /// later would quietly start printing "InSchoolSuspension" to an Arabic parent's school. Nothing
    /// would fail. So this fails instead.
    /// </para>
    /// <para>
    /// The second is the module's separation of powers. BR-DCP-002 opens recording to every teacher
    /// and BR-DCP-003 closes deciding to almost nobody, which only holds if recording and deciding
    /// are separate screens with separately grantable verbs. That is a property of the catalogue, and
    /// a later tidy-up that merged the two screens would silently hand every teacher the power to
    /// decide the case they themselves reported.
    /// </para>
    /// </summary>
    public class DisciplineScreenTests
    {
        // ------------------------------------------------------------------ bilingual labels

        public static IEnumerable<object[]> ArabicLabels() => new[]
        {
            Case<CaseStatus>(v => DisciplineLabels.CaseStatus(v, true)),
            Case<ConsequenceKind>(v => DisciplineLabels.ConsequenceKind(v, true)),
            Case<StatementKind>(v => DisciplineLabels.StatementKind(v, true)),
            Case<AppealOutcome>(v => DisciplineLabels.AppealOutcome(v, true)),
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

        [Theory]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public void Every_severity_level_reads_as_a_word_in_both_languages(int severity)
        {
            var arabic = DisciplineLabels.Severity(severity, true);
            var english = DisciplineLabels.Severity(severity, false);

            // BR-DCP-001's scale is 1–4, and a bare number on a screen tells a teacher nothing about
            // what recording it will set in motion.
            Assert.NotEqual(severity.ToString(System.Globalization.CultureInfo.InvariantCulture), arabic);
            Assert.NotEqual(severity.ToString(System.Globalization.CultureInfo.InvariantCulture), english);
            Assert.DoesNotContain(arabic, c => c is >= 'A' and <= 'Z');
        }

        [Fact]
        public void Every_severity_level_has_a_colour_of_its_own()
        {
            var classes = new[] { 1, 2, 3, 4 }.Select(DisciplineLabels.SeverityClass).ToList();

            // The badge colour is how severity reads at a glance on the board; two levels sharing one
            // makes the glance wrong rather than merely plain.
            Assert.Equal(classes.Count, classes.Distinct(StringComparer.Ordinal).Count());
        }

        // ------------------------------------------------------------------ separation of powers

        [Fact]
        public void Recording_and_deciding_are_separate_screens()
        {
            // If these ever became one screen, "may record an incident" and "may decide the case it
            // opens" would be the same grant — which is precisely what BR-DCP-002/003 separate.
            Assert.NotEqual(ScreenCatalog.Discipline.Incidents, ScreenCatalog.Discipline.Cases);

            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, ActionVerb.Create));
            Assert.False(
                ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Incidents, ActionVerb.Approve),
                "The record desk must not define Approve — deciding a case is not something a recorder does.");

            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Approve));
            Assert.False(
                ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Cases, ActionVerb.Create),
                "Cases are opened by the engine when severity warrants it (BR-DCP-002), never created on a screen.");
        }

        [Fact]
        public void Writing_the_code_and_publishing_it_are_different_verbs()
        {
            // BR-DCP-001: the handbook families are held to is published, and publishing it is not the
            // same act as drafting it. Separate verbs are what let a school give those to two people.
            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, ActionVerb.Configure));
            Assert.True(ScreenCatalog.Defines(ScreenCatalog.Modules.Discipline, ScreenCatalog.Discipline.Code, ActionVerb.Approve));
        }

        [Fact]
        public void Analytics_can_only_be_read()
        {
            // The repeat list names children by their record (BR-DCP-008). A write verb on this screen
            // would mean something on it could be changed, and nothing on it should be.
            var verbs = ScreenCatalog.Screens
                .Single(s => s.ModuleCode == ScreenCatalog.Modules.Discipline && s.ScreenCode == ScreenCatalog.Discipline.Analytics)
                .Verbs;

            Assert.Equal(new[] { ActionVerb.View }, verbs.ToArray());
        }

        [Fact]
        public void The_module_code_is_the_one_the_sidebar_already_uses()
        {
            // ScreenCatalog's module codes are ModuleCatalog's, so a permission reads back to a
            // sidebar entry without a translation table in between.
            Assert.Equal("DIS", ScreenCatalog.Modules.Discipline);
            Assert.NotNull(Sms.Web.Navigation.ModuleCatalog.Find("DIS"));
        }

        [Fact]
        public void The_sidebar_entry_points_at_the_case_board_rather_than_the_placeholder()
        {
            var module = Sms.Web.Navigation.ModuleCatalog.Find("DIS")!;

            Assert.Equal("Discipline", module.ScreenController);
            Assert.Equal("Index", module.ScreenAction);
        }
    }
}
