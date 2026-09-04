import 'dart:async';

import 'package:flutter/foundation.dart';

import '../core/api_client.dart';
import '../core/api_exception.dart';
import '../core/session_store.dart';
import '../models/me.dart';
import 'portal_api.dart';

/// Where the app is in the sign-in the server owns.
enum AuthStage {
  /// Reading the keystore. The splash is showing.
  restoring,

  signedOut,

  /// BR-SEC-003: the password was accepted and a five-minute ticket is held.
  twoFactorPending,

  /// BR-SEC-005: signed in, but every endpoint except change-password and
  /// logout will refuse until a new password is set.
  mustChangePassword,

  signedIn,
}

/// The session, and nothing else.
///
/// Every rule enforced here already lives on the server; this class only keeps
/// the app's screens in step with it. In particular the app never decides *who
/// may see what* — [Me.permissions] is the server's own evaluation, and the
/// menu is built from it (BR-SEC-010).
class AuthController extends ChangeNotifier {
  AuthController({SessionStore? store, ApiClient Function(String)? clientFactory})
      : _store = store ?? SessionStore(),
        _clientFactory = clientFactory;

  /// Android's emulator reaches the host machine here; a real phone needs the
  /// laptop's LAN address, which is why the sign-in screen lets it be changed.
  static const String defaultBaseUrl = 'http://10.0.2.2:5099';

  final SessionStore _store;
  final ApiClient Function(String)? _clientFactory;

  AuthStage _stage = AuthStage.restoring;
  AuthStage get stage => _stage;

  String _baseUrl = defaultBaseUrl;
  String get baseUrl => _baseUrl;

  String _languageCode = 'ar';
  String get languageCode => _languageCode;
  bool get isArabic => _languageCode == 'ar';

  Me? _me;
  Me? get me => _me;

  ApiClient? _client;
  PortalApi? _api;

  /// The call surface, once there is a session to make calls with.
  PortalApi get api => _api ??= PortalApi(_ensureClient());

  String? _twoFactorToken;

  /// Set when a background 401 ended the session, so the sign-in screen can say
  /// why it is being shown rather than looking like a random sign-out.
  bool _endedByServer = false;
  bool get endedByServer => _endedByServer;

  ApiClient _ensureClient() {
    final ApiClient existing = _client ??= _clientFactory?.call(_baseUrl) ??
        ApiClient(baseUrl: _baseUrl, onUnauthenticated: _onUnauthenticated);
    existing.languageCode = _languageCode;
    return existing;
  }

  void _rebuildClient() {
    _client?.close();
    _client = null;
    _api = null;
  }

  /// A 401 from anywhere. BR-SEC-004 makes this permanent for that token —
  /// revocation, idle timeout and the absolute ceiling all arrive this way, and
  /// none of them is worth a retry.
  void _onUnauthenticated() {
    if (_stage == AuthStage.signedOut) return;
    _endedByServer = true;
    unawaited(_forget());
  }

  Future<void> _forget() async {
    await _store.clearSession();
    _me = null;
    _twoFactorToken = null;
    _client?.token = null;
    _stage = AuthStage.signedOut;
    notifyListeners();
  }

  // ------------------------------------------------------------- start-up

  /// Reads what the last session left behind. A token past its ceiling is
  /// dropped here rather than being discovered through a 401 mid-tap.
  Future<void> restore() async {
    _baseUrl = await _store.readBaseUrl() ?? defaultBaseUrl;
    _rebuildClient();

    final String? token = await _store.readToken();
    final DateTime? expiry = await _store.readExpiry();
    if (token == null ||
        (expiry != null && !expiry.isAfter(DateTime.now().toUtc()))) {
      await _store.clearSession();
      _stage = AuthStage.signedOut;
      notifyListeners();
      return;
    }

    _ensureClient().token = token;
    try {
      await _loadMe();
    } on ApiException {
      // Any refusal to /me means this token is not usable. `_onUnauthenticated`
      // has already cleared a 401; anything else is cleared here.
      await _forget();
    } on ApiUnreachableException {
      // The school is unreachable, which is not the same as being signed out.
      // Keep the token and let the sign-in screen offer a retry.
      _stage = AuthStage.signedOut;
      notifyListeners();
    }
  }

  Future<void> setBaseUrl(String value) async {
    final String trimmed = value.trim();
    if (trimmed.isEmpty || trimmed == _baseUrl) return;
    _baseUrl = trimmed.endsWith('/')
        ? trimmed.substring(0, trimmed.length - 1)
        : trimmed;
    await _store.writeBaseUrl(_baseUrl);
    _rebuildClient();
    notifyListeners();
  }

  /// The language is the client's choice and the server's instruction at once:
  /// it goes out as `Accept-Language` on every call, so the school answers —
  /// refusals included — in the language the app is showing.
  void setLanguage(String code) {
    if (code == _languageCode) return;
    _languageCode = code;
    _client?.languageCode = code;
    notifyListeners();
  }

  // -------------------------------------------------------------- sign-in

  Future<void> signIn({
    required String userName,
    required String password,
    String? deviceName,
  }) async {
    _endedByServer = false;
    final PortalApi client = PortalApi(_ensureClient());
    final LoginResult result = await client.login(
      userName: userName,
      password: password,
      deviceName: deviceName,
    );
    await _accept(result);
  }

  Future<void> submitTwoFactor(String code) async {
    final String? ticket = _twoFactorToken;
    if (ticket == null) {
      _stage = AuthStage.signedOut;
      notifyListeners();
      return;
    }
    final LoginResult result = await PortalApi(_ensureClient())
        .completeTwoFactor(twoFactorToken: ticket, code: code);
    await _accept(result);
  }

  Future<void> _accept(LoginResult result) async {
    if (result.requiresTwoFactor) {
      _twoFactorToken = result.twoFactorToken;
      _stage = AuthStage.twoFactorPending;
      notifyListeners();
      return;
    }

    _twoFactorToken = null;
    final String? token = result.token;
    if (token == null) {
      // The API contract says exactly one of the two is set; reaching here means
      // the server answered something this build does not understand, and
      // pretending to be signed in would be worse than saying so.
      throw ApiException(
        status: 200,
        code: 'unexpected_response',
        message: '',
      );
    }

    await _store.writeSession(token, result.expiresAtUtc);
    _ensureClient().token = token;

    if (result.mustChangePassword) {
      _stage = AuthStage.mustChangePassword;
      notifyListeners();
      return;
    }
    await _loadMe();
  }

  /// BR-SEC-005. Reachable while the forced change stands, and the only way out
  /// of [AuthStage.mustChangePassword].
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    await PortalApi(_ensureClient()).changePassword(
      currentPassword: currentPassword,
      newPassword: newPassword,
    );
    await _loadMe();
  }

  Future<void> _loadMe() async {
    final Me loaded = await PortalApi(_ensureClient()).me();
    _me = loaded;
    _stage = loaded.mustChangePassword
        ? AuthStage.mustChangePassword
        : AuthStage.signedIn;
    notifyListeners();
  }

  /// Ends the session on the server first. If that call cannot be made the
  /// local state is still cleared — a phone that keeps showing a family's file
  /// because the network was down is the worse failure of the two.
  Future<void> signOut() async {
    try {
      await PortalApi(_ensureClient()).logout();
    } on ApiException {
      // Already invalid server-side. Nothing left to end.
    } on ApiUnreachableException {
      // Offline. The token still expires on its own ceiling (BR-SEC-004).
    }
    _endedByServer = false;
    await _forget();
  }
}
