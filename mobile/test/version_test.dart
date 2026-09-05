import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:sms_portal/app_version.dart';

/// The version the app prints must be the version it was built as.
///
/// `kAppVersion` is a hand-written constant, which is the whole reason this
/// exists: the failure it guards against is silent and lands on a family — the
/// school says "install 1.2.0", the phone shows 1.1.0 because somebody bumped
/// `pubspec.yaml` alone, and nobody can tell whether the update took.
void main() {
  test('the constant matches pubspec.yaml', () {
    final String pubspec = File('pubspec.yaml').readAsStringSync();
    final RegExpMatch? line = RegExp(
      r'^version:\s*(?<v>\d+\.\d+\.\d+)\+(?<b>\d+)\s*$',
      multiLine: true,
    ).firstMatch(pubspec);

    expect(line, isNotNull,
        reason: 'pubspec.yaml must carry a `version: x.y.z+n` line');
    expect(kAppVersion, line!.namedGroup('v'));
    expect(kAppBuild, int.parse(line.namedGroup('b')!));
  });

  test('the label is the form the screens print', () {
    expect(appVersionLabel, '$kAppVersion ($kAppBuild)');
  });
}
