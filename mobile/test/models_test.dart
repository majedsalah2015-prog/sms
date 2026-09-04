import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:sms_portal/models/json.dart';
import 'package:sms_portal/models/me.dart';
import 'package:sms_portal/models/portal.dart';

/// Reading what the API really sends.
void main() {
  test('a decimal arrives as an int when the figure happens to be round', () {
    // `System.Text.Json` writes 12000m as `12000`, not `12000.0`. A model that
    // cast straight to double would crash on exactly the schools whose fees are
    // round numbers — which is most of them.
    final PortalFees fees = PortalFees.fromJson(
      jsonDecode(
        '{"studentId":1,"position":12000,"grossCharges":12000.5,'
        '"discounts":0,"currency":"SAR","charges":[]}',
      ) as Map<String, dynamic>,
    );

    expect(fees.position, 12000.0);
    expect(fees.grossCharges, 12000.5);
    expect(fees.currency, 'SAR');
  });

  test('a null figure stays null and never becomes zero', () {
    // `GET /portal/children` returns null when the fee gate refused for this
    // caller. Reading that as 0 would tell a guardian the family owes nothing.
    final PortalChild child = PortalChild.fromJson(
      jsonDecode(
        '{"studentId":4,"studentNo":"S-4","nameAr":"سالم","nameEn":"Salem",'
        '"isSelf":false,"gradeName":"الصف الثالث","sectionName":"أ",'
        '"attendancePercent":100,"feeBalance":null}',
      ) as Map<String, dynamic>,
    );

    expect(child.feeBalance, isNull);
    expect(child.attendancePercent, 100.0);
    expect(child.nameAr, 'سالم');
  });

  test('attendance with no recorded days is empty, not zero per cent', () {
    final PortalAttendance a = PortalAttendance.fromJson(
      jsonDecode(
        '{"studentId":1,"scheduledDays":0,"exemptedDays":0,'
        '"absentDays":0,"attendancePercent":0}',
      ) as Map<String, dynamic>,
    );

    expect(a.isEmpty, isTrue);
  });

  test('a *Utc field with no offset is read as UTC, not as local time', () {
    // `DateTime.Kind == Unspecified` serialises without a `Z`. Reading it as
    // local shifts a due date across the day boundary in the Gulf.
    final DateTime? parsed = asUtcDate('2026-03-12T21:30:00');
    expect(parsed!.isUtc, isTrue);
    expect(parsed.hour, 21);
  });

  test('permissions land as a set the menu can ask', () {
    final Me me = Me.fromJson(
      jsonDecode(
        '{"userAccountId":9,"userName":"parent","accountType":"Parent",'
        '"schoolId":1,"schoolNameAr":"مدرسة","schoolNameEn":"School",'
        '"workingAcademicYearId":3,"workingAcademicYearName":"2026/2027",'
        '"mustChangePassword":false,"twoFactorEnabled":false,'
        '"sessionExpiresAtUtc":"2026-09-02T22:00:00",'
        '"subject":{"kind":"Parent","id":5,"nameAr":"أبو سالم",'
        '"nameEn":"Abu Salem","reference":null},'
        '"children":[{"studentId":4,"studentNo":"S-4","nameAr":"سالم",'
        '"nameEn":"Salem"}],'
        '"permissions":["POR/Home/View","POR/Child/View"]}',
      ) as Map<String, dynamic>,
    );

    expect(me.isPortalAccount, isTrue);
    expect(me.can(PortalPermissions.home), isTrue);
    expect(me.can(PortalPermissions.child), isTrue);
    // Not granted. The tab is hidden — and the endpoint would answer 404
    // anyway, which is the half that actually enforces it (BR-SEC-010).
    expect(me.can(PortalPermissions.statement), isFalse);
    expect(me.children.single.nameAr, 'سالم');
    expect(me.subject?.kind, 'Parent');
  });

  test('a staff account is recognised as not belonging in this app', () {
    final Me me = Me.fromJson(
      jsonDecode(
        '{"userAccountId":1,"userName":"admin","accountType":"Staff",'
        '"schoolNameAr":"مدرسة","schoolNameEn":"School",'
        '"mustChangePassword":false,"children":[],"permissions":[]}',
      ) as Map<String, dynamic>,
    );

    expect(me.isPortalAccount, isFalse);
    expect(me.children, isEmpty);
  });

  test('the timetable groups by day and orders periods within it', () {
    final PortalTimetable week = PortalTimetable.fromJson(
      jsonDecode(
        '{"studentId":1,"sectionName":"أ","gradeCode":"G3",'
        '"weekStart":"2026-09-05T00:00:00","entries":['
        '{"dayOfWeek":1,"periodSequence":2,"subjectNameAr":"علوم",'
        '"subjectNameEn":"Science","changeKind":null},'
        '{"dayOfWeek":1,"periodSequence":1,"subjectNameAr":"رياضيات",'
        '"subjectNameEn":"Maths","changeKind":"Substitution"},'
        '{"dayOfWeek":0,"periodSequence":1,"subjectNameAr":"لغة",'
        '"subjectNameEn":"Language","changeKind":null}]}',
      ) as Map<String, dynamic>,
    );

    final Map<int, List<TimetableEntry>> days = week.byDay();
    expect(days.keys.toSet(), <int>{0, 1});
    expect(
      days[1]!.map((TimetableEntry e) => e.periodSequence).toList(),
      <int>[1, 2],
    );
    expect(days[1]!.first.changeKind, 'Substitution');
  });

  test('a page reports what is left rather than being guessed at', () {
    final Paged<PortalAnnouncement> page = Paged.fromJson<PortalAnnouncement>(
      jsonDecode(
        '{"items":[{"id":1,"titleAr":"إعلان","titleEn":"Notice",'
        '"bodyAr":null,"bodyEn":null,"sentAtUtc":"2026-09-01T08:00:00"}],'
        '"page":1,"pageSize":25,"total":40,"totalPages":2,"hasMore":true}',
      ),
      PortalAnnouncement.fromJson,
    );

    expect(page.items, hasLength(1));
    expect(page.hasMore, isTrue);
    expect(page.total, 40);
  });

  test('homework with no maximum is practice, not a blank mark', () {
    final PortalHomework hw = PortalHomework.fromJson(
      jsonDecode(
        '{"homeworkId":3,"titleAr":"تمرين","titleEn":"Drill",'
        '"instructionsAr":null,"instructionsEn":null,'
        '"subjectNameAr":"رياضيات","subjectNameEn":"Maths",'
        '"dueDate":"2026-09-10T00:00:00","maxMarks":null,'
        '"latePenaltyApplies":false,"latePenaltyPercent":null}',
      ) as Map<String, dynamic>,
    );

    // BR-LRN-004. The screen says "not graded"; it must not show an empty box
    // that reads as a mark nobody has entered yet.
    expect(hw.maxMarks, isNull);
    expect(hw.latePenaltyApplies, isFalse);
  });
}
