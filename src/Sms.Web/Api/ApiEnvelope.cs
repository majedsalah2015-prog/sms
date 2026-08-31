using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sms.Web.Api
{
    /// <summary>
    /// The one refusal shape the mobile client parses. Every non-2xx answer this
    /// API gives is an <see cref="ApiErrorResponse"/> — including the ones the
    /// framework raises on its own (model binding, 401, 403, 404), which are
    /// reshaped into it rather than left as the two other formats ASP.NET Core
    /// would otherwise emit.
    /// <para>
    /// <see cref="ApiError.Message"/> is already in the caller's language: doc
    /// 06 and this repository's standing rule are that a refusal the user can
    /// trigger is translated at the web boundary, and an API is a web boundary
    /// like any other. <see cref="ApiError.Code"/> is the stable half — a client
    /// branches on the code and shows the message, never the reverse.
    /// </para>
    /// </summary>
    public sealed class ApiErrorResponse
    {
        public ApiErrorResponse(ApiError error)
        {
            Error = error;
        }

        public ApiError Error { get; }
    }

    /// <summary>One refusal. See <see cref="ApiErrorResponse"/>.</summary>
    public sealed class ApiError
    {
        public ApiError(string code, string message, IDictionary<string, string[]>? fields = null)
        {
            Code = code;
            Message = message;
            Fields = fields;
        }

        /// <summary>Stable, language-independent, snake_case. What a client branches on.</summary>
        public string Code { get; }

        /// <summary>Arabic or English per the request's culture. What a client shows.</summary>
        public string Message { get; }

        /// <summary>Per-field validation messages, present only on <c>validation_failed</c>.</summary>
        public IDictionary<string, string[]>? Fields { get; }
    }

    /// <summary>
    /// The one list shape. Every collection endpoint pages, without exception:
    /// a school's student roll and a year's charges are both lists a phone must
    /// not be handed whole, and an endpoint that returns everything today is one
    /// that times out in the third school it is deployed to.
    /// </summary>
    public sealed class ApiPage<T>
    {
        public ApiPage(IReadOnlyList<T> items, int page, int pageSize, int total)
        {
            Items = items;
            Page = page;
            PageSize = pageSize;
            Total = total;
        }

        public IReadOnlyList<T> Items { get; }

        /// <summary>1-based.</summary>
        public int Page { get; }

        public int PageSize { get; }

        /// <summary>Rows matching the query, not rows in this page.</summary>
        public int Total { get; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);

        public bool HasMore => Page < TotalPages;
    }

    /// <summary>
    /// Paging as it arrives on the query string, clamped where it is read rather
    /// than trusted. <see cref="PageSize"/> has a ceiling because the client
    /// picking it is the one thing a server may never let decide how much work
    /// it does.
    /// </summary>
    public static class ApiPaging
    {
        public const int DefaultPageSize = 25;
        public const int MaxPageSize = 200;

        public static (int Page, int PageSize) Clamp(int? page, int? pageSize)
        {
            var p = page.GetValueOrDefault(1);
            var s = pageSize.GetValueOrDefault(DefaultPageSize);
            return (p < 1 ? 1 : p, s < 1 ? DefaultPageSize : s > MaxPageSize ? MaxPageSize : s);
        }

        public static int Skip(int page, int pageSize) => (page - 1) * pageSize;
    }

    /// <summary>
    /// Money as this API reports it. Amounts cross the wire as a JSON number in
    /// invariant form and are never pre-formatted: BR-NUM-007's separators and
    /// digit shapes are a display decision, and the phone showing the figure is
    /// the only side that knows which locale it is showing it in.
    /// <para>
    /// <see cref="Currency"/> travels with every amount so a client never has to
    /// infer it from a screen title.
    /// </para>
    /// </summary>
    public sealed class ApiMoney
    {
        public ApiMoney(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        public decimal Amount { get; }

        public string Currency { get; }

        /// <summary>Invariant, for a client that would rather not re-derive it.</summary>
        public string Text => Amount.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
