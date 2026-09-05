import 'package:intl/intl.dart';

/// How this app renders the numbers and dates the API sends raw.
///
/// The server sends money as a JSON number in invariant form and never
/// pre-formats it (docs/Integration/03-Mobile-API.md §2), because only the
/// client knows what locale it is displaying in. This is that decision, in one
/// place — and it is deliberately the *same* decision the web portal already
/// made, so a figure reads identically on a laptop and on a phone.
abstract final class Fmt {
  /// What `decimal.ToString("N2")` produces under `ar-SA`, verified against the
  /// running portal rather than assumed: **Western digits** with the Arabic
  /// thousands separator U+066C and decimal separator U+066B.
  ///
  /// Neither of these is what CLDR's `ar` locale would give — its default
  /// numbering system is `arab`, which would print ١٢٬٠٠٠٫٠٠ in Arabic-Indic
  /// digits and break this product's standing rule that money keeps Western
  /// digits in both directions. So the number is formatted in `en` and only its
  /// two separators are swapped.
  static const String _arabicGroup = '٬';
  static const String _arabicDecimal = '٫';

  static String _separators(String western, String languageCode) {
    if (languageCode != 'ar') return western;
    final StringBuffer out = StringBuffer();
    for (int i = 0; i < western.length; i++) {
      final String ch = western[i];
      if (ch == ',') {
        out.write(_arabicGroup);
      } else if (ch == '.') {
        out.write(_arabicDecimal);
      } else {
        out.write(ch);
      }
    }
    return out.toString();
  }

  /// Money keeps Western digits in both languages and is laid out
  /// left-to-right, which is this product's standing rule — an amount is the
  /// one thing on an Arabic screen that must not switch numeral systems, and a
  /// figure a parent reads out to the accounts office has to match the receipt.
  static String money(double? amount, String currency, String languageCode) {
    if (amount == null) return '—';
    final String text = _separators(
      NumberFormat('#,##0.00', 'en').format(amount),
      languageCode,
    );
    return currency.isEmpty ? text : '$text $currency';
  }

  /// Attendance and late-penalty rates. One decimal, dropped when whole:
  /// "97.5%" and "100%", never "100.0%" — the same `0.#` the portal uses.
  static String percent(double? value, String languageCode) {
    if (value == null) return '—';
    final bool whole = value == value.roundToDouble();
    return '${_separators(NumberFormat(whole ? '#,##0' : '#,##0.#', 'en').format(value), languageCode)}%';
  }

  /// A mark, or a count of days. Trailing zeros are noise on a phone.
  static String marks(double? value, String languageCode) {
    if (value == null) return '—';
    final bool whole = value == value.roundToDouble();
    return _separators(
      NumberFormat(whole ? '#,##0' : '#,##0.##', 'en').format(value),
      languageCode,
    );
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
