using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Sms.Application.Common.Exceptions;
using Sms.Application.Common.Interfaces;
using Sms.Application.Messaging;
using Sms.Application.Notifications;
using Sms.Application.Security;
using Sms.Domain.Messaging;
using Sms.Domain.Notifications;
using Sms.Domain.Security;
using Sms.Web.Models;
using Sms.Web.Security;

namespace Sms.Web.Controllers
{
    /// <summary>
    /// Module 32's compose screen (doc/Modules/32 §8.1): the bilingual editor, the audience
    /// builder with its live count, the channel picker with a cost estimate, and the
    /// approval submission BR-MSG-001 requires above section level.
    /// <para>
    /// <b>Only §8.1.</b> The threads inbox (§8.2), the communication-matrix config (§8.3),
    /// the official-letter centre (§8.4), the moderation queue (§8.5) and the archive
    /// search (§8.6) are not built here and have no permission catalogued — an entry that
    /// opens nothing is a grant a school cannot use. §8.4 is additionally blocked: a
    /// numbered official letter is a rendered document, and the PDF engine remains an open
    /// owner decision (docs/Status, O6).
    /// </para>
    /// <para>
    /// <b>Scheduled sending is not built either.</b> §8.1 lists it; an announcement here
    /// sends when the button is pressed. Nothing in the model records an intended time, so
    /// this is an admitted gap rather than a half-built one.
    /// </para>
    /// </summary>
    [Route("messaging")]
    public class MessagingController : Controller
    {
        private static bool IsArabic => CultureInfo.CurrentUICulture.TextInfo.IsRightToLeft;

        private static string T(string en, string ar) => IsArabic ? ar : en;

        private readonly IMessagingAdmin _messaging;
        private readonly INotificationOpsAdmin _ops;
        private readonly INotificationDispatcher _dispatcher;
        private readonly IClock _clock;

        public MessagingController(
            IMessagingAdmin messaging, INotificationOpsAdmin ops, INotificationDispatcher dispatcher, IClock clock)
        {
            _messaging = messaging;
            _ops = ops;
            _dispatcher = dispatcher;
            _clock = clock;
        }

        [HttpGet("")]
        [RequirePermission(ScreenCatalog.Modules.Messaging, ScreenCatalog.Messaging.Announcements, ActionVerb.View)]
        public async Task<IActionResult> Index()
            => View(new AnnouncementListViewModel
            {
                Announcements = await _messaging.ListAnnouncementsAsync(HttpContext.RequestAborted),
            });

        /// <summary>
        /// The compose form. The scope's targets and the audience count are resolved on
        /// every load rather than by script, so the number on the screen is the number the
        /// send will use — a count computed one way and a send performed another is how a
        /// school learns it messaged the wrong grade.
        /// </summary>
        [HttpGet("compose")]
        [RequirePermission(ScreenCatalog.Modules.Messaging, ScreenCatalog.Messaging.Announcements, ActionVerb.Create)]
        public async Task<IActionResult> Compose(
            AudienceScope scope = AudienceScope.Section, int? targetId = null, int channelMask = 0)
            => View(await BuildComposeAsync(new ComposeAnnouncementViewModel
            {
                Scope = scope,
                TargetId = targetId,
                ChannelMask = channelMask,
            }));

        [HttpPost("compose")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Messaging, ScreenCatalog.Messaging.Announcements, ActionVerb.Create)]
        public async Task<IActionResult> Compose(ComposeAnnouncementViewModel form, int[]? channelPicks)
        {
            // The picker posts one bit per ticked box rather than a packed integer, so a box
            // that is not ticked is simply absent — the same shape the permission grid uses,
            // and what makes the post idempotent rather than a diff against what was there.
            form.ChannelMask = (channelPicks ?? Array.Empty<int>()).Aggregate(0, (mask, bit) => mask | bit);

            if (string.IsNullOrWhiteSpace(form.TitleAr) || string.IsNullOrWhiteSpace(form.TitleEn)
                || string.IsNullOrWhiteSpace(form.BodyAr) || string.IsNullOrWhiteSpace(form.BodyEn))
            {
                ModelState.AddModelError(string.Empty, T(
                    "Both languages are required. A parent who reads only one of them would receive a blank announcement.",
                    "اللغتان مطلوبتان. فولي الأمر الذي يقرأ إحداهما فقط سيستلم إعلاناً فارغاً."));
                return View(await BuildComposeAsync(form));
            }

            try
            {
                var announcement = await _messaging.DefineAnnouncementAsync(
                    form.TitleAr!, form.TitleEn!, form.BodyAr!, form.BodyEn!,
                    form.Scope, form.Scope == AudienceScope.SchoolWide ? null : form.TargetId, form.ChannelMask,
                    HttpContext.RequestAborted);

                TempData["Message"] = announcement.Status == AnnouncementStatus.PendingApproval
                    ? T("Saved and submitted for approval — anything above a single section needs one (BR-MSG-001).",
                        "حُفظ وأُرسل للاعتماد — فما فوق الشعبة الواحدة يحتاج اعتماداً (BR-MSG-001).")
                    : T("Saved. Send it when you are ready.", "حُفظ. أرسله متى شئت.");

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                ModelState.AddModelError(string.Empty, UserMessage.For(ex, IsArabic));
                return View(await BuildComposeAsync(form));
            }
        }

