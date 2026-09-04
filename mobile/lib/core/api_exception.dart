/// A refusal the server gave, in the one envelope it gives them in.
///
/// docs/Integration/03-Mobile-API.md §3: every non-2xx answer — including the
/// ones the framework raises on its own — arrives as
/// `{ "error": { "code": ..., "message": ..., "fields": ... } }`. [code] is
/// stable and language-independent, so this app branches on it; [message] is
/// already in the caller's language, so this app *shows* it and never
/// re-translates it. Doing the reverse is how a school ends up reading a code
/// name it was never meant to see.
class ApiException implements Exception {
  ApiException({
    required this.status,
    required this.code,
    required this.message,
    this.fields,
  });

  /// HTTP status. 404 means "not found **or** not permitted" — BR-SEC-010 makes
  /// those one answer on purpose, and the app must not try to tell them apart.
  final int status;

  final String code;

  /// Already Arabic or English per `Accept-Language`. Show it as-is.
  final String message;

  /// Per-field messages, present only on `validation_failed`.
  final Map<String, List<String>>? fields;

  /// BR-SEC-004: the session is over — revoked, idle out, or past its ceiling.
  /// The app signs out rather than retrying, because nothing it can send will
  /// make this token work again.
  bool get isUnauthenticated => status == 401;

  /// BR-SEC-005. Every endpoint but change-password and logout refuses while
  /// this stands.
  bool get mustChangePassword => code == 'must_change_password';

  @override
  String toString() => 'ApiException($status, $code): $message';
}

/// The network never reached the school. Distinct from [ApiException] because
/// there is no server-authored message to show and no code to branch on — the
/// app supplies both, and a retry is worth offering.
class ApiUnreachableException implements Exception {
  ApiUnreachableException(this.cause);

  final Object cause;

  @override
  String toString() => 'ApiUnreachableException: $cause';
}
