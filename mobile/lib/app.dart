import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'l10n/strings.dart';
import 'state/auth_controller.dart';
import 'ui/change_password_page.dart';
import 'ui/home_page.dart';
import 'ui/login_page.dart';
import 'ui/two_factor_page.dart';

/// The shell.
///
/// Direction is not set here: `MaterialApp` derives it from [Locale], so
/// choosing Arabic flips the whole tree to RTL the same way `_Layout.cshtml`
/// swaps in `bootstrap.rtl.min.css` for the browser. Anything that must stay
/// left-to-right inside an Arabic screen — an amount, a time range — says so at
/// the widget that shows it, not here.
class SmsPortalApp extends StatelessWidget {
  const SmsPortalApp({super.key});

  @override
  Widget build(BuildContext context) {
    final AuthController auth = context.watch<AuthController>();

    return MaterialApp(
      onGenerateTitle: (BuildContext context) => Strings.of(context).appTitle,
      debugShowCheckedModeBanner: false,
      locale: Locale(auth.languageCode),
      supportedLocales: Strings.supportedLocales,
      localizationsDelegates: const <LocalizationsDelegate<Object>>[
        GlobalMaterialLocalizations.delegate,
        GlobalWidgetsLocalizations.delegate,
        GlobalCupertinoLocalizations.delegate,
      ],
      theme: _theme(Brightness.light),
      darkTheme: _theme(Brightness.dark),
      home: switch (auth.stage) {
        AuthStage.restoring => const _Splash(),
        AuthStage.signedOut => const LoginPage(),
        AuthStage.twoFactorPending => const TwoFactorPage(),
        AuthStage.mustChangePassword =>
          const ChangePasswordPage(forced: true),
        AuthStage.signedIn => const HomePage(),
      },
    );
  }

  static ThemeData _theme(Brightness brightness) {
    final ColorScheme scheme = ColorScheme.fromSeed(
      seedColor: const Color(0xFF0F5C4A),
      brightness: brightness,
    );
    return ThemeData(
      colorScheme: scheme,
      // Cairo and Tajawal are not bundled: shipping a font is a licence
      // decision the school makes, and Android's own Arabic face renders the
      // product's text correctly without one.
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(14),
          side: BorderSide(color: scheme.outlineVariant),
        ),
      ),
      inputDecorationTheme: const InputDecorationTheme(
        border: OutlineInputBorder(),
      ),
    );
  }
}

class _Splash extends StatelessWidget {
  const _Splash();

  @override
  Widget build(BuildContext context) {
    return const Scaffold(
      body: Center(child: CircularProgressIndicator()),
    );
  }
}
