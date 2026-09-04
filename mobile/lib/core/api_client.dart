import 'dart:async';
import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

import 'package:http/http.dart' as http;

import 'api_exception.dart';

/// The one place this app talks to the school.
///
/// It is deliberately thin. The server owns every rule, every permission and
/// every refusal (docs/Integration/03-Mobile-API.md); this class carries the
/// bearer token, states the caller's language, and turns the API's single error
/// envelope into an [ApiException]. It decides nothing else — an HTTP client
/// that starts interpreting business answers is how a phone and a printed
/// statement begin disagreeing about what a family owes.
class ApiClient {
  ApiClient({
    required this.baseUrl,
    http.Client? inner,
    this.onUnauthenticated,
  }) : _http = inner ?? http.Client();

  /// The school's root, without a trailing slash — `http://10.0.2.2:5099`.
  final String baseUrl;

  /// Called once for any 401. BR-SEC-004 makes a dead token permanently dead:
  /// revocation, idle timeout and the absolute ceiling all land here, and none
  /// of them is worth a retry.
  final void Function()? onUnauthenticated;

  final http.Client _http;

  /// The bearer token, set after sign-in and cleared on sign-out.
  String? token;

  /// `ar` or `en`. Request localization on the server reads this and answers —
  /// including every refusal — in that language, so nothing else is needed.
  String languageCode = 'ar';

  static const Duration _timeout = Duration(seconds: 30);

  Map<String, String> _headers({bool json = false}) {
    final Map<String, String> headers = <String, String>{
      'Accept': 'application/json',
      // The server matches on the culture, not the exact tag; the regional
      // subtags are what its request localization was configured with.
      'Accept-Language': languageCode == 'ar' ? 'ar-SA' : 'en-US',
    };
    if (json) headers['Content-Type'] = 'application/json; charset=utf-8';
    final String? bearer = token;
    if (bearer != null && bearer.isNotEmpty) {
      headers['Authorization'] = 'Bearer $bearer';
    }
    return headers;
  }

  Uri _uri(String path, [Map<String, dynamic>? query]) {
    final String root = baseUrl.endsWith('/')
        ? baseUrl.substring(0, baseUrl.length - 1)
        : baseUrl;
    final String rooted = path.startsWith('/') ? path : '/$path';
    final Uri uri = Uri.parse('$root$rooted');
    if (query == null || query.isEmpty) return uri;
    return uri.replace(
      queryParameters: query.map(
        (String k, dynamic v) => MapEntry<String, String>(k, '$v'),
      ),
    );
  }

  Future<dynamic> get(String path, {Map<String, dynamic>? query}) =>
      _send(() => _http.get(_uri(path, query), headers: _headers()));

  Future<dynamic> post(String path, {Object? body}) => _send(
        () => _http.post(
          _uri(path),
          headers: _headers(json: true),
          body: body == null ? null : jsonEncode(body),
        ),
      );

  /// An absolute URL for something the API told us where to fetch — a lesson
  /// resource's `downloadUrl`, which arrives relative to the same root.
  Uri resolve(String pathOrUrl) {
    if (pathOrUrl.startsWith('http://') || pathOrUrl.startsWith('https://')) {
      return Uri.parse(pathOrUrl);
    }
    return _uri(pathOrUrl);
  }

  /// Fetches a file endpoint's bytes.
  ///
  /// It goes through this client rather than being handed to the phone's
  /// browser because the download is `[Authorize]`d like everything else: a
  /// browser carries no `Authorization` header and would be answered `401`, and
  /// putting the session token in a URL to work around that would leak a live
  /// credential into history, logs and the recents list. BR-LRN-006's scan gate
  /// is re-applied at this endpoint, so a refusal here is a real answer and
  /// arrives in the normal envelope.
  Future<DownloadedFile> getBytes(String path) async {
    final http.Response response;
    try {
      response = await _http
          .get(_uri(path), headers: _headers())
          .timeout(const Duration(minutes: 2));
    } on SocketException catch (e) {
      throw ApiUnreachableException(e);
    } on HttpException catch (e) {
      throw ApiUnreachableException(e);
    } on http.ClientException catch (e) {
      throw ApiUnreachableException(e);
    } on TimeoutException catch (e) {
      throw ApiUnreachableException(e);
    }

    if (response.statusCode >= 400) {
      dynamic decoded;
      try {
        decoded = jsonDecode(utf8.decode(response.bodyBytes));
      } on FormatException {
        decoded = null;
      }
      throw _failure(response, decoded);
    }

    return DownloadedFile(
      bytes: response.bodyBytes,
      fileName: _fileNameFrom(response.headers['content-disposition']),
      contentType: response.headers['content-type'],
    );
  }