        [HttpPost("{id:int}/approve")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Messaging, ScreenCatalog.Messaging.Announcements, ActionVerb.Approve)]
        public async Task<IActionResult> Approve(int id)
        {
            try
            {
                await _messaging.ApproveAnnouncementAsync(id, HttpContext.RequestAborted);
                TempData["Message"] = T("Approved. It can be sent now.", "اعتُمد. ويمكن إرساله الآن.");
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// Resolves the audience, queues the deliveries and drains them. Post rather than
        /// Create: on WhatsApp or SMS this spends the school's money, one message per
        /// guardian, and the budget is checked before it does (BR-NTF-004).
        /// </summary>
        [HttpPost("{id:int}/send")]
        [ValidateAntiForgeryToken]
        [RequirePermission(ScreenCatalog.Modules.Messaging, ScreenCatalog.Messaging.Announcements, ActionVerb.Post)]
        public async Task<IActionResult> Send(int id)
        {
            var announcement = await _messaging.GetAnnouncementAsync(id, HttpContext.RequestAborted);
            if (announcement == null)
            {
                return NotFound();
            }

            var blocked = await BudgetRefusalAsync(announcement, HttpContext.RequestAborted);
            if (blocked != null)
            {
                TempData["Error"] = blocked;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var reach = await _messaging.SendAnnouncementAsync(id, HttpContext.RequestAborted);
                var processed = await _dispatcher.DispatchQueuedAsync(HttpContext.RequestAborted);

                TempData["Message"] = T(
                    $"Sent to {reach} guardian(s); {processed} delivery(ies) processed. The delivery log has what each channel said.",
                    $"أُرسل إلى {reach} من أولياء الأمور، وعولج {processed} تسليماً. وسجل التسليم يبيّن ردّ كل قناة.");
            }
            catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
            {
                TempData["Error"] = UserMessage.For(ex, IsArabic);
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// BR-NTF-004's hard stop, applied where the money is actually spent. An
        /// announcement is never a safety class — those are doc 09's events, raised by a
        /// module, not composed by a person — so it has no exemption, and the refusal is
        /// stated in the reader's language rather than raised from the engine.
        /// </summary>
        private async Task<string?> BudgetRefusalAsync(Announcement announcement, System.Threading.CancellationToken cancellationToken)
        {
            var costed = AnnouncementChannels.Costed
                .Where(c => AnnouncementChannels.Includes(announcement.ChannelMask, c))
                .ToList();

            if (costed.Count == 0)
            {
                return null;
            }

            var periodKey = _clock.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var budgets = await _ops.ListBudgetsAsync(periodKey, cancellationToken);

            foreach (var channel in costed)
            {
                var row = budgets.FirstOrDefault(b => b.Channel == channel);
                if (row is { HardStopEnabled: true, IsOverLimit: true })
                {
                    return T(
                        $"The {ChannelName(channel, false)} budget for {periodKey} is spent and the hard stop is on. Raise the ceiling in the budget console, or send without that channel.",
                        $"استُنفدت ميزانية {ChannelName(channel, true)} لشهر {periodKey} والإيقاف الصارم مفعَّل. ارفع السقف من كونسول الميزانية، أو أرسل دون هذه القناة.");
                }
            }

            return null;
        }

        private static string ChannelName(NotificationChannel channel, bool arabic) => channel switch
        {
            NotificationChannel.Sms => arabic ? "الرسائل النصية" : "SMS",
            NotificationChannel.WhatsApp => arabic ? "واتساب" : "WhatsApp",
            NotificationChannel.Email => arabic ? "البريد الإلكتروني" : "email",
            _ => arabic ? "داخل النظام" : "in-app",
        };

        private async Task<ComposeAnnouncementViewModel> BuildComposeAsync(ComposeAnnouncementViewModel form)
        {
            form.Targets = await _messaging.ListAudienceTargetsAsync(form.Scope, HttpContext.RequestAborted);

            // A target that is not in the picker (a stale link, a switched scope) is dropped
            // rather than carried: previewing one audience and sending to another is the
            // failure this screen exists to prevent.
            if (form.Scope != AudienceScope.SchoolWide
                && form.TargetId is { } targetId
                && form.Targets.All(t => t.Id != targetId))
            {
                form.TargetId = null;
            }

            if (form.Scope == AudienceScope.SchoolWide || form.TargetId != null)
            {
                form.Preview = await _messaging.PreviewAudienceAsync(
                    form.Scope,
                    form.Scope == AudienceScope.SchoolWide ? null : form.TargetId,
                    form.ChannelMask,
                    HttpContext.RequestAborted);
            }

            form.Providers = await _ops.ListProvidersAsync(HttpContext.RequestAborted);
            return form;
        }
    }
}
