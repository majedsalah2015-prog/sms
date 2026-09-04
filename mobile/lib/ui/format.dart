import 'package:intl/intl.dart';

/// How this app renders the numbers and dates the API sends raw.
///
/// The server sends money as a JSON number in invariant form and never
/// pre-formats it (docs/Integration/03-Mobile-API.md §2), because only the
/// client knows what locale it is displaying in. This is that decision, in one
/// place.
abstract final class Fmt {
  /// Money keeps Western digits in both languages and is laid out
  /// left-to-right, which is this product's standing rule — an amount is the
  /// one thing on an Arabic screen that must not switch numeral systems, and a
  /// figure a parent reads out to the accounts office has to match the receipt.
  static String money(double? amount, String currency) {
    if (amount == null) return '—';
    final String text = NumberFormat('#,##0.00', 'en').format(amount);
    return currency.isEmpty ? text : '$text $currency';
  }

  /// Attendance and late-penalty rates. One decimal, dropped when whole:
  /// "97.5%" and "100%", never "100.0%".
  static String percent(double? value) {
    if (value == null) return '—';
    final bool whole = value == value.roundToDouble();
    return '${NumberFormat(whole ? '#,##0' : '#,##0.#', 'en').format(value)}%';
  }

  /// A mark out of a maximum. Trailing zeros are noise on a phone.
  static String marks(double? value) {
    if (value == null) return '—';
    final bool whole = value == value.roundToDouble();
    return NumberFormat(whole ? '#,##0' : '#,##0.##', 'en').format(value);
  }

  /// Gregorian, always — the calendar never follows the language (ADR-4). The
  /// month name does, which is what makes an Arabic screen read as Arabic
  /// without moving the date itself.
  static String date(DateTime? value, String languageCode) {
    if (value == null) return '—';
    return DateFormat.yMMMd(languageCode).format(value.toLocal());
  }

  /// `startTime` / `endTime` arrive as the timetable's own "HH:mm" text. Shown
  /// as sent rather than re-parsed: a period that reads 07:45 on the wall
  /// planner must read 07:45 here.
  static String timeRange(String? start, String? end) {
    final String from = (start ?? '').trim();
    final String to = (end ?? '').trim();
    if (from.isEmpty && to.isEmpty) return '';
    if (to.isEmpty) return from;
    if (from.isEmpty) return to;
    return '$from – $to';
  }
}