  /// `Content-Disposition: attachment; filename="..."` — and its `filename*`
  /// form, which is what `File(...)` emits the moment a school names a file in
  /// Arabic.
  static String? _fileNameFrom(String? header) {
    if (header == null) return null;

    final RegExpMatch? extended =
        RegExp(r"filename\*=(?:UTF-8'')?([^;]+)", caseSensitive: false)
            .firstMatch(header);
    if (extended != null) {
      final String raw = extended.group(1)!.trim().replaceAll('"', '');
      try {
        return Uri.decodeComponent(raw);
      } on ArgumentError {
        return raw;
      }
    }

    final RegExpMatch? plain =
        RegExp(r'filename="?([^";]+)"?', caseSensitive: false)
            .firstMatch(header);
    return plain?.group(1)?.trim();
  }

  Future<dynamic> _send(Future<http.Response> Function() call) async {
    final http.Response response;
    try {
      response = await call().timeout(_timeout);
    } on SocketException catch (e) {
      throw ApiUnreachableException(e);
    } on HttpException catch (e) {
      throw ApiUnreachableException(e);
    } on http.ClientException catch (e) {
      throw ApiUnreachableException(e);
    } on TimeoutException catch (e) {
      // A school that answers too slowly is unreachable from the family's side,
      // and "try again" is the honest thing to offer. Left uncaught it would
      // surface as "something went wrong", which invites a pointless retry of
      // the same tap.
      throw ApiUnreachableException(e);
    }

    if (response.statusCode == 204 || response.bodyBytes.isEmpty) {
      if (response.statusCode >= 400) throw _failure(response, null);
      return null;
    }

    // The API answers UTF-8 and says so, but a decode that trusted the header
    // would mangle Arabic the one time a proxy dropped the charset.
    final dynamic decoded;
    try {
      decoded = jsonDecode(utf8.decode(response.bodyBytes));
    } on FormatException {
      // Not JSON at all. Almost always an HTML error page from something in
      // front of the app — which is a fault, not a refusal.
      throw ApiException(
        status: response.statusCode,
        code: 'unexpected_response',
        message: '',
      );
    }

    if (response.statusCode >= 400) {
      throw _failure(response, decoded);
    }
    return decoded;
  }

  ApiException _failure(http.Response response, dynamic decoded) {
    if (response.statusCode == 401) {
      onUnauthenticated?.call();
    }

    String code = 'unexpected_response';
    String message = '';
    Map<String, List<String>>? fields;

    if (decoded is Map<String, dynamic>) {
      final dynamic error = decoded['error'];
      if (error is Map<String, dynamic>) {
        code = (error['code'] as String?) ?? code;
        message = (error['message'] as String?) ?? '';
        final dynamic raw = error['fields'];
        if (raw is Map<String, dynamic>) {
          fields = raw.map(
            (String key, dynamic value) => MapEntry<String, List<String>>(
              key,
              value is List
                  ? value.map((dynamic v) => '$v').toList()
                  : <String>['$value'],
            ),
          );
        }
      }
    }

    return ApiException(
      status: response.statusCode,
      code: code,
      message: message,
      fields: fields,
    );
  }

  void close() => _http.close();
}

/// Bytes the API handed over, with whatever it called them.
class DownloadedFile {
  const DownloadedFile({
    required this.bytes,
    required this.fileName,
    required this.contentType,
  });

  final Uint8List bytes;
  final String? fileName;
  final String? contentType;
}
