import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:sms_portal/core/api_client.dart';
import 'package:sms_portal/core/api_exception.dart';

/// The transport's own promises: the token goes out, the language goes out, and
/// every refusal comes back as one shape with the server's own sentence intact.
void main() {
  ApiClient clientAnswering(
    http.Response Function(http.Request request) respond, {
    void Function()? onUnauthenticated,
  }) {
    return ApiClient(
      baseUrl: 'http://school.test',
      inner: MockClient((http.Request request) async => respond(request)),
      onUnauthenticated: onUnauthenticated,
    );
  }

  test('sends the bearer token and the caller language', () async {
    late http.Request seen;
    final ApiClient client = clientAnswering((http.Request request) {
      seen = request;
      return http.Response('{"ok":true}', 200);
    })
      ..token = 'abc123'
      ..languageCode = 'ar';

    await client.get('/api/v1/auth/me');

    expect(seen.headers['Authorization'], 'Bearer abc123');
    expect(seen.headers['Accept-Language'], 'ar-SA');
    expect(seen.url.toString(), 'http://school.test/api/v1/auth/me');
  });

  test('an English caller asks for English', () async {
    late http.Request seen;
    final ApiClient client = clientAnswering((http.Request request) {
      seen = request;
      return http.Response('{}', 200);
    })..languageCode = 'en';

    await client.get('/api/v1/auth/me');

    expect(seen.headers['Accept-Language'], 'en-US');
  });

  test('a refusal keeps the server sentence and the stable code', () async {
    final ApiClient client = clientAnswering(
      (_) => http.Response.bytes(
        utf8.encode(
          '{"error":{"code":"installment_not_open",'
          '"message":"القسط غير مفتوح.","fields":null}}',
        ),
        409,
        headers: <String, String>{'content-type': 'application/json'},
      ),
    );

    await expectLater(
      client.get('/api/v1/finance/x'),
      throwsA(
        isA<ApiException>()
            .having((ApiException e) => e.status, 'status', 409)
            .having((ApiException e) => e.code, 'code', 'installment_not_open')
            // Shown verbatim. Re-translating a message the server already
            // localised is how a school reads a sentence nobody wrote.
            .having((ApiException e) => e.message, 'message', 'القسط غير مفتوح.'),
      ),
    );
  });

  test('validation_failed carries its per-field sentences', () async {
    // Response.bytes, not Response(String): the string constructor encodes
    // Latin-1 and throws on the first Arabic letter — which is the same trap
    // the client itself avoids by decoding bodyBytes as UTF-8 rather than
    // trusting `response.body`.
    final ApiClient client = clientAnswering(
      (_) => http.Response.bytes(
        utf8.encode(
          '{"error":{"code":"validation_failed","message":"تحقق من الحقول.",'
          '"fields":{"password":["أقصر من الحد الأدنى.","بدون رقم."]}}}',
        ),
        400,
      ),
    );

    try {
      await client.post('/api/v1/auth/change-password');
      fail('expected a refusal');
    } on ApiException catch (e) {
      expect(e.fields, isNotNull);
      expect(e.fields!['password'], hasLength(2));
    }
  });

  test('a 401 tells the app once, and only once per call', () async {
    int notified = 0;
    final ApiClient client = clientAnswering(
      (_) => http.Response('{"error":{"code":"unauthenticated","message":"x"}}', 401),
      onUnauthenticated: () => notified++,
    );

    await expectLater(
      client.get('/api/v1/portal/children'),
      throwsA(isA<ApiException>()
          .having((ApiException e) => e.isUnauthenticated, 'isUnauthenticated', true)),
    );
    expect(notified, 1);
  });

  test('a body that is not JSON is a fault, not a tidy refusal', () async {
    // An HTML error page from something in front of the app. Dressing it up as
    // a business refusal is how a broken deployment goes uninvestigated.
    final ApiClient client =
        clientAnswering((_) => http.Response('<html>502</html>', 502));

    await expectLater(
      client.get('/api/v1/portal/children'),
      throwsA(isA<ApiException>()
          .having((ApiException e) => e.code, 'code', 'unexpected_response')),
    );
  });

  test('204 answers become null rather than a decode failure', () async {
    final ApiClient client = clientAnswering((_) => http.Response('', 204));
    expect(await client.post('/api/v1/auth/logout'), isNull);
  });

  group('download filename', () {
    test('reads the plain form', () async {
      final ApiClient client = clientAnswering(
        (_) => http.Response.bytes(
          <int>[1, 2, 3],
          200,
          headers: <String, String>{
            'content-disposition': 'attachment; filename="week-3.pdf"',
            'content-type': 'application/pdf',
          },
        ),
      );

      final DownloadedFile file =
          await client.getBytes('/api/v1/portal/resources/7/file');

      expect(file.fileName, 'week-3.pdf');
      expect(file.bytes, hasLength(3));
    });

    test('reads the UTF-8 form a school gets by naming a file in Arabic',
        () async {
      final ApiClient client = clientAnswering(
        (_) => http.Response.bytes(
          <int>[1],
          200,
          headers: <String, String>{
            'content-disposition':
                "attachment; filename=drs.pdf; filename*=UTF-8''%D8%AF%D8%B1%D8%B3.pdf",
          },
        ),
      );

      final DownloadedFile file =
          await client.getBytes('/api/v1/portal/resources/7/file');

      expect(file.fileName, 'درس.pdf');
    });
  });
}
