import 'package:flutter_test/flutter_test.dart';
import 'package:intl/date_symbol_data_local.dart';
import 'package:sms_portal/ui/format.dart';

/// The app and the web portal must print the same figure the same way.
///
/// These expectations are not a preference — they were read off the running
/// portal (`/portal/students/1`, ar-SA) code point by code point, and they are
/// what `decimal.ToString("N2")` produces there. If a Flutter or `intl` upgrade
/// changes the output, this fails here rather than in a school, where the first
/// symptom would be a parent reading an amount back to the accounts office that
/// does not match the receipt.
void main() {
  setUpAll(() async => initializeDateFormatting());

  const String arabicGroup = '٬'; // ARABIC THOUSANDS SEPARATOR ٬
  const String arabicDecimal = '٫'; // ARABIC DECIMAL SEPARATOR ٫

  group('money', () {
    test('English is Western throughout', () {
      expect(Fmt.money(12000, 'SAR', 'en'), '12,000.00 SAR');
      expect(Fmt.money(0, 'SAR', 'en'), '0.00 SAR');
    });

    test('Arabic keeps Western digits and swaps only the separators', () {
      final String text = Fmt.money(12000, 'SAR', 'ar');
      expect(text, '12${arabicGroup}000${arabicDecimal}00 SAR');

      // The point of the whole exercise: no Arabic-Indic digit may appear. CLDR's
      // `ar` locale would have printed ١٢٬٠٠٠٫٠٠ and broken the product's rule.
      expect(RegExp(r'[٠-٩]').hasMatch(text), isFalse,
          reason: 'money must keep Western digits in both languages');
      expect(RegExp(r'[0-9]').hasMatch(text), isTrue);
    });

    test('a null amount is a dash, never a zero', () {
      // Null means "not shared with this caller" on `GET /portal/children`.
      // Printing 0.00 would tell a guardian the family owes nothing.
      expect(Fmt.money(null, 'SAR', 'ar'), '—');
    });

    test('no currency yields the bare figure', () {
      expect(Fmt.money(1234.5, '', 'en'), '1,234.50');
    });
  });

  group('percent', () {
    test('drops the decimal when the figure is whole', () {
      expect(Fmt.percent(100, 'en'), '100%');
      expect(Fmt.percent(97.5, 'en'), '97.5%');
    });

    test('Arabic uses the Arabic decimal separator', () {
      expect(Fmt.percent(97.5, 'ar'), '97${arabicDecimal}5%');
      expect(Fmt.percent(100, 'ar'), '100%');
    });

    test('null is a dash', () => expect(Fmt.percent(null, 'ar'), '—'));
  });

  group('marks', () {
    test('whole marks print without trailing zeros', () {
      expect(Fmt.marks(20, 'en'), '20');
      expect(Fmt.marks(17.25, 'en'), '17.25');
    });

    test('Arabic follows the same separator rule', () {
      expect(Fmt.marks(17.25, 'ar'), '17${arabicDecimal}25');
    });
  });

  group('dates', () {
    test('are Gregorian in both languages', () {
      // ADR-4: the calendar never follows the language. Only the month name does.
      final DateTime d = DateTime.utc(2026, 3, 12);
      expect(Fmt.date(d, 'en'), contains('2026'));
      expect(Fmt.date(d, 'ar'), contains('2026'));
    });

    test('null is a dash', () => expect(Fmt.date(null, 'en'), '—'));
  });

  group('time range', () {
    test('is shown as the timetable sent it', () {
      expect(Fmt.timeRange('07:45', '08:30'), '07:45 – 08:30');
      expect(Fmt.timeRange('07:45', null), '07:45');
      expect(Fmt.timeRange(null, null), '');
    });
  });
}
