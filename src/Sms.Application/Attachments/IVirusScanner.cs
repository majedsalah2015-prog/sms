using System.Threading;
using System.Threading.Tasks;
using Sms.Domain.Attachments;

namespace Sms.Application.Attachments
{
    /// <summary>
    /// BR-ATT-009 pluggable adapter (cloud scanning service / on-prem
    /// ICAP-CLI). Must never throw for an ordinary scan-engine hiccup —
    /// callers treat anything other than Clean as "keep quarantined."
    /// </summary>
    public interface IVirusScanner
    {
        Task<ScanStatus> ScanAsync(byte[] content, CancellationToken cancellationToken = default);
    }
}
