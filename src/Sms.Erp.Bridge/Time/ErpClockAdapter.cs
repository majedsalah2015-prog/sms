using System;
using ERP2028.Application.Abstractions.Time;
using Sms.Application.Common.Interfaces;

namespace Sms.Erp.Bridge.Time
{
    /// <summary>
    /// Presents the school's clock to the ERP modules as their own
    /// <see cref="IDateTime"/>. The two abstractions are the same idea named
    /// twice; nothing is converted, and both sides keep stamping from one source
    /// so an SMS document and the journal entry it produces cannot disagree
    /// about when they happened.
    /// <para>
    /// The first of the host adapters described in
    /// docs/Integration/01-Embedded-Accounting-Plan.md §5. It is here in P1
    /// rather than P2 because an empty project proves nothing: this compiles
    /// only if a type from the ERP's abstractions and a type from
    /// <c>Sms.Application</c> can meet in one assembly, which is the whole claim
    /// the skeleton exists to test.
    /// </para>
    /// </summary>
    public sealed class ErpClockAdapter : IDateTime
    {
        private readonly IClock _clock;

        public ErpClockAdapter(IClock clock) => _clock = clock;

        public DateTime UtcNow => _clock.UtcNow;
    }
}
