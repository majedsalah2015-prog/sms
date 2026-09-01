using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Sms.TestSupport;
using Sms.Web.Binding;
using Xunit;

namespace Sms.Web.Tests
{
    /// <summary>
    /// The Arabic half of every money form in this product.
    /// <para>
    /// <c>ar-SA</c>'s <c>NumberDecimalSeparator</c> is <c>٫</c> (U+066B). An
    /// <c>&lt;input type="number"&gt;</c> is required by HTML to submit a valid floating-point
    /// number, so it posts <c>905.00</c> in every language there is. The framework's binder read
    /// the browser's own output against the reader's culture, failed, and handed the action a
    /// null — which every screen then reported as "this field is required", about a field the
    /// person had filled in. Contracts, fees, payments, payroll: all of them, and only when the
    /// amount had a fractional part, which is why it survived a year of whole-number demo data.
    /// </para>
    /// <para>
    /// <c>doc/08 §BR-NUM-007</c>: display formatting is presentation only and the stored number is
    /// the invariant canonical form. These tests hold the binder to reading that canonical form in
    /// Arabic, without giving up the reader's own separator or turning a genuinely bad value into
    /// a silent zero.
    /// </para>
    /// </summary>
    public class CultureTolerantNumberBindingTests
    {
        private static readonly CultureInfo Arabic = CultureInfo.GetCultureInfo("ar-SA");
        private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

        // ------------------------------------------------------------------ the regression

        [Theory]
        [BusinessRule("BR-NUM-007")]
        [InlineData("905.00", 905.00)]
        [InlineData("0.5", 0.5)]
        [InlineData("1234.75", 1234.75)]
        [InlineData("-12.25", -12.25)]
        public void An_arabic_reader_binds_the_invariant_amount_the_browser_posts(string posted, double expected)
        {
            var (result, state) = Bind(typeof(decimal), posted, Arabic);

            Assert.True(result.IsModelSet);
            Assert.Equal((decimal)expected, Assert.IsType<decimal>(result.Model));
            Assert.Equal(0, state.ErrorCount);
        }

        /// <summary>
        /// The other direction, and the reason the reader's culture is still tried first: a text
        /// box an Arabic user types into by hand carries the separator their keyboard produces.
        /// </summary>
        [Theory]
        [BusinessRule("BR-NUM-007")]
        [InlineData("905٫00", 905.00)]
        [InlineData("1٬450٫75", 1450.75)]
        public void An_arabic_reader_also_binds_their_own_separator(string posted, double expected)
        {
            var (result, state) = Bind(typeof(decimal), posted, Arabic);

            Assert.True(result.IsModelSet);
            Assert.Equal((decimal)expected, Assert.IsType<decimal>(result.Model));
            Assert.Equal(0, state.ErrorCount);
        }

        /// <summary>
        /// 905.00 must not become ninety thousand five hundred on the way through a culture that
        /// cannot read it. Neither culture this product ships groups with a full stop, and the
        /// fallback only runs after the first parse has failed outright — but a money field is the
        /// wrong place to leave that to reasoning.
        /// </summary>
        [Fact]
        [BusinessRule("BR-NUM-007")]
        public void The_fallback_never_reads_a_decimal_point_as_a_thousands_group()
        {
            var (result, _) = Bind(typeof(decimal), "905.00", Arabic);

            Assert.Equal(905.00m, result.Model);
            Assert.NotEqual(90500m, result.Model);
        }

        // ------------------------------------------------------------------ nothing else moved

        [Theory]
        [InlineData("905.00", 905.00)]
        [InlineData("1,450.00", 1450.00)]
        [InlineData("0.5", 0.5)]
        public void An_english_reader_binds_exactly_what_they_did_before(string posted, double expected)
        {
            var (result, state) = Bind(typeof(decimal), posted, English);

            Assert.True(result.IsModelSet);
            Assert.Equal((decimal)expected, Assert.IsType<decimal>(result.Model));
            Assert.Equal(0, state.ErrorCount);
        }

        [Theory]
        [InlineData(typeof(double))]
        [InlineData(typeof(float))]
        public void Double_and_float_fall_back_the_same_way(Type type)
        {
            var (result, state) = Bind(type, "905.5", Arabic);

            Assert.True(result.IsModelSet);
            Assert.Equal(905.5, Convert.ToDouble(result.Model, CultureInfo.InvariantCulture), 3);
            Assert.Equal(0, state.ErrorCount);
        }

        /// <summary>
        /// A value that is not a number in either reading is still refused — the fallback widens
        /// what can be read, never what is accepted. Silently binding zero here would put a wrong
        /// amount in the ledger, which is worse than the refusal this replaced.
        /// </summary>
        [Theory]
        [InlineData("abc")]
        [InlineData("905.00.00")]
        [InlineData("١٢٣")]
        public void A_value_that_is_not_a_number_in_either_reading_is_refused(string posted)
        {
            var (result, state) = Bind(typeof(decimal), posted, Arabic);

            Assert.False(result.IsModelSet);
            Assert.Equal(1, state.ErrorCount);
            Assert.Single(state["amount"].Errors);
        }

        /// <summary>
        /// An English reader does not silently gain the Arabic separator: it is not the invariant
        /// form and it is not theirs, so it is not a number.
        /// </summary>
        [Fact]
        public void An_english_reader_does_not_read_the_arabic_separator()
        {
            var (result, state) = Bind(typeof(decimal), "905٫00", English);

            Assert.False(result.IsModelSet);
            Assert.Equal(1, state.ErrorCount);
        }

