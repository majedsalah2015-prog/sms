import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';

import 'app.dart';
import 'state/auth_controller.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  // Arabic month names come from here. Without it `DateFormat.yMMMd('ar')`
  // throws on first use — and it would throw on a results screen, in a school,
  // rather than in a test.
  await initializeDateFormatting();

  final AuthController auth = AuthController();

  // Not awaited: the splash is the whole point of `AuthStage.restoring`, and
  // blocking `runApp` on the keystore is a black screen on a cold start.
  unawaited(auth.restore());

  runApp(
    ChangeNotifierProvider<AuthController>.value(
      value: auth,
      child: const SmsPortalApp(),
    ),
  );
}
