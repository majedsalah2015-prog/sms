import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../core/api_exception.dart';
import '../../l10n/strings.dart';
import '../../state/auth_controller.dart';
import 'async_view.dart';

/// The frame the three sign-in screens share: the school's name, the language
/// switch, a centred card, and one place for the server's refusal to appear.
class AuthScaffold extends StatelessWidget {
  const AuthScaffold({
    required this.title,
    required this.children,
    this.subtitle,
    this.error,
    super.key,
  });

  final String title;
  final String? subtitle;
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
          TextButton(
            onPressed: () => auth.setLanguage(auth.isArabic ? 'en' : 'ar'),
            child: Text(s.languageToggle),
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
                  Text(title, style: theme.textTheme.headlineSmall),
                  if (subtitle != null) ...<Widget>[
                    const SizedBox(height: 8),
                    Text(
                      subtitle!,
                      style: theme.textTheme.bodyMedium?.copyWith(
                        color: theme.colorScheme.onSurfaceVariant,
                      ),
                    ),
                  ],
                  const SizedBox(height: 24),
                  if (error != null) ...<Widget>[
                    _ErrorBanner(error: error!),
                    const SizedBox(height: 16),
                  ],
                  ...children,
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
        color: theme.colorScheme.errorContainer,
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          for (final String line in lines)
            if (line.trim().isNotEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 2),
                child: Text(
                  line,
                  style: theme.textTheme.bodyMedium?.copyWith(
                    color: theme.colorScheme.onErrorContainer,
                  ),
                ),
              ),
        ],
      ),
    );
  }
}
