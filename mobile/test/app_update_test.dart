import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_localizations/flutter_localizations.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:provider/provider.dart';
import 'package:sms_portal/app_version.dart';
import 'package:sms_portal/core/api_client.dart';
import 'package:sms_portal/l10n/strings.dart';
import 'package:sms_portal/models/app_update.dart';
import 'package:sms_portal/state/update_controller.dart';
import 'package:sms_portal/ui/widgets/update_banner.dart';

/// Telling a family their app is out of date — and, much more carefully, not
/// telling them so when nobody said it.
///
/// The failure worth writing tests against is not the banner failing to appear.
/// It is the opposite: an app that decides on its own that it is unsupported,
/// because a request timed out or a school is still running last month's server,
/// and shuts a parent out of their child's marks with a screen they cannot get
/// past. Silence is the safe answer to every question this cannot get an answer
/// to, and most of what follows pins that.
void main() {
  // ------------------------------------------------------------------ model

  group('the answer', () {
    test('reads what the endpoint sends', () {
      final AppUpdate update = AppUpdate.fromJson(<String, dynamic>{
        'published': true,
        'latestVersion': '1.2.0',
        'latestBuild': 3,
        'minimumVersion': '1.1.0',
        'minimumBuild': 2,
        'updateAvailable': true,
        'updateRequired': false,
        'installUrl': '/portal/app',
      });

      expect(update.published, isTrue);
      expect(update.latestVersion, '1.2.0');
      expect(update.latestBuild, 3);
      expect(update.minimumVersion, '1.1.0');
      expect(update.updateAvailable, isTrue);
      expect(update.updateRequired, isFalse);
      expect(update.installUrl, '/portal/app');
      // The same form the running build prints, so a family comparing the two
      // is comparing like with like.
      expect(update.latestLabel, '1.2.0 (3)');
    });

    test('a school that has published nothing says nothing', () {
      final AppUpdate update = AppUpdate.fromJson(<String, dynamic>{
        'published': false,
        'updateAvailable': false,
        'updateRequired': false,
        'installUrl': '/portal/app',
      });

      expect(update.published, isFalse);
      expect(update.latestLabel, isNull);
      expect(update.isSilent, isTrue);
    });

    test('a package whose name carried no version still parses', () {
      // MobileAppPackage serves a file it could not read a version out of, and
      // says so by sending none. "Published, version unknown" must not become
      // "1.2.0 (null)".
      final AppUpdate update = AppUpdate.fromJson(<String, dynamic>{
        'published': true,
        'latestVersion': null,
        'latestBuild': null,
        'updateAvailable': false,
        'updateRequired': false,
        'installUrl': '/portal/app',
      });

      expect(update.published, isTrue);
      expect(update.latestLabel, isNull);
    });

    test('a version without a build number prints without one', () {
      final AppUpdate update = AppUpdate.fromJson(<String, dynamic>{
        'latestVersion': '1.2.0',
        'installUrl': '/portal/app',
      });

      expect(update.latestLabel, '1.2.0');
    });

    test('an app that could not ask is silent, never demanding', () {
      expect(AppUpdate.unknown.updateAvailable, isFalse);
      expect(AppUpdate.unknown.updateRequired, isFalse);
      expect(AppUpdate.unknown.isSilent, isTrue);
    });
  });

  // ------------------------------------------------------------- controller

  group('asking the school', () {
    UpdateController controllerAnswering(
      http.Response Function(http.Request request) respond, {
      String baseUrl = 'http://school.test',
    }) {
      return UpdateController(
        clientFactory: (String url) => ApiClient(
          baseUrl: url,
          inner: MockClient((http.Request request) async => respond(request)),
        ),
      );
    }

    Future<void> ask(UpdateController controller) => controller.check(
          baseUrl: 'http://school.test',
          languageCode: 'ar',
        );

    String body(Map<String, dynamic> json) => jsonEncode(json);

    test('sends the running build, split across two parameters', () async {
      late http.Request seen;
      final UpdateController controller = controllerAnswering((request) {
        seen = request;
        return http.Response(body(<String, dynamic>{}), 200);
      });

      await ask(controller);

      expect(seen.url.path, '/api/v1/app/version');
      // Not one `1.1.0+2` string: `+` in a query decodes to a space, and the
      // build number would silently arrive missing rather than wrong.
      expect(seen.url.queryParameters['version'], kAppVersion);
      expect(seen.url.queryParameters['build'], '$kAppBuild');
    });

    test('offers a newer build', () async {
      final UpdateController controller = controllerAnswering(
        (_) => http.Response(
          body(<String, dynamic>{
            'published': true,
            'latestVersion': '1.2.0',
            'latestBuild': 3,
            'updateAvailable': true,
            'updateRequired': false,
            'installUrl': '/portal/app',
          }),
          200,
        ),
      );

      await ask(controller);

      expect(controller.shouldOffer, isTrue);
      expect(controller.mustUpdate, isFalse);
      // Relative on the wire, absolute here — the app is the only side that
      // knows which school it is talking to.
      expect(controller.installUri.toString(),
          'http://school.test/portal/app');
    });

    test('a required update is not also offered as a banner', () async {
      final UpdateController controller = controllerAnswering(
        (_) => http.Response(
          body(<String, dynamic>{
            'published': true,
            'latestVersion': '1.2.0',
            'latestBuild': 3,
            'updateAvailable': true,
            'updateRequired': true,
            'installUrl': '/portal/app',
          }),
          200,
        ),
      );

      await ask(controller);

      expect(controller.mustUpdate, isTrue);
      // The blocking screen owns this case. A banner behind it would be a second
      // copy of the same demand, dismissible, on a screen nobody can reach.
      expect(controller.shouldOffer, isFalse);
    });

    test('a school one server version behind is silent, not out of date',
        () async {
      // No such endpoint yet: this deployment has the app but not the half that
      // answers it. That is the school being behind, not the phone.
      // `.bytes` rather than the string constructor: that one encodes Latin-1,
      // which cannot hold the Arabic the API actually answers with.
      final UpdateController controller = controllerAnswering(
        (_) => http.Response.bytes(
          utf8.encode(body(<String, dynamic>{
            'error': <String, dynamic>{
              'code': 'not_found',
              'message': 'غير موجود.',
            },
          })),
          404,
        ),
      );

      await ask(controller);

      expect(controller.shouldOffer, isFalse);
      expect(controller.mustUpdate, isFalse);
    });

    test('a school that cannot be reached never blocks the app', () async {
      // The important one. A timeout must look exactly like a school with
      // nothing to say — the alternative is a family locked out of their child's
      // marks by a request that never arrived.
      final UpdateController controller = UpdateController(
        clientFactory: (String url) => ApiClient(
          baseUrl: url,
          inner: MockClient(
            (_) async => throw http.ClientException('no route to host'),
          ),
        ),
      );

      await ask(controller);

      expect(controller.mustUpdate, isFalse);
      expect(controller.shouldOffer, isFalse);
    });

    test('an answer that is not JSON at all is silent', () async {
      // A captive portal or a proxy answering with its own HTML page.
      final UpdateController controller =
          controllerAnswering((_) => http.Response('<html>hello</html>', 200));

      await ask(controller);

      expect(controller.mustUpdate, isFalse);
      expect(controller.shouldOffer, isFalse);
    });

    test('a dismissal covers that version and not the next one', () {
      final UpdateController controller = UpdateController()
        ..seed(const AppUpdate(
          published: true,
          latestVersion: '1.2.0',
          latestBuild: 3,
          minimumVersion: null,
          updateAvailable: true,
          updateRequired: false,
          installUrl: '/portal/app',
        ));

      expect(controller.shouldOffer, isTrue);
      controller.dismiss();
      expect(controller.shouldOffer, isFalse);

      // The school publishes again. A dismissal of 1.2.0 must not silence 1.3.0.
      controller.seed(const AppUpdate(
        published: true,
        latestVersion: '1.3.0',
        latestBuild: 4,
        minimumVersion: null,
        updateAvailable: true,
        updateRequired: false,
        installUrl: '/portal/app',
      ));
      expect(controller.shouldOffer, isTrue);
    });
  });

  // ----------------------------------------------------------------- screen

  group('the banner', () {
    Widget host(Widget child, UpdateController update, String lang) {
      return ChangeNotifierProvider<UpdateController>.value(
        value: update,
        child: MaterialApp(
          locale: Locale(lang),
          supportedLocales: Strings.supportedLocales,
          localizationsDelegates: const <LocalizationsDelegate<Object>>[
            GlobalMaterialLocalizations.delegate,
            GlobalWidgetsLocalizations.delegate,
            GlobalCupertinoLocalizations.delegate,
          ],
          home: Scaffold(body: child),
        ),
      );
    }

    UpdateController offering() => UpdateController()
      ..seed(
        const AppUpdate(
          published: true,
          latestVersion: '1.2.0',
          latestBuild: 3,
          minimumVersion: null,
          updateAvailable: true,
          updateRequired: false,
          installUrl: '/portal/app',
        ),
        installUri: Uri.parse('http://school.test/portal/app'),
      );

    testWidgets('draws nothing when there is nothing to say', (tester) async {
      await tester.pumpWidget(
        host(const UpdateBanner(), UpdateController(), 'en'),
      );

      expect(find.byType(FilledButton), findsNothing);
      expect(find.text(const Strings(false).updateAvailableTitle), findsNothing);
    });

    testWidgets('says so in English, with both builds', (tester) async {
      await tester.pumpWidget(host(const UpdateBanner(), offering(), 'en'));

      const Strings en = Strings(false);
      expect(find.text(en.updateAvailableTitle), findsOneWidget);
      expect(find.text(en.updateNow), findsOneWidget);
      // What is installed and what is waiting, so the family can tell afterwards
      // whether the install took.
      expect(find.text(appVersionLabel), findsOneWidget);
      expect(find.text('1.2.0 (3)'), findsOneWidget);
    });

    testWidgets('says so in Arabic', (tester) async {
      await tester.pumpWidget(host(const UpdateBanner(), offering(), 'ar'));

      const Strings ar = Strings(true);
      expect(find.text(ar.updateAvailableTitle), findsOneWidget);
      expect(find.text(ar.updateNow), findsOneWidget);
      // The screen flips to RTL with the locale; the version numbers do not.
      final Text installed = tester.widget<Text>(find.text('1.2.0 (3)'));
      expect(installed.textDirection, TextDirection.ltr);
    });

    testWidgets('goes away when the family waves it off', (tester) async {
      final UpdateController update = offering();
      await tester.pumpWidget(host(const UpdateBanner(), update, 'en'));

      expect(find.text(const Strings(false).updateNow), findsOneWidget);

      await tester.tap(find.byIcon(Icons.close_rounded));
      await tester.pump();

      expect(find.text(const Strings(false).updateNow), findsNothing);
    });
  });
}
