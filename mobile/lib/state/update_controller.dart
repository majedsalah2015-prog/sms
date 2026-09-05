import 'package:flutter/foundation.dart';

import '../core/api_client.dart';
import '../core/api_exception.dart';
import '../models/app_update.dart';
import 'portal_api.dart';

/// Whether this build is still the one the school wants installed.
///
/// It is kept apart from [AuthController] on purpose: that class is the session
/// and nothing else, and this question has no session in it. The endpoint behind
/// it is anonymous precisely so a build too old to sign in can still be told
/// what is wrong with it, and folding the check into the controller that owns
/// the token would have quietly made the token a prerequisite again.
///
/// **This is not a push notification.** Push needs a device registry and a
/// provider decision, both still pending in `docs/Status/`, and neither is
/// invented here. The phone asks when it starts; the school answers. A family
/// that never opens the app is never told, which is the honest limit of what an
/// in-app check can do — and it is still the difference between a fix reaching a
/// school and sitting on the server while the complaint keeps arriving.
class UpdateController extends ChangeNotifier {
  UpdateController({ApiClient Function(String baseUrl)? clientFactory})
      : _clientFactory = clientFactory;

  final ApiClient Function(String baseUrl)? _clientFactory;

  AppUpdate _update = AppUpdate.unknown;

  /// The school's answer, or [AppUpdate.unknown] until there is one.
  AppUpdate get update => _update;

  /// Where the family goes to install it, absolute. Null until a check has
  /// succeeded — there is no address to send anyone to before then.
  Uri? _installUri;
  Uri? get installUri => _installUri;

  /// The version the family has already waved away this run. Held per version
  /// rather than as a flag, so a school that publishes again is not silenced by
  /// a dismissal of the build before it.
  ///
  /// Not persisted. Reminding once per launch is mild, and the alternative —
  /// a stored dismissal — is a preference that would have to be cleaned up when
  /// the version it names stops existing, for a nag this app does not have.
  String? _dismissed;

  bool _checking = false;

  /// The school will not accept this build any more. The app stops rather than
  /// nags; the server never says so unless something new enough to satisfy it is
  /// genuinely downloadable, so this is always a demand the family can meet.
  bool get mustUpdate => _update.updateRequired;

  /// A newer build exists and the family has not waved it away yet.
  bool get shouldOffer =>
      _update.updateAvailable &&
      !_update.updateRequired &&
      _dismissed != _update.latestVersion;

  /// Asks the school. Silent about its own failures, on purpose: an update
  /// check that cannot reach the server must look exactly like a school with
  /// nothing to say, or a family on a weak connection would be told their app
  /// was out of date — or worse, blocked — by a request that simply never
  /// arrived.
  Future<void> check({
    required String baseUrl,
    required String languageCode,
  }) async {
    if (_checking) return;
    _checking = true;

    final bool ours = _clientFactory == null;
    final ApiClient client =
        _clientFactory?.call(baseUrl) ?? ApiClient(baseUrl: baseUrl);
    client.languageCode = languageCode;

    try {
      final AppUpdate answer = await PortalApi(client).appVersion();
      _update = answer;
      _installUri = client.resolve(answer.installUrl);
      notifyListeners();
    } on ApiException {
      // A school running a build of the server without this endpoint answers
      // 404. That is a deployment one version behind, not a phone that is — and
      // it must read as "nothing to say".
    } on ApiUnreachableException {
      // Offline, or the school is down. Neither is the family's problem to be
      // shown as an update prompt.
    } finally {
      if (ours) client.close();
      _checking = false;
    }
  }

  /// The family has read the banner. Applies to this version only, and only
  /// until the app is next started.
  void dismiss() {
    if (!_update.updateAvailable) return;
    _dismissed = _update.latestVersion;
    notifyListeners();
  }

  /// Test seam. Nothing in the app calls this — the school is the only thing
  /// that decides what [update] holds.
  @visibleForTesting
  void seed(AppUpdate value, {Uri? installUri}) {
    _update = value;
    _installUri = installUri;
    _dismissed = null;
    notifyListeners();
  }
}
