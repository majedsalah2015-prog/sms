import 'package:flutter_secure_storage/flutter_secure_storage.dart';

/// Where the bearer token and the school's address live between launches.
///
/// The token is `sec.UserSession.SessionToken` — the same opaque credential the
/// browser's auth cookie carries, and the only thing standing between a stolen
/// phone and a family's file. It goes in the platform keystore, never in
/// shared preferences.
///
/// The base URL sits beside it for convenience rather than secrecy: one store
/// is one dependency, and a school's hostname is not a secret.
class SessionStore {
  SessionStore([FlutterSecureStorage? storage])
      : _storage = storage ??
            const FlutterSecureStorage(
              aOptions: AndroidOptions(encryptedSharedPreferences: true),
            );

  static const String _tokenKey = 'sms.session.token';
  static const String _expiryKey = 'sms.session.expiresAtUtc';
  static const String _baseUrlKey = 'sms.server.baseUrl';

  final FlutterSecureStorage _storage;

  Future<String?> readToken() => _storage.read(key: _tokenKey);

  /// BR-SEC-004's absolute ceiling for the stored session, or null when none is
  /// stored. The app treats a past ceiling as signed-out *before* calling, so a
  /// family sees a sign-in screen rather than a failure mid-tap.
  Future<DateTime?> readExpiry() async {
    final String? raw = await _storage.read(key: _expiryKey);
    if (raw == null) return null;
    return DateTime.tryParse(raw)?.toUtc();
  }

  Future<void> writeSession(String token, DateTime? expiresAtUtc) async {
    await _storage.write(key: _tokenKey, value: token);
    if (expiresAtUtc == null) {
      await _storage.delete(key: _expiryKey);
    } else {
      await _storage.write(
        key: _expiryKey,
        value: expiresAtUtc.toUtc().toIso8601String(),
      );
    }
  }

  Future<void> clearSession() async {
    await _storage.delete(key: _tokenKey);
    await _storage.delete(key: _expiryKey);
  }

  Future<String?> readBaseUrl() => _storage.read(key: _baseUrlKey);

  Future<void> writeBaseUrl(String baseUrl) =>
      _storage.write(key: _baseUrlKey, value: baseUrl);
}
