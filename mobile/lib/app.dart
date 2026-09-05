import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'l10n/strings.dart';
import 'state/auth_controller.dart';
import 'ui/theme.dart';
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
      theme: AppTheme.light(),
      darkTheme: AppTheme.dark(),
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

}

/// Shown while the keystore is read. It carries the school's mark rather than a
/// bare spinner, because on a cold start this is the first thing a parent sees
/// and a blank screen with a wheel on it belongs to no product in particular.
class _Splash extends StatelessWidget {
  const _Splash();

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: <Widget>[
            Container(
              width: 72,
              height: 72,
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(22),
              ),
              child: const Icon(
                Icons.school_rounded,
                size: 38,
                color: Colors.white,
              ),
            ),
            const SizedBox(height: 20),
            Text(
              s.appTitle,
              style: Theme.of(context).textTheme.titleMedium?.copyWith(
                    fontWeight: FontWeight.w600,
                  ),
            ),
            const SizedBox(height: 24),
            const SizedBox(
              width: 22,
              height: 22,
              child: CircularProgressIndicator(strokeWidth: 2.5),
            ),
          ],
        ),
      ),
    );
  }
}
