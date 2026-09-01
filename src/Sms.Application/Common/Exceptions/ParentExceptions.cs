using System;

namespace Sms.Application.Common.Exceptions
{
    /// <summary>
    /// Which way a submitted residence selection fails (doc/Modules/11 §7: governorate → locality →
    /// quarter, of which only the lower two are stored).
    /// </summary>
    public enum ResidenceSelectionFault
    {
        /// <summary>A quarter chosen with no locality under it to place it in.</summary>
        QuarterWithoutLocality = 1,

        /// <summary>A quarter that belongs to some locality other than the one chosen beside it.</summary>
        QuarterOutsideLocality = 2,
    }

    /// <summary>
    /// The residence hierarchy was given a selection it cannot store.
    /// <para>
    /// The fault travels as a value rather than as a sentence, because the two cases need different
    /// advice and the boundary has to be able to give it in either language — see
    /// <c>Sms.Web/Models/UserMessage.People.cs</c>. Both were raised as a bare
    /// <see cref="InvalidOperationException"/> until now, which reached an Arabic screen in English.
    /// </para>
    /// </summary>
    public sealed class InvalidResidenceSelectionException : InvalidOperationException
    {
        public InvalidResidenceSelectionException(ResidenceSelectionFault fault)
            : base(fault == ResidenceSelectionFault.QuarterWithoutLocality
                ? "A neighbourhood cannot be recorded without the locality it belongs to."
                : "That neighbourhood does not belong to the chosen locality.")
            => Fault = fault;

        public ResidenceSelectionFault Fault { get; }
    }
}
