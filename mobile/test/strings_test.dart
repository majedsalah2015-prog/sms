import 'dart:io';

import 'package:flutter_test/flutter_test.dart';
import 'package:sms_portal/l10n/strings.dart';

/// The bilingual rule, enforced rather than trusted.
///
/// This product's standing law is that every user-visible string ships Arabic
/// **and** English, and the failure it actually produces is not a missing
/// string — it is an English one copied into the Arabic slot and never looked
/// at again. So this reads the table's own source and checks both halves of
/// every entry, which no amount of calling getters one by one would cover.
void main() {
  final RegExp call = RegExp(
    r"_t\(\s*'((?:[^'\\]|\\.)*)'\s*,\s*'((?:[^'\\]|\\.)*)'\s*,?\s*\)",
    dotAll: true,
  );
  final RegExp arabicLetter = RegExp(r'[؀-ۿ]');

  late String source;
  late List<RegExpMatch> entries;

  setUpAll(() {
    source = File('lib/l10n/strings.dart').readAsStringSync();
    entries = call.allMatches(source).toList();
  });

  test('the table was found at all', () {
    // A refactor that renames the helper must not quietly turn this whole file
    // into a test that passes by checking nothing.
    expect(entries.length, greaterThan(50));
  });

  test('every entry ships both halves, and they are not the same string', () {
    for (final RegExpMatch entry in entries) {
      final String en = entry.group(1)!;
      final String ar = entry.group(2)!;

      expect(en.trim(), isNotEmpty, reason: 'English half empty: $ar');
      expect(ar.trim(), isNotEmpty, reason: 'Arabic half empty: $en');
      expect(
        ar,
        isNot(equals(en)),
        reason: 'Arabic and English are identical: "$en"',
      );
    }
  });

  test('the Arabic half is actually Arabic', () {
    for (final RegExpMatch entry in entries) {
      final String en = entry.group(1)!;
      final String ar = entry.group(2)!;

      // The one deliberate exception is the language switch, whose Arabic slot
      // holds the *other* language's name — that button says "العربية" while
      // the app is in English and "English" while it is in Arabic.
      if (ar == 'English') continue;

      expect(
        arabicLetter.hasMatch(ar),
        isTrue,
        reason: 'No Arabic letters in the Arabic half of "$en": "$ar"',
      );
    }
  });

  test('weekday names are complete in both languages', () {
    const Strings en = Strings(false);
    const Strings ar = Strings(true);
    for (int day = 0; day <= 6; day++) {
      expect(en.weekday(day), isNotEmpty);
      expect(ar.weekday(day), isNotEmpty);
      expect(ar.weekday(day), isNot(equals(en.weekday(day))));
    }
    // Out of range is empty rather than a crash: the API sends
    // `System.DayOfWeek`, and a value outside it means the server changed.
    expect(en.weekday(7), isEmpty);
  });

  test('pair() falls back rather than showing a blank', () {
    const Strings ar = Strings(true);
    expect(ar.pair('Grade 3', 'الصف الثالث'), 'الصف الثالث');
    // The school filled in only English. A blank cell is worse than the wrong
    // language, so the other half is shown.
    expect(ar.pair('Grade 3', ''), 'Grade 3');
    expect(ar.pair(null, null), '');
  });
}
