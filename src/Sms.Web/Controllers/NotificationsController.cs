using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Audit;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Messaging;
using Sms.Application.Notifications;
using Sms.Application.Security;
using Sms.Application.Setup;
using Sms.Domain.Notifications;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Module 33's administration surface — the four screens doc/Modules/33 §8 asks for
    /// that did not exist: the template studio (§8.2), the provider console (§8.3), the
    /// delivery operations log (§8.4) and the budget console (§8.5), plus every user's own
    /// notification centre (§8.6).
    /// <para>
    /// §8.1's event × channel subscription matrix is deliberately not here. It already
    /// exists as <c>/setup/notifications</c> — doc/Modules/01 §8.3 puts it in the settings
    /// hub — and a second screen over the same two tables would be two places to change
    /// one decision. The workspace tabs link to it instead.
    /// </para>
    /// <para>
    /// <b>The notification centre carries no permission.</b> Everything else here is
    /// gated, but a signed-in person reading their own inbox is not exercising a grant:
    /// the scope is the recipient id, not the catalogue, and a school that had to hand out
    /// a permission before staff could see their own alerts would hand it to everybody.
    /// </para>
    /// </summary>
    [Route("notifications")]
    public class NotificationsController : Controller
    {
        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private readonly INotificationConfigAdmin _config;
        private readonly INotificationOpsAdmin _ops;
        private readonly INotificationDispatcher _dispatcher;
        private readonly ISystemSetupAdmin _setup;
        private readonly ICurrentUser _currentUser;
        private readonly IClock _clock;
        private readonly IAuditContext _audit;

        public NotificationsController(
            INotificationConfigAdmin config,
            INotificationOpsAdmin ops,
            INotificationDispatcher dispatcher,
            ISystemSetupAdmin setup,
            ICurrentUser currentUser,
            IClock clock,
            IAuditContext audit)
        {
            _config = config;
            _ops = ops;
            _dispatcher = dispatcher;
            _setup = setup;
            _currentUser = currentUser;
            _clock = clock;
            _audit = audit;
        }

        // ================================================================== §8.6 notification centre

        /// <summary>
        /// The signed-in user's own in-app inbox — doc 09 §5's bell/list/mark-read, which
        /// <see cref="Delivery"/> was built for ("for InApp this row IS the inbox entry")
        /// and which nothing rendered until now.
        /// </summary>
        [HttpGet("")]
        [NoPermissionRequired("A person's own inbox. Scoped to their own user id, not to a grant — every signed-in user has one, and gating it would mean granting it to everybody.")]
        public async Task<IActionResult> Index(bool includeRead = false)
        {
            var unread = await MineAsync(includeRead: false);

            return View(new NotificationCentreViewModel
            {
                Items = includeRead ? await MineAsync(includeRead: true) : unread,
                IncludeRead = includeRead,
                UnreadCount = unread.Count,

                // The one screen of this controller a family reaches (BR-SEC-010's portal
                // allow-list names these three actions and nothing else here), so it wears the
                // portal's shell for them and the staff shell for everyone else.
                ForPortal = PortalAreaFilter.IsPortalAccount(User.FindFirst(SmsClaimTypes.AccountType)?.Value),
            });
        }

        [HttpPost("read/{deliveryId:int}")]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Marks the caller's own notification read; the port refuses any row that is not theirs.")]
        public async Task<IActionResult> MarkRead(int deliveryId, bool includeRead = false)
        {
            await _ops.MarkInAppReadAsync(deliveryId, _currentUser.UserId, HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index), new { includeRead });
        }

        [HttpPost("read-all")]
        [ValidateAntiForgeryToken]
        [NoPermissionRequired("Marks the caller's own notifications read.")]
        public async Task<IActionResult> MarkAllRead()
        {
            var count = await _ops.MarkAllInAppReadAsync(_currentUser.UserId, HttpContext.RequestAborted);
            TempData["Message"] = count == 0
                ? T("Nothing was unread.", "لا يوجد غير مقروء.")
                : T($"{count} notification(s) marked read.", $"وُسِمت {count} إشعاراً كمقروء.");
            return RedirectToAction(nameof(Index));
        }

        private async Task<IReadOnlyList<InboxItem>> MineAsync(bool includeRead)
            => await _ops.ListInboxAsync(_currentUser.UserId, includeRead, HttpContext.RequestAborted);

        // ================================================================== §8.2 template studio

        [HttpGet("templates")]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Templates, ActionVerb.View)]
        public async Task<IActionResult> Templates()
            => View(new TemplateStudioViewModel
            {
                Templates = await _config.ListTemplatesAsync(HttpContext.RequestAborted),
                TakenPairs = await _config.ListTemplatedPairsAsync(HttpContext.RequestAborted),
            });

        [HttpGet("templates/{id:int}")]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Templates, ActionVerb.View)]
        public async Task<IActionResult> Template(int id)
        {
            var detail = await _config.GetTemplateAsync(id, HttpContext.RequestAborted);
            if (detail == null)
            {
                return NotFound();
            }

            return View(new TemplateEditorViewModel
            {
                Detail = detail,
                Placeholders = TemplatePlaceholderRules.Available(detail.Template.EventCode),
            });
        }

        /// <summary>
        /// Writes a new version, never over the old one (BR-NOT-008). The placeholder check
        /// is doc/Modules/33 §9's, applied at authoring because <see cref="TemplateRenderer"/>
        /// deliberately will not apply it at send: an unknown token is left in the text, and
        /// a parent reading the literal word "{Amount}" is what this refusal prevents.
        /// </summary>
        [HttpPost("templates")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Templates, ActionVerb.Create)]
        public async Task<IActionResult> SaveTemplate(
            string eventCode, NotificationChannel channel, string? subjectAr, string? subjectEn, string bodyAr, string bodyEn)
        {
            if (!NotificationEventCatalog.TryGet(eventCode, out _))
            {
                TempData["Error"] = T("There is no notification event with that code.", "لا يوجد حدث إشعار بهذا الرمز.");
                return RedirectToAction(nameof(Templates));
            }

            if (string.IsNullOrWhiteSpace(bodyAr) || string.IsNullOrWhiteSpace(bodyEn))
            {
                TempData["Error"] = T(
                    "Both languages are required — a template with one side blank sends an empty message to half the school.",
                    "اللغتان مطلوبتان — القالب الذي يخلو أحد جانبيه يرسل رسالة فارغة إلى نصف المدرسة.");
                return RedirectToAction(nameof(Templates));
            }

            var unknown = TemplatePlaceholderRules.Unknown(eventCode, subjectAr, subjectEn, bodyAr, bodyEn);
            if (unknown.Count > 0)
            {
                TempData["Error"] = T(
                    $"This event does not supply: {string.Join(", ", unknown)}. Those would reach the reader as the words themselves.",
                    $"هذا الحدث لا يوفّر: {string.Join("، ", unknown)}. وستصل هذه إلى القارئ كما هي مكتوبة.");
                return RedirectToAction(nameof(Templates));
            }

            var version = await _config.DefineTemplateAsync(
                eventCode, channel, subjectAr, subjectEn, bodyAr, bodyEn, HttpContext.RequestAborted);

            TempData["Message"] = T(
                $"Version {version.VersionNumber} saved as a draft. Send a test before publishing it.",
                $"حُفظت النسخة {version.VersionNumber} كمسودة. أرسل اختباراً قبل نشرها.");

            return RedirectToAction(nameof(Template), new { id = version.TemplateId });
        }

        /// <summary>BR-NTF-001's mandatory test send — a real message on the template's own channel, to the person asking for it.</summary>
        [HttpPost("templates/{templateId:int}/test")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Templates, ActionVerb.Submit)]
        public async Task<IActionResult> TestTemplate(int templateId, int versionId)
        {
            try
            {
                await _ops.TestSendTemplateVersionAsync(versionId, _currentUser.UserId, HttpContext.RequestAborted);

                // Drained here rather than left for the scheduler: a test the operator has to
                // wait fifteen minutes for is a test nobody runs twice.
                await _dispatcher.DispatchQueuedAsync(HttpContext.RequestAborted);

                TempData["Message"] = T(
                    "Test sent to your own account. Check the delivery log for what the gateway said.",
                    "أُرسل الاختبار إلى حسابك. راجع سجل التسليم لمعرفة ردّ البوابة.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Template), new { id = templateId });
        }

        [HttpPost("templates/{templateId:int}/publish")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Templates, ActionVerb.Approve)]
        public async Task<IActionResult> PublishTemplate(int templateId, int versionId)
        {
            try
            {
                await _ops.PublishTemplateVersionAsync(versionId, HttpContext.RequestAborted);
                TempData["Message"] = T("Published. Every delivery from now renders this version.",
                                        "نُشرت. وكل تسليم من الآن يُبنى على هذه النسخة.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Template), new { id = templateId });
        }

        // ================================================================== §8.3 provider console

        [HttpGet("providers")]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Providers, ActionVerb.View)]
        public async Task<IActionResult> Providers()
            => View(new ProviderConsoleViewModel
            {
                Providers = await _ops.ListProvidersAsync(HttpContext.RequestAborted),
                DiallingCode = await _setup.GetSettingAsync(
                    SettingKeys.DefaultDiallingCode, cancellationToken: HttpContext.RequestAborted),
            });

        [HttpPost("providers")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Providers, ActionVerb.Configure)]
        public async Task<IActionResult> SaveProvider(
            int? id, NotificationChannel channel, string providerCode, string displayName,
            string? accountIdentifier, string? secret, string? senderId, string? apiBaseUrl, int failoverOrder, string? reason)
        {
            // Provider is T1-audited (BR-NTF-006): the reason rides the same ambient context
            // every other T1 screen uses. The token itself never reaches the trail — see
            // SecretFieldAttribute.
            _audit.Reason = reason;

            try
            {
                var saved = await _ops.SaveProviderAsync(
                    id, channel, providerCode, displayName, accountIdentifier, secret, senderId, apiBaseUrl,
                    failoverOrder, HttpContext.RequestAborted);

                TempData["Message"] = saved.IsConfigured
                    ? T("Gateway saved. Test it before a parent finds out for you.",
                        "حُفظت البوابة. اختبرها قبل أن يكتشف ولي أمر ذلك بدلاً عنك.")
                    : T("Gateway saved, but it is not yet usable — it still needs an account identifier, a token and a sender number.",
                        "حُفظت البوابة، لكنها غير صالحة للاستخدام بعد — ما زالت تحتاج معرّف حساب ورمز مصادقة ورقم مُرسِل.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Providers));
        }

        [HttpPost("providers/{id:int}/test")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Providers, ActionVerb.Configure)]
        public async Task<IActionResult> TestProvider(int id)
        {
            try
            {
                var outcome = await _ops.TestProviderAsync(id, HttpContext.RequestAborted);
                var provider = await _ops.GetProviderAsync(id, HttpContext.RequestAborted);

                if (outcome == ProviderTestOutcome.Passed)
                {
                    TempData["Message"] = T("The gateway accepted these credentials.", "قبلت البوابة هذه الاعتمادات.");
                }
                else
                {
                    // The gateway's own words, verbatim and untranslated: they are a support
                    // detail rather than a product refusal, and paraphrasing an error code into
                    // Arabic would make it unsearchable in the vendor's documentation.
                    TempData["Error"] = T("The gateway rejected these credentials: ", "رفضت البوابة هذه الاعتمادات: ")
                                        + (provider?.LastTestDetail ?? string.Empty);
                }
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Providers));
        }

        [HttpPost("providers/{id:int}/deactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Providers, ActionVerb.Deactivate)]
        public async Task<IActionResult> DeactivateProvider(int id, string? reason)
        {
            _audit.Reason = reason;
            try
            {
                await _ops.DeactivateProviderAsync(id, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Providers));
        }

        [HttpPost("providers/{id:int}/reactivate")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Providers, ActionVerb.Deactivate)]
        public async Task<IActionResult> ReactivateProvider(int id)
        {
            try
            {
                await _ops.ReactivateProviderAsync(id, HttpContext.RequestAborted);
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Providers));
        }

        // ================================================================== §8.4 delivery operations

        [HttpGet("deliveries")]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Deliveries, ActionVerb.View)]
        public async Task<IActionResult> Deliveries(DeliveryStatus? status = null, NotificationChannel? channel = null, string? eventCode = null)
            => View(new DeliveryOperationsViewModel
            {
                Rows = await _ops.ListDeliveriesAsync(status, channel, eventCode, 200, HttpContext.RequestAborted),
                Totals = await _ops.CountDeliveriesAsync(HttpContext.RequestAborted),
                Status = status,
                Channel = channel,
                EventCode = eventCode,
            });

        /// <summary>
        /// BR-NTF-005's retry, and the manual drain that goes with it. Post, not Edit: this
        /// runs the dispatcher, and on a metered channel it spends money.
        /// </summary>
        [HttpPost("deliveries/retry")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Deliveries, ActionVerb.Post)]
        public async Task<IActionResult> Retry(int[]? deliveryIds, DeliveryStatus? status, NotificationChannel? channel, string? eventCode)
        {
            var ids = deliveryIds ?? Array.Empty<int>();
            if (ids.Length == 0)
            {
                TempData["Error"] = T("Nothing was ticked.", "لم يُحدَّد شيء.");
                return RedirectToAction(nameof(Deliveries), new { status, channel, eventCode });
            }

            var requeued = await _ops.RetryDeliveriesAsync(ids, HttpContext.RequestAborted);
            var sent = requeued == 0 ? 0 : await _dispatcher.DispatchQueuedAsync(HttpContext.RequestAborted);

            TempData["Message"] = requeued == 0
                ? T("Nothing was retried — only failed deliveries can be.", "لم تُعَد أي محاولة — المحاولة تُعاد للتسليمات الفاشلة فقط.")
                : T($"{requeued} delivery(ies) requeued; {sent} processed.", $"أُعيدت {requeued} من التسليمات إلى الطابور، وعولج منها {sent}.");

            return RedirectToAction(nameof(Deliveries), new { status, channel, eventCode });
        }

        [HttpPost("deliveries/dispatch")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Deliveries, ActionVerb.Post)]
        public async Task<IActionResult> Dispatch(DeliveryStatus? status, NotificationChannel? channel, string? eventCode)
        {
            var processed = await _dispatcher.DispatchQueuedAsync(HttpContext.RequestAborted);
            TempData["Message"] = T($"{processed} queued delivery(ies) processed.", $"عولجت {processed} من التسليمات المنتظرة.");
            return RedirectToAction(nameof(Deliveries), new { status, channel, eventCode });
        }

        // ================================================================== §8.5 budget console

        [HttpGet("budgets")]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Budgets, ActionVerb.View)]
        public async Task<IActionResult> Budgets(string? period = null)
        {
            var periodKey = NormalizePeriod(period);
            return View(new BudgetConsoleViewModel
            {
                PeriodKey = periodKey,
                Rows = await _ops.ListBudgetsAsync(periodKey, HttpContext.RequestAborted),
            });
        }

        [HttpPost("budgets")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Notifications, ScreenCatalog.Notifications.Budgets, ActionVerb.Configure)]
        public async Task<IActionResult> SaveBudgets(int smsLimit, int whatsAppLimit, bool hardStop, string? period)
        {
            try
            {
                await _setup.SetSettingAsync(
                    SettingKeys.SmsMonthlyBudget, Math.Max(0, smsLimit).ToString(CultureInfo.InvariantCulture),
                    cancellationToken: HttpContext.RequestAborted);
                await _setup.SetSettingAsync(
                    SettingKeys.WhatsAppMonthlyBudget, Math.Max(0, whatsAppLimit).ToString(CultureInfo.InvariantCulture),
                    cancellationToken: HttpContext.RequestAborted);
                await _setup.SetSettingAsync(
                    SettingKeys.BudgetHardStop, hardStop ? "true" : "false", cancellationToken: HttpContext.RequestAborted);

                TempData["Message"] = T("Budget saved.", "حُفظت الميزانية.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Budgets), new { period });
        }

        /// <summary>
        /// A "yyyy-MM" the counters are keyed by. A hand-typed one that is not a month
        /// becomes this month rather than an empty console — the field is a convenience for
        /// looking back, not an input worth refusing over.
        /// </summary>
        private string NormalizePeriod(string? period)
            => DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
                ? parsed.ToString("yyyy-MM", CultureInfo.InvariantCulture)
                : _clock.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
    }
}
