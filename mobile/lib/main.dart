import 'dart:async';

import 'package:flutter/material.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:provider/provider.dart';
// `MultiProvider.providers` is a list of these, and provider keeps the type in
// its own library rather than the barrel file.
import 'package:provider/single_child_widget.dart';

import 'app.dart';
import 'state/auth_controller.dart';
import 'state/update_controller.dart';

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
    MultiProvider(
      providers: <SingleChildWidget>[
        ChangeNotifierProvider<AuthController>.value(value: auth),
        // Separate from the session on purpose: the endpoint behind it is
        // anonymous so that a build too old to sign in can still be told what is
        // wrong with it, and putting the check inside the controller that owns
        // the token would quietly have made the token a prerequisite again.
        ChangeNotifierProvider<UpdateController>(
          create: (_) => UpdateController(),
        ),
      ],
      child: const SmsPortalApp(),
    ),
  );
}
