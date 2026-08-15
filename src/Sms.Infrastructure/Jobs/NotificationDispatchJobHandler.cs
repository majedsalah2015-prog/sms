using System.Threading;
using System.Threading.Tasks;
using Sms.Application.Jobs;
using Sms.Application.Notifications;

namespace Sms.Infrastructure.Jobs
{
    /// <summary>E-007's deferred "recurring dispatch trigger" (BR-NOT-006/009) — the trigger this epic was waiting for.</summary>
    public class NotificationDispatchJobHandler : IJobHandler
    {
        private readonly INotificationDispatcher _dispatcher;

        public NotificationDispatchJobHandler(INotificationDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string JobCode => "NotificationDispatch";

        public Task RunAsync(CancellationToken cancellationToken = default)
            => _dispatcher.DispatchQueuedAsync(cancellationToken);
    }
}
