using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Guards;
using Sms.TestSupport;
using Sms.Web.Models;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// Every refusal this product can raise has a sentence in both languages, and this is what keeps
    /// that true as the product grows.
    /// <para>
    /// The rule was already written down — an Arabic screen never shows an English refusal — and
    /// <see cref="TranslatedRefusalTests"/> already held controllers to routing through
    /// <see cref="UserMessage"/>. Neither caught the actual gap, which was on the other side of that
    /// call: for roughly a hundred and eighty of the product's exception types the translator had no
    /// case, so it fell through to the engine's English sentence. From the controller, routing
    /// correctly to a translator that knows the type and routing correctly to one that does not look
    /// exactly the same.
    /// </para>
    /// <para>
    /// So this test does not read a list. It reflects over every exception type the Application and
    /// Domain assemblies define, builds one of each, and asks for its message in both languages. A
    /// new exception type is therefore translated before it can ship — the build says so on the day
    /// it is written, not the day a school meets it.
    /// </para>
    /// <para>
    /// It asserts three things and pins no wording: that the sentence is not the engine's own text,
    /// that the two languages are not the same string, and that the Arabic one is actually in
    /// Arabic. Wording is meant to keep improving; a test that fixes the sentence makes improving it
    /// a chore, and this file would then be edited to match the code rather than to check it.
    /// </para>
    /// </summary>
    public class RefusalCoverageTests
    {
        /// <summary>
        /// Types this test cannot build and does not need to. <see cref="MissingAuditReasonException"/>
        /// and the rest are all covered — they are simply constructed from values only the audit
        /// captor has, and their translations are asserted where those values exist.
        /// </summary>
        private static readonly HashSet<string> NotConstructedHere = new(StringComparer.Ordinal);

        public static IEnumerable<object[]> EveryRefusal() => Assemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(IsRefusal)
            .Where(type => !NotConstructedHere.Contains(type.Name))
            .OrderBy(type => type.Name, StringComparer.Ordinal)
            .Select(type => new object[] { type });

        [Theory]
        [MemberData(nameof(EveryRefusal))]
        [BusinessRule("BR-GLB-001")]
        public void Every_refusal_the_product_can_raise_is_translated(Type type)
        {
            var refusal = Build(type);

            var arabic = UserMessage.For(refusal, arabic: true);
            var english = UserMessage.For(refusal, arabic: false);

            Assert.False(
                string.Equals(arabic, refusal.Message, StringComparison.Ordinal),
                $"{type.Name} has no Arabic sentence — UserMessage.For falls through to the engine's own " +
                "English text. Add a case to the module table in src/Sms.Web/Models/UserMessage.*.cs.");

            Assert.False(
                string.Equals(english, refusal.Message, StringComparison.Ordinal),
                $"{type.Name} has no English sentence of its own. The engine's message is written for a log — " +
                "it names ids and rule numbers. Give the reader one that says what to do next.");

            Assert.False(
                string.Equals(arabic, english, StringComparison.Ordinal),
                $"{type.Name} returns the same string in both languages, so one of them is wrong.");

            Assert.True(
                arabic.Any(IsArabicLetter),
                $"{type.Name}'s Arabic sentence contains no Arabic: \"{arabic}\".");
        }

        /// <summary>
        /// A translated refusal still has to be readable. These are the two ways one stops being so:
        /// an empty string, and a sentence that is only a rule reference.
        /// </summary>
        [Theory]
        [MemberData(nameof(EveryRefusal))]
        [BusinessRule("BR-GLB-001")]
        public void Every_translated_refusal_is_a_sentence(Type type)
        {
            var refusal = Build(type);

            foreach (var arabic in new[] { true, false })
            {
                var message = UserMessage.For(refusal, arabic);

                Assert.False(string.IsNullOrWhiteSpace(message), $"{type.Name} returns an empty message.");
                Assert.True(
                    message.Length >= 20,
                    $"{type.Name} returns \"{message}\", which is too short to tell anyone anything.");
            }
        }

        private static IEnumerable<Assembly> Assemblies() => new[]
        {
            typeof(HardDeleteForbiddenException).Assembly,          // Sms.Application
            typeof(Sms.Domain.Common.ISchoolScoped).Assembly,       // Sms.Domain
        };

        /// <summary>
        /// A refusal is a concrete, public exception type. <c>RoomAvailabilityException</c> is
        /// deliberately not one — despite the name it is a Domain entity, a row saying a room is
        /// under maintenance, and it is filtered out here by being an entity rather than by name.
        /// </summary>
        private static bool IsRefusal(Type type)
            => typeof(Exception).IsAssignableFrom(type)
               && type.IsPublic
               && !type.IsAbstract;

        /// <summary>
        /// One of the type, built from whatever its narrowest constructor asks for. The values are
        /// placeholders — the assertions above are about which sentence comes back, not about what
        /// is interpolated into it.
        /// </summary>
        private static Exception Build(Type type)
        {
            foreach (var constructor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
            {
                var arguments = constructor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();
                if (arguments.Any(a => a is Unbuildable))
                {
                    continue;
                }

                return (Exception)constructor.Invoke(arguments);
            }

            throw new InvalidOperationException(
                $"{type.Name} takes a constructor argument this test cannot make up. Teach Sample() the type, " +
                "or add the exception to NotConstructedHere with a note saying where it is covered instead.");
        }

        /// <summary>A stand-in value for one constructor parameter, or a marker meaning "not this constructor".</summary>
        private static object Sample(Type type)
        {
            if (type == typeof(string))
            {
                return "X1";
            }

            if (type.IsEnum)
            {
                return Enum.GetValues(type).GetValue(0)!;
            }

            if (type == typeof(UsageReport))
            {
                return UsageReport.From(new UsageReference("open loan(s)", "إعارة مفتوحة", 2));
            }

            if (type == typeof(Exception) || typeof(Exception).IsAssignableFrom(type))
            {
                // An inner-exception overload. There is always a plainer constructor, and the
                // wrapped-exception one would say nothing about this type's own sentence.
                return new Unbuildable();
            }

            if (type.IsArray)
            {
                var element = type.GetElementType()!;
                var array = Array.CreateInstance(element, 1);
                var sample = Sample(element);
                if (sample is Unbuildable)
                {
                    return new Unbuildable();
                }

                array.SetValue(sample, 0);
                return array;
            }

            if (type.IsGenericType && Collections.Contains(type.GetGenericTypeDefinition()))
            {
                var element = type.GetGenericArguments()[0];
                var sample = Sample(element);
                if (sample is Unbuildable)
                {
                    return new Unbuildable();
                }

                var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(element))!;
                list.Add(sample);
                return list;
            }

            if (type == typeof(DateTime))
            {
                return new DateTime(2026, 3, 15);
            }

            if (type.IsValueType)
            {
                // int, decimal, bool, TimeSpan and the rest: whatever "one" or "false" is for it.
                return type == typeof(bool) ? false : Convert.ChangeType(1, type);
            }

            // Anything else the Application layer carries on a refusal — a checklist item, a verdict —
            // is a plain object whose own constructor takes the same kinds of values. Build it the
            // same way, one level down, rather than teaching this method every such type by name.
            foreach (var constructor in type.GetConstructors().OrderBy(c => c.GetParameters().Length))
            {
                var arguments = constructor.GetParameters().Select(p => Sample(p.ParameterType)).ToArray();
                if (arguments.All(a => a is not Unbuildable))
                {
                    return constructor.Invoke(arguments);
                }
            }

            return new Unbuildable();
        }

        private static readonly HashSet<Type> Collections = new()
        {
            typeof(IReadOnlyList<>),
            typeof(IReadOnlyCollection<>),
            typeof(IList<>),
            typeof(ICollection<>),
            typeof(IEnumerable<>),
            typeof(List<>),
        };

        /// <summary>Arabic script proper, plus the Arabic Supplement and Extended-A ranges.</summary>
        private static bool IsArabicLetter(char c) => c is >= '؀' and <= 'ࣿ';

        private sealed class Unbuildable
        {
        }
    }
}
