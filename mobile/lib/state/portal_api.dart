import '../app_version.dart';
import '../core/api_client.dart';
import '../models/app_update.dart';
import '../models/json.dart';
import '../models/me.dart';
import '../models/portal.dart';

/// Every call this app makes, named once.
///
/// The routes are the API's, verbatim (docs/Integration/04-Mobile-API-Reference.md).
/// Nothing here computes: `IStatementService` and `IFeeAdmin` are the single
/// central computation BR-FEE-008 requires, and a second arithmetic on a second
/// transport is how a phone and a printed statement start disagreeing about
/// what a family owes.
class PortalApi {
  const PortalApi(this._client);

  final ApiClient _client;

  // ----------------------------------------------------------------- app

  /// What build the school is publishing, and whether this one is behind it.
  ///
  /// Anonymous on the server, deliberately: the case this exists for at its
  /// sharpest is a build too old to sign in, and a check that needed a token
  /// would answer that phone with a sign-in failure instead of the one message
  /// that would help it. So it is also the only call this app makes before the
  /// keystore has been read.
  ///
  /// The running build goes out as two parameters rather than one `1.1.0+2`
  /// string because `+` in a query string decodes to a space — the one-parameter
  /// form would need percent-encoding forever, and forgetting it once would look
  /// like a build with no versionCode rather than like a mistake.
  Future<AppUpdate> appVersion() async {
    final dynamic json = await _client.get(
      '/api/v1/app/version',
      query: <String, dynamic>{
        'version': kAppVersion,
        'build': kAppBuild,
      },
    );
    return AppUpdate.fromJson(json as Map<String, dynamic>);
  }

  // ---------------------------------------------------------------- auth

  Future<LoginResult> login({
    required String userName,
    required String password,
    String? deviceName,
  }) async {
    final dynamic json = await _client.post(
      '/api/v1/auth/login',
      body: <String, dynamic>{
        'userName': userName,
        'password': password,
        if (deviceName != null && deviceName.isNotEmpty)
          'deviceName': deviceName,
      },
    );
    return LoginResult.fromJson(json as Map<String, dynamic>);
  }

  Future<LoginResult> completeTwoFactor({
    required String twoFactorToken,
    required String code,
  }) async {
    final dynamic json = await _client.post(
      '/api/v1/auth/two-factor',
      body: <String, dynamic>{
        'twoFactorToken': twoFactorToken,
        'code': code,
      },
    );
    return LoginResult.fromJson(json as Map<String, dynamic>);
  }

  /// BR-SEC-005. One of the two endpoints that still answer while a forced
  /// change stands.
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) =>
      _client.post(
        '/api/v1/auth/change-password',
        body: <String, dynamic>{
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
      );

  /// Ends the session on the server, not merely on this device. A local-only
  /// sign-out leaves a live token on a phone that has been handed to somebody.
  Future<void> logout() => _client.post('/api/v1/auth/logout');

  Future<Me> me() async {
    final dynamic json = await _client.get('/api/v1/auth/me');
    return Me.fromJson(json as Map<String, dynamic>);
  }

  // -------------------------------------------------------------- portal

  Future<List<PortalChild>> children() async {
    final dynamic json = await _client.get('/api/v1/portal/children');
    return (json is List ? json : const <dynamic>[])
        .whereType<Map<String, dynamic>>()
        .map(PortalChild.fromJson)
        .toList(growable: false);
  }

  Future<PortalAttendance> attendance(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/attendance');
    return PortalAttendance.fromJson(json as Map<String, dynamic>);
  }

  Future<List<PortalResult>> results(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/results');
    return (json is List ? json : const <dynamic>[])
        .whereType<Map<String, dynamic>>()
        .map(PortalResult.fromJson)
        .toList(growable: false);
  }

  Future<PortalFees> fees(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/fees');
    return PortalFees.fromJson(json as Map<String, dynamic>);
  }

  Future<PortalStatement> statement() async {
    final dynamic json = await _client.get('/api/v1/portal/statement');
    return PortalStatement.fromJson(json as Map<String, dynamic>);
  }

  Future<List<PortalHomework>> homework(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/homework');
    return (json is List ? json : const <dynamic>[])
        .whereType<Map<String, dynamic>>()
        .map(PortalHomework.fromJson)
        .toList(growable: false);
  }

  Future<List<PortalLesson>> lessons(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/lessons');
    return (json is List ? json : const <dynamic>[])
        .whereType<Map<String, dynamic>>()
        .map(PortalLesson.fromJson)
        .toList(growable: false);
  }

  Future<PortalTimetable> timetable(int studentId) async {
    final dynamic json =
        await _client.get('/api/v1/portal/students/$studentId/timetable');
    return PortalTimetable.fromJson(json as Map<String, dynamic>);
  }

  /// One lesson resource's bytes, fetched with the session token rather than
  /// handed to the phone's browser — the endpoint is `[Authorize]`d, and
  /// BR-LRN-006 re-checks the scan verdict here, so a resource withdrawn since
  /// it was listed refuses at this call and not silently.
  Future<DownloadedFile> downloadResource(PortalLessonResource resource) {
    final String path = resource.downloadUrl.isNotEmpty
        ? resource.downloadUrl
        : '/api/v1/portal/resources/${resource.resourceId}/file';
    return _client.getBytes(path);
  }

  Future<Paged<PortalAnnouncement>> announcements({
    int page = 1,
    int pageSize = 25,
  }) async {
    final dynamic json = await _client.get(
      '/api/v1/portal/announcements',
      query: <String, dynamic>{'page': page, 'pageSize': pageSize},
    );
    return Paged.fromJson<PortalAnnouncement>(
      json,
      PortalAnnouncement.fromJson,
    );
  }
}
