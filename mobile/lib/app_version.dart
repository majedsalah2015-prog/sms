/// What this build calls itself, shown on the sign-in screen and in the menu.
///
/// A family that has just been asked to install an update needs to be able to
/// answer "did it actually install?" without help from the school, and until
/// this existed there was nothing on any screen that said. It is a constant
/// rather than a `package_info_plus` lookup because the answer is fixed at
/// compile time and a dependency is a poor price for one string —
/// `test/version_test.dart` reads `pubspec.yaml` and fails the build if the two
/// ever drift apart, which is the only risk a constant carries.
library;

/// Must equal the `version:` line in `pubspec.yaml`, before the `+`.
const String kAppVersion = '1.1.0';

/// Android's versionCode — the `+N` half. Shown beside the version so a school
/// can tell two builds of one version apart when diagnosing an install.
const int kAppBuild = 2;

/// `1.1.0 (2)`, the form both screens print.
String get appVersionLabel => '$kAppVersion ($kAppBuild)';
