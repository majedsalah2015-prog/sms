import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:provider/provider.dart';

import 'l10n/strings.dart';
import 'state/auth_controller.dart';
import 'state/update_controller.dart';
import 'ui/theme.dart';
import 'ui/change_password_page.dart';
import 'ui/home_page.dart';
import 'ui/login_page.dart';
import 'ui/two_factor_page.dart';
import 'ui/update_required_page.dart';

/// The shell.
///
/// Direction is not set here: `MaterialApp` derives it from [Locale], so
/// choosing Arabic flips the whole tree to RTL the same way `_Layout.cshtml`
/// swaps in `bootstrap.rtl.min.css` for the browser. Anything that must stay
/// left-to-right inside an Arabic screen — an amount, a time range — says so at
/// the widget that shows it, not here.
class SmsPortalApp extends StatefulWidget {
  const SmsPortalApp({super.key});

  @override
  State<SmsPortalApp> createState() => _SmsPortalAppState();
}

class _SmsPortalAppState extends State<SmsPortalApp> {
  /// The address the school was last asked about its published build. Held so
  /// the question is asked once per school rather than on every rebuild — and
  /// asked again the moment a family corrects the address on the sign-in screen,
  /// because the first answer came from somewhere that was not their school.
  String? _askedFor;

  @override
  Widget build(BuildContext context) {
    final AuthController auth = context.watch<AuthController>();
    final UpdateController update = context.watch<UpdateController>();

    _askAboutUpdates(auth);

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
      // A build the school has stopped supporting stands in front of everything,
      // sign-in included. That is the whole difference between this and the
      // banner on the family screen: a demand a family can scroll past is not a
      // demand, and the case this exists for at its sharpest is a build too old
      // to sign in at all — where every screen behind it would only fail.
      home: update.mustUpdate
          ? const UpdateRequiredPage()
          : switch (auth.stage) {
              AuthStage.restoring => const _Splash(),
              AuthStage.signedOut => const LoginPage(),
              AuthStage.twoFactorPending => const TwoFactorPage(),
              AuthStage.mustChangePassword =>
                const ChangePasswordPage(forced: true),
              AuthStage.signedIn => const HomePage(),
            },
    );
  }

  /// Asks the school what it is publishing, once the address is settled.
  ///
  /// Not while [AuthStage.restoring]: the keystore is still being read and
  /// `baseUrl` is the built-in default until it finishes, so asking then would
  /// put the question to the wrong host and spend a request finding out.
  ///
  /// The check is silent about its own failures — an offline phone and a school
  /// running a server one version behind both look like a school with nothing to
  /// say, which is the only safe reading. The alternative is an app that locks a
  /// family out because a request did not arrive.
  void _askAboutUpdates(AuthController auth) {
    if (auth.stage == AuthStage.restoring) return;
    if (_askedFor == auth.baseUrl) return;
    _askedFor = auth.baseUrl;

    final UpdateController update = context.read<UpdateController>();
    final String baseUrl = auth.baseUrl;
    final String languageCode = auth.languageCode;
    // After the frame: this is reached from build(), and a controller that
    // notified its listeners mid-build would rebuild the tree it is inside.
    WidgetsBinding.instance.addPostFrameCallback((_) {
      unawaited(update.check(baseUrl: baseUrl, languageCode: languageCode));
    });
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
