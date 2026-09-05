import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';

import '../../app_version.dart';
import '../../l10n/strings.dart';
import '../../state/update_controller.dart';
import '../theme.dart';

/// Hands the family to the school's own app page, in a browser.
///
/// The app deliberately does not fetch and install the package itself. Doing so
/// would need `REQUEST_INSTALL_PACKAGES` in the manifest — the permission to
/// install software — so that this app could do what one browser tab already
/// does; and the page on the other end is the only place the "allow installs
/// from this browser" prompt is explained, in both languages, beside the file's
/// size and date. A second copy of that explanation inside the app is one that
/// goes stale on its own.
///
/// Failure is shown rather than swallowed: a phone with no browser that can
/// take the address is rare, but a button that silently does nothing is the
/// worst possible answer on the screen that is asking someone to act.
Future<void> openInstallPage(BuildContext context) async {
  final Strings s = Strings.of(context);
  final UpdateController update = context.read<UpdateController>();
  // Read before the await: the messenger must not be looked up through a
  // BuildContext that may no longer be mounted afterwards.
  final ScaffoldMessengerState messenger = ScaffoldMessenger.of(context);
  final Uri? target = update.installUri;

  bool opened = false;
  if (target != null) {
    try {
      opened = await launchUrl(target, mode: LaunchMode.externalApplication);
    } on Exception {
      // `launchUrl` throws rather than returning false when the platform has no
      // handler at all, and in a test there is no plugin behind it. Both mean
      // the same thing to the family, and neither is worth a crash.
      opened = false;
    }
  }

  if (!opened) {
    messenger.showSnackBar(SnackBar(content: Text(s.updateOpenFailed)));
  }
}

/// "A new version is available", above the family screen.
///
/// Dismissible, because this is the *optional* half: the school has published
/// something newer but still accepts this build. The blocking half lives in
/// `UpdateRequiredPage` and is not a banner at all — a demand that can be
/// scrolled past is not a demand.
///
/// Renders nothing at all when there is nothing to say, so a caller can place it
/// unconditionally.
class UpdateBanner extends StatelessWidget {
  const UpdateBanner({super.key});

  @override
  Widget build(BuildContext context) {
    final UpdateController update = context.watch<UpdateController>();
    if (!update.shouldOffer) return const SizedBox.shrink();

    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    const Color tone = AppColors.primary;

    return Padding(
      padding: const EdgeInsets.fromLTRB(16, 16, 16, 0),
      child: Container(
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: Color.alphaBlend(tone.withValues(alpha: 0.08), Colors.white),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: Color.alphaBlend(tone.withValues(alpha: 0.25), Colors.white),
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                const Icon(Icons.system_update_rounded, size: 20, color: tone),
                const SizedBox(width: 10),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Text(
                        s.updateAvailableTitle,
                        style: theme.textTheme.bodyMedium?.copyWith(
                          fontWeight: FontWeight.w600,
                          color: tone,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        s.updateAvailableBody,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: AppColors.muted, height: 1.4),
                      ),
                      const SizedBox(height: 6),
                      const VersionStep(),
                    ],
                  ),
                ),
                // The dismissal is this run only and this version only, so a
                // school that publishes again is not silenced by it.
                IconButton(
                  onPressed: update.dismiss,
                  icon: const Icon(Icons.close_rounded, size: 18),
                  color: AppColors.muted,
                  tooltip: s.updateLater,
                  visualDensity: VisualDensity.compact,
                ),
              ],
            ),
            const SizedBox(height: 6),
            Align(
              alignment: AlignmentDirectional.centerStart,
              child: FilledButton.icon(
                style: FilledButton.styleFrom(
                  minimumSize: const Size(0, 40),
                  padding: const EdgeInsets.symmetric(horizontal: 18),
                ),
                onPressed: () => openInstallPage(context),
                icon: const Icon(Icons.download_rounded, size: 18),
                label: Text(s.updateNow),
              ),
            ),
          ],
        ),
      ),
    );
  }
}

/// `1.1.0 (2) → 1.2.0 (3)` — what is installed and what is waiting.
///
/// Forced left-to-right in both languages, like every other figure in this
/// product. An Arabic sentence with two version numbers and an arrow inside it
/// lays the arrow out against the numbers, and the reader is told to move from
/// the new build to the old one.
class VersionStep extends StatelessWidget {
  const VersionStep({super.key});

  @override
  Widget build(BuildContext context) {
    final UpdateController update = context.watch<UpdateController>();
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final String? latest = update.update.latestLabel;

    final TextStyle? label =
        theme.textTheme.labelSmall?.copyWith(color: AppColors.muted);
    final TextStyle? value = theme.textTheme.bodySmall?.copyWith(
      fontWeight: FontWeight.w600,
      color: AppColors.primary,
    );

    return Wrap(
      spacing: 14,
      runSpacing: 4,
      children: <Widget>[
        _Pair(label: s.updateInstalled, value: appVersionLabel,
            labelStyle: label, valueStyle: theme.textTheme.bodySmall),
        if (latest != null)
          _Pair(label: s.updateNewest, value: latest,
              labelStyle: label, valueStyle: value),
      ],
    );
  }
}

class _Pair extends StatelessWidget {
  const _Pair({
    required this.label,
    required this.value,
    required this.labelStyle,
    required this.valueStyle,
  });

  final String label;
  final String value;
  final TextStyle? labelStyle;
  final TextStyle? valueStyle;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Text(label, style: labelStyle),
        const SizedBox(width: 5),
        Text(value, textDirection: TextDirection.ltr, style: valueStyle),
      ],
    );
  }
}
