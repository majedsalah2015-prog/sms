import 'json.dart';

/// `GET /api/v1/app/version` — what build the school is publishing, and whether
/// this one is behind it.
///
/// The two verdicts are the **server's**, not this app's. A build that decided
/// for itself would decide with whatever comparison it happened to ship — and
/// the build being asked about is precisely the old one, so a mistake in it
/// could never be corrected from the school's side. It also means a school can
/// begin requiring an update without anyone installing anything first.
///
/// The wording is this app's, though. Every *refusal* is translated at the web
/// boundary because only the server knows the rule; a banner is the other case —
/// it is a label on a screen this app owns, `l10n/strings.dart` has both halves
/// of it, and a sentence fetched over the wire would be the one thing on that
/// banner that comes back blank on a slow connection.
class AppUpdate {
  const AppUpdate({
    required this.published,
    required this.latestVersion,
    required this.latestBuild,
    required this.minimumVersion,
    required this.updateAvailable,
    required this.updateRequired,
    required this.installUrl,
  });

  /// False when the school has published no package at all. Nothing is said in
  /// that case: "nobody has published a build" is not "you are up to date", and
  /// an app that showed the second would nag a family about an update that does
  /// not exist.
  final bool published;

  /// `1.4.0`, or null when the published file's name did not carry a version.
  final String? latestVersion;

  /// Android's versionCode for it, when the name carried one.
  final int? latestBuild;

  /// The oldest build the school still accepts, or null when it has set none —
  /// which is the default, and means nothing is ever forced.
  final String? minimumVersion;

  /// A newer build exists. Worth saying once, and dismissible.
  final bool updateAvailable;

  /// This build is older than the school's stated minimum. The app stops rather
  /// than nags. The server never sets this unless a build new enough to satisfy
  /// it is genuinely downloadable, so acting on it can always be acted upon.
  final bool updateRequired;

  /// The portal's own app page, relative to the school's root. It already
  /// carries the file, its size, its date and the four install steps in both
  /// languages, so this app sends a family there rather than keeping a second
  /// copy of that explanation which would go stale on its own.
  final String installUrl;

  /// `1.4.0 (12)` — the same form `appVersionLabel` prints for the running
  /// build, so a family comparing the two is comparing like with like.
  String? get latestLabel {
    final String? version = latestVersion;
    if (version == null || version.isEmpty) return null;
    return latestBuild == null ? version : '$version ($latestBuild)';
  }

  /// Nothing to say. The banner and the blocking screen both check this rather
  /// than each re-deriving it.
  bool get isSilent => !updateAvailable && !updateRequired;

  static AppUpdate fromJson(Map<String, dynamic> json) => AppUpdate(
        published: asBool(json['published']),
        latestVersion: asStringOrNull(json['latestVersion']),
        latestBuild: json['latestBuild'] is num
            ? asInt(json['latestBuild'])
            : null,
        minimumVersion: asStringOrNull(json['minimumVersion']),
        updateAvailable: asBool(json['updateAvailable']),
        updateRequired: asBool(json['updateRequired']),
        installUrl: asString(json['installUrl'], '/portal/app'),
      );

  /// What an app that could not ask looks like: silent. A school that cannot be
  /// reached must never be read as a school demanding an update — that would
  /// lock a family out of an app that was working perfectly well.
  static const AppUpdate unknown = AppUpdate(
    published: false,
    latestVersion: null,
    latestBuild: null,
    minimumVersion: null,
    updateAvailable: false,
    updateRequired: false,
    installUrl: '/portal/app',
  );
}
