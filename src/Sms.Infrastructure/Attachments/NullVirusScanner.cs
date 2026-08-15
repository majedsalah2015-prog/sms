using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Attachments;
using Sms.Domain.Attachments;

namespace Sms.Infrastructure.Attachments
{
    /// <summary>
    /// No scanning engine is wired yet — no vendor/ICAP adapter has been
    /// selected (doc 10 §9 Q3, same open-decision category as O6/O7).
    /// Always reports Clean so the pipeline (quarantine → scan → active)
    /// is fully exercised; swapping in a real scanner is a drop-in
    /// IVirusScanner replacement once one is chosen.
    /// </summary>
    public class NullVirusScanner : IVirusScanner
    {
        public Task<ScanStatus> ScanAsync(byte[] content, CancellationToken cancellationToken = default)
            => Task.FromResult(ScanStatus.Clean);
    }
}
