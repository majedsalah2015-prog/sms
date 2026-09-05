import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../app_version.dart';
import '../../core/api_exception.dart';
import '../../l10n/strings.dart';
import '../../state/auth_controller.dart';
import '../theme.dart';
import 'async_view.dart';

/// The frame the three sign-in screens share: the school's mark, the language
/// switch, a centred card, and one place for the server's refusal to appear.
class AuthScaffold extends StatelessWidget {
  const AuthScaffold({
    required this.title,
    required this.children,
    this.icon = Icons.school_rounded,
    this.subtitle,
    this.error,
    super.key,
  });

  final String title;
  final String? subtitle;

  /// The glyph above the card — what this particular step is about.
  final IconData icon;

  final List<Widget> children;

  /// The last failure, if any. Its message is the server's own and is shown
  /// verbatim (docs/Integration/03-Mobile-API.md §3).
  final Object? error;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();
    final ThemeData theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: Text(s.appTitle),
        actions: <Widget>[
          TextButton.icon(
            onPressed: () => auth.setLanguage(auth.isArabic ? 'en' : 'ar'),
            icon: const Icon(Icons.language_rounded, size: 18),
            label: Text(s.languageToggle),
          ),
        ],
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.all(24),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 440),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: <Widget>[
                  Center(
                    child: Container(
                      width: 68,
                      height: 68,
                      decoration: BoxDecoration(
                        gradient: const LinearGradient(
                          colors: <Color>[
                            AppColors.primary,
                            AppColors.primaryHover,
                          ],
                          begin: Alignment.topLeft,
                          end: Alignment.bottomRight,
                        ),
                        borderRadius: BorderRadius.circular(20),
                      ),
                      child: Icon(icon, size: 32, color: Colors.white),
                    ),
                  ),
                  const SizedBox(height: 20),
                  Text(
                    title,
                    textAlign: TextAlign.center,
                    style: theme.textTheme.headlineSmall
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  if (subtitle != null) ...<Widget>[
                    const SizedBox(height: 8),
                    Text(
                      subtitle!,
                      textAlign: TextAlign.center,
                      style: theme.textTheme.bodyMedium
                          ?.copyWith(color: AppColors.muted, height: 1.5),
                    ),
                  ],
                  const SizedBox(height: 24),
                  if (error != null) ...<Widget>[
                    _ErrorBanner(error: error!),
                    const SizedBox(height: 16),
                  ],
                  Card(
                    child: Padding(
                      padding: const EdgeInsets.all(20),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: children,
                      ),
                    ),
                  ),
                  const SizedBox(height: 20),
                  // So a family asked to install an update can answer "did it
                  // install?" from the first screen, without help from the school.
                  Center(
                    child: Text(
                      appVersionLabel,
                      textDirection: TextDirection.ltr,
                      style: theme.textTheme.bodySmall
                          ?.copyWith(color: AppColors.muted),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _ErrorBanner extends StatelessWidget {
  const _ErrorBanner({required this.error});

  final Object error;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final Object e = error;
    final bool offline = e is ApiUnreachableException;
    final Color tone = offline ? AppColors.warning : AppColors.danger;

    // `validation_failed` carries a sentence per field. The server wrote all of
    // them in the caller's language, so they are listed rather than collapsed
    // into the first one.
    final List<String> lines = <String>[
      FailureView.messageFor(e, s),
      if (e is ApiException && e.fields != null)
        for (final List<String> messages in e.fields!.values) ...messages,
    ];

    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: Color.alphaBlend(tone.withValues(alpha: 0.08), Colors.white),
        borderRadius: BorderRadius.circular(14),
        border: Border.all(
          color: Color.alphaBlend(tone.withValues(alpha: 0.25), Colors.white),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Icon(
            offline ? Icons.wifi_off_rounded : Icons.error_outline_rounded,
            size: 20,
            color: tone,
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                for (final String line in lines)
                  if (line.trim().isNotEmpty)
                    Padding(
                      padding: const EdgeInsets.symmetric(vertical: 2),
                      child: Text(
                        line,
                        style: theme.textTheme.bodyMedium
                            ?.copyWith(color: tone, height: 1.4),
                      ),
                    ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
