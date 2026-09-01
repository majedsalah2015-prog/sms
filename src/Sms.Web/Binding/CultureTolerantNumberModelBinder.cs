using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Sms.Web.Binding
{
    /// <summary>
    /// Reads <c>decimal</c>, <c>double</c> and <c>float</c> form values written with either the
    /// reader's decimal separator or the invariant one. <c>doc/08 §BR-NUM-007</c> states the
    /// principle for document numbers — "display formatting … is presentation only; the stored
    /// number is the invariant canonical form" — and an amount on a form is the same shape of
    /// thing: the browser posts the canonical form, the screen shows the reader's. BR-GLB-111
    /// covers the message a value that really is not a number still earns.
    /// <para>
    /// <c>Startup</c> pins <c>CurrentCulture.DateTimeFormat.Calendar</c> to Gregorian so a date
    /// posted as <c>yyyy-MM-dd</c> binds in Arabic. Nothing did the equivalent for numbers, and
    /// numbers need it more: <c>ar-SA</c>'s <c>NumberDecimalSeparator</c> is <c>٫</c> (U+066B),
    /// while <c>&lt;input type="number"&gt;</c> is required by HTML to submit a *valid floating-point
    /// number* — always <c>905.00</c>, never <c>905٫00</c>, whatever the page's language. So the
    /// stock binder parsed the browser's own output against a culture that could not read it,
    /// bound the parameter to null, and every money screen in the product refused in Arabic with
    /// its own "… is required" sentence. The salary on a contract, a fee, a payment, a payroll
    /// line: all of them, and only when the amount had a fractional part.
    /// </para>
    /// <para>
    /// Pinning <c>NumberFormat</c> the way the calendar is pinned would have fixed the binding by
    /// changing every Arabic screen's *display* to <c>905.00</c> — a visible product decision, not
    /// a defect fix. Reading both separators changes nothing on screen: Arabic still renders
    /// <c>905٫00</c>, and the box still accepts what the browser sends and what a person types.
    /// </para>
    /// </summary>
    public sealed class CultureTolerantNumberModelBinderProvider : IModelBinderProvider
    {
        /// <inheritdoc />
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            // Registered at the head of the list, which puts this ahead of the four providers that
            // are meant to win before any value-provider binding happens — [ModelBinder], the
            // services binder, the body binder and the header binder. The framework's own
            // floating-point provider never has to check, because it sits after them; this one
            // stands in front and does.
            if (context.BindingInfo.BinderType != null)
            {
                return null;
            }

            var source = context.BindingInfo.BindingSource;
            if (source != null && source.IsGreedy)
            {
                return null;
            }

            var type = context.Metadata.UnderlyingOrModelType;
            return type == typeof(decimal) || type == typeof(double) || type == typeof(float)
                ? new CultureTolerantNumberModelBinder()
                : null;
        }
    }

    /// <summary>
    /// The binder behind <see cref="CultureTolerantNumberModelBinderProvider"/>. Mirrors the
    /// framework's <c>DecimalModelBinder</c> — same number styles, same empty-value handling, same
    /// null-into-a-value-type check, same exception path so the recorded message stays the
    /// translated one <c>Startup</c> installed — and adds exactly one thing: a second parse against
    /// <see cref="CultureInfo.InvariantCulture"/> when the reader's culture cannot read the value.
    /// </summary>
    public sealed class CultureTolerantNumberModelBinder : IModelBinder
    {
        /// <summary>
        /// What the framework's floating-point binders use, kept identical on purpose: a value that
        /// binds today must still bind, and the fallback must be the only difference. Note that
        /// <c>AllowThousands</c> is safe for both cultures this product ships — neither
        /// <c>en-US</c> nor <c>ar-SA</c> groups with <c>.</c>, so the invariant <c>905.00</c> can
        /// never be misread as ninety thousand five hundred on the way past the first parse.
        /// </summary>
        private const NumberStyles Styles = NumberStyles.Float | NumberStyles.AllowThousands;

        /// <inheritdoc />
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            if (bindingContext == null)
            {
                throw new ArgumentNullException(nameof(bindingContext));
            }

            var modelName = bindingContext.ModelName;
            var entry = bindingContext.ValueProvider.GetValue(modelName);
            if (entry == ValueProviderResult.None)
            {
                // Nothing was posted under this name; leave the parameter's own default alone.
                return Task.CompletedTask;
            }

            var modelState = bindingContext.ModelState;
            modelState.SetModelValue(modelName, entry);

            var metadata = bindingContext.ModelMetadata;
            var type = metadata.UnderlyingOrModelType;

            try
            {
                var text = entry.FirstValue;
                var model = string.IsNullOrWhiteSpace(text) ? null : Parse(type, text!, entry.Culture);

                // A null that cannot be stored is a failed conversion, not a bound null — the
                // framework's own check, kept so an empty box on a non-nullable decimal still says
                // "this field is required" rather than binding zero.
                if (model == null && !metadata.IsReferenceOrNullableType)
                {
                    modelState.TryAddModelError(
                        modelName,
                        metadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(entry.ToString()));
                }
                else
                {
                    bindingContext.Result = ModelBindingResult.Success(model);
                }
            }
            catch (Exception exception)
            {
                modelState.TryAddModelError(modelName, exception, metadata);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// The reader's culture first, so a person typing <c>٩٠٥٫٥</c>'s separator into a text box is
        /// read the way they meant it; the invariant form second, because that is what every
        /// <c>type="number"</c> box on the page submits regardless of language.
        /// </summary>
        private static object Parse(Type type, string text, CultureInfo culture)
        {
            if (TryParse(type, text, culture, out var model))
            {
                return model!;
            }

            if (!culture.Equals(CultureInfo.InvariantCulture)
                && TryParse(type, text, CultureInfo.InvariantCulture, out model))
            {
                return model!;
            }

            // Neither separator read it, so it is not a number in any language this product speaks.
            // Let the framework's own parse raise the failure: MVC turns that exact exception into
            // the "value is not valid" message, and Startup already ships that message in both
            // languages. Composing one here would be a third English sentence nobody translated.
            return type == typeof(decimal) ? decimal.Parse(text, Styles, culture)
                : type == typeof(double) ? double.Parse(text, Styles, culture)
                : (object)float.Parse(text, Styles, culture);
        }

        private static bool TryParse(Type type, string text, CultureInfo culture, out object? model)
        {
            if (type == typeof(decimal))
            {
                if (decimal.TryParse(text, Styles, culture, out var value))
                {
                    model = value;
                    return true;
                }
            }
            else if (type == typeof(double))
            {
                if (double.TryParse(text, Styles, culture, out var value))
                {
                    model = value;
                    return true;
                }
            }
            else if (type == typeof(float))
            {
                if (float.TryParse(text, Styles, culture, out var value))
                {
                    model = value;
                    return true;
                }
            }

            model = null;
            return false;
        }
    }
}