        // ------------------------------------------------------------------ empty boxes

        [Fact]
        public void An_empty_box_binds_null_on_a_nullable_amount()
        {
            var (result, state) = Bind(typeof(decimal?), string.Empty, Arabic);

            Assert.True(result.IsModelSet);
            Assert.Null(result.Model);
            Assert.Equal(0, state.ErrorCount);
        }

        [Fact]
        public void An_empty_box_is_refused_on_a_required_amount_rather_than_bound_to_zero()
        {
            var (result, state) = Bind(typeof(decimal), string.Empty, Arabic);

            Assert.False(result.IsModelSet);
            Assert.Equal(1, state.ErrorCount);
        }

        [Fact]
        public void A_name_nothing_was_posted_under_leaves_the_parameter_alone()
        {
            var context = Context(typeof(decimal), new SingleValueProvider("somethingElse", "1", Arabic));

            new CultureTolerantNumberModelBinder().BindModelAsync(context).GetAwaiter().GetResult();

            Assert.False(context.Result.IsModelSet);
            Assert.Equal(0, context.ModelState.ErrorCount);
        }

        // ------------------------------------------------------------------ the provider's reach

        [Theory]
        [InlineData(typeof(decimal))]
        [InlineData(typeof(decimal?))]
        [InlineData(typeof(double))]
        [InlineData(typeof(double?))]
        [InlineData(typeof(float))]
        [InlineData(typeof(float?))]
        public void The_provider_claims_every_fractional_type(Type type)
            => Assert.IsType<CultureTolerantNumberModelBinder>(BinderFor(type));

        [Theory]
        [InlineData(typeof(int))]
        [InlineData(typeof(long))]
        [InlineData(typeof(string))]
        [InlineData(typeof(DateTime))]
        [InlineData(typeof(bool))]
        public void The_provider_leaves_every_other_type_to_the_framework(Type type)
            => Assert.Null(BinderFor(type));

        /// <summary>
        /// Sitting at the head of the provider list means standing in front of the four providers
        /// that must win before any value-provider binding happens. An amount read from the request
        /// body, or one with its own <c>[ModelBinder]</c>, is not this binder's to take.
        /// </summary>
        [Fact]
        public void The_provider_declines_a_binding_that_belongs_to_a_provider_it_now_precedes()
        {
            Assert.Null(BinderFor(typeof(decimal), source: BindingSource.Body));
            Assert.Null(BinderFor(typeof(decimal), source: BindingSource.Services));
            Assert.Null(BinderFor(typeof(decimal), source: BindingSource.Header));
            Assert.Null(BinderFor(typeof(decimal), binderType: typeof(CultureTolerantNumberModelBinder)));

            // …while the sources a form actually arrives through stay claimed.
            Assert.NotNull(BinderFor(typeof(decimal), source: BindingSource.Form));
            Assert.NotNull(BinderFor(typeof(decimal), source: BindingSource.Query));
        }

        // ------------------------------------------------------------------ harness

        private static (ModelBindingResult Result, ModelStateDictionary State) Bind(
            Type modelType, string posted, CultureInfo culture)
        {
            var context = Context(modelType, new SingleValueProvider("amount", posted, culture));

            new CultureTolerantNumberModelBinder().BindModelAsync(context).GetAwaiter().GetResult();

            return (context.Result, context.ModelState);
        }

        private static DefaultModelBindingContext Context(Type modelType, IValueProvider values) => new()
        {
            ModelMetadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType),
            ModelName = "amount",
            ModelState = new ModelStateDictionary(),
            ValueProvider = values,
        };

        private static IModelBinder? BinderFor(Type modelType, BindingSource? source = null, Type? binderType = null)
        {
            var metadata = new EmptyModelMetadataProvider().GetMetadataForType(modelType);
            var context = new TestBinderProviderContext(metadata)
            {
                BindingInfo = { BindingSource = source, BinderType = binderType },
            };

            return new CultureTolerantNumberModelBinderProvider().GetBinder(context);
        }

        /// <summary>
        /// <c>ModelBinderProviderContext</c> is abstract and its production implementation is
        /// internal to MVC; only the two members the provider reads are needed here.
        /// </summary>
        private sealed class TestBinderProviderContext : ModelBinderProviderContext
        {
            public TestBinderProviderContext(ModelMetadata metadata) => Metadata = metadata;

            public override BindingInfo BindingInfo { get; } = new();

            public override ModelMetadata Metadata { get; }

            public override IModelMetadataProvider MetadataProvider { get; } = new EmptyModelMetadataProvider();

            public override IModelBinder CreateBinder(ModelMetadata metadata) => throw new NotSupportedException();
        }

        private sealed class SingleValueProvider : IValueProvider
        {
            private readonly string _name;
            private readonly string _value;
            private readonly CultureInfo _culture;

            public SingleValueProvider(string name, string value, CultureInfo culture)
            {
                _name = name;
                _value = value;
                _culture = culture;
            }

            public bool ContainsPrefix(string prefix)
                => string.Equals(prefix, _name, StringComparison.OrdinalIgnoreCase);

            public ValueProviderResult GetValue(string key)
                => string.Equals(key, _name, StringComparison.OrdinalIgnoreCase)
                    ? new ValueProviderResult(_value, _culture)
                    : ValueProviderResult.None;
        }
    }
}
