/// Readers for the shapes the API actually sends.
///
/// Money and percentages cross the wire as JSON numbers in invariant form and
/// are never pre-formatted (docs/Integration/03-Mobile-API.md §2) — an `int`
/// where the school happened to charge a round figure, a `double` otherwise. A
/// cast to either one alone crashes on the other, which is exactly the sort of
/// fault that only shows up against a real school's data.
library;

double? asDecimal(dynamic value) {
  if (value == null) return null;
  if (value is num) return value.toDouble();
  if (value is String) return double.tryParse(value);
  return null;
}

int asInt(dynamic value, [int fallback = 0]) {
  if (value is num) return value.toInt();
  if (value is String) return int.tryParse(value) ?? fallback;
  return fallback;
}

bool asBool(dynamic value, [bool fallback = false]) =>
    value is bool ? value : fallback;

String asString(dynamic value, [String fallback = '']) =>
    value is String ? value : fallback;

String? asStringOrNull(dynamic value) => value is String ? value : null;

/// Dates are Gregorian ISO-8601, always (§2). Hijri display, if a school ever
/// wants it, is the client's own decision and does not change the wire.
DateTime? asUtcDate(dynamic value) {
  if (value is! String || value.isEmpty) return null;
  final DateTime? parsed = DateTime.tryParse(value);
  if (parsed == null) return null;
  // `System.DateTime` serialises without an offset when its Kind is Unspecified,
  // which is how every `*Utc` field on this API arrives. Reading it as local
  // time would shift a due date across a day boundary in the Gulf.
  return parsed.isUtc ? parsed : DateTime.utc(
        parsed.year,
        parsed.month,
        parsed.day,
        parsed.hour,
        parsed.minute,
        parsed.second,
        parsed.millisecond,
      );
}

List<Map<String, dynamic>> asObjectList(dynamic value) {
  if (value is! List) return const <Map<String, dynamic>>[];
  return value.whereType<Map<String, dynamic>>().toList(growable: false);
}

/// The one list shape every collection endpoint returns (§4).
class Paged<T> {
  const Paged({
    required this.items,
    required this.page,
    required this.pageSize,
    required this.total,
    required this.hasMore,
  });

  final List<T> items;
  final int page;
  final int pageSize;
  final int total;
  final bool hasMore;

  static Paged<T> fromJson<T>(
    dynamic json,
    T Function(Map<String, dynamic>) read,
  ) {
    final Map<String, dynamic> map =
        json is Map<String, dynamic> ? json : const <String, dynamic>{};
    return Paged<T>(
      items: asObjectList(map['items']).map(read).toList(growable: false),
      page: asInt(map['page'], 1),
      pageSize: asInt(map['pageSize'], 25),
      total: asInt(map['total']),
      hasMore: asBool(map['hasMore']),
    );
  }

  Paged<T> followedBy(Paged<T> next) => Paged<T>(
        items: <T>[...items, ...next.items],
        page: next.page,
        pageSize: next.pageSize,
        total: next.total,
        hasMore: next.hasMore,
      );
}
