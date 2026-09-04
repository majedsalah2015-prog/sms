import 'json.dart';

/// One of the family's students, as the home screen lists them.
///
/// Both figures may be null on their own, and that is a configuration rather
/// than a failure: `GET /portal/children` asks for each separately and keeps the
/// row when one refuses, because a guardian who may see the child but not the
/// money is a real arrangement.
class PortalChild {
  const PortalChild({
    required this.studentId,
    required this.studentNo,
    required this.nameAr,
    required this.nameEn,
    required this.isSelf,
    required this.gradeName,
    required this.sectionName,
    required this.attendancePercent,
    required this.feeBalance,
  });

  final int studentId;
  final String studentNo;
  final String nameAr;
  final String nameEn;

  /// True when the caller is the student rather than a guardian.
  final bool isSelf;

  final String? gradeName;
  final String? sectionName;

  /// BR-ATD-009 for the working year; null when the student has no enrollment.
  final double? attendancePercent;

  /// Positive is owed. Null when the fee gate refused for this caller.
  final double? feeBalance;

  static PortalChild fromJson(Map<String, dynamic> json) => PortalChild(
        studentId: asInt(json['studentId']),
        studentNo: asString(json['studentNo']),
        nameAr: asString(json['nameAr']),
        nameEn: asString(json['nameEn']),
        isSelf: asBool(json['isSelf']),
        gradeName: asStringOrNull(json['gradeName']),
        sectionName: asStringOrNull(json['sectionName']),
        attendancePercent: asDecimal(json['attendancePercent']),
        feeBalance: asDecimal(json['feeBalance']),
      );
}

/// BR-ATD-009 as the portal states it, over the working academic year.
class PortalAttendance {
  const PortalAttendance({
    required this.scheduledDays,
    required this.exemptedDays,
    required this.absentDays,
    required this.attendancePercent,
  });

  final int scheduledDays;
  final int exemptedDays;
  final int absentDays;
  final double attendancePercent;

  /// Nothing has been recorded yet — which the screen must say, rather than
  /// showing a confident 0%.
  bool get isEmpty => scheduledDays == 0;

  static PortalAttendance fromJson(Map<String, dynamic> json) =>
      PortalAttendance(
        scheduledDays: asInt(json['scheduledDays']),
        exemptedDays: asInt(json['exemptedDays']),
        absentDays: asInt(json['absentDays']),
        attendancePercent: asDecimal(json['attendancePercent']) ?? 0,
      );
}

/// One published term result. BR-SEC-012: drafts do not exist out here.
class PortalResult {
  const PortalResult({
    required this.subjectNameAr,
    required this.subjectNameEn,
    required this.termName,
    required this.scorePercent,
    required this.bandCode,
    required this.publishedAtUtc,
  });

  final String subjectNameAr;
  final String subjectNameEn;
  final String? termName;
  final double scorePercent;
  final String? bandCode;
  final DateTime? publishedAtUtc;

  static PortalResult fromJson(Map<String, dynamic> json) => PortalResult(
        subjectNameAr: asString(json['subjectNameAr']),
        subjectNameEn: asString(json['subjectNameEn']),
        termName: asStringOrNull(json['termName']),
        scorePercent: asDecimal(json['scorePercent']) ?? 0,
        bandCode: asStringOrNull(json['bandCode']),
        publishedAtUtc: asUtcDate(json['publishedAtUtc']),
      );
}

/// The family's money for one student. Gross and discounts are reported apart
/// and never netted invisibly (BR-DIS-010), so this screen shows all three.
class PortalFees {
  const PortalFees({
    required this.studentId,
    required this.position,
    required this.grossCharges,
    required this.discounts,
    required this.currency,
    required this.charges,
  });

  final int studentId;
  final double position;
  final double grossCharges;
  final double discounts;
  final String currency;
  final List<PortalChargeLine> charges;

  static PortalFees fromJson(Map<String, dynamic> json) => PortalFees(
        studentId: asInt(json['studentId']),
        position: asDecimal(json['position']) ?? 0,
        grossCharges: asDecimal(json['grossCharges']) ?? 0,
        discounts: asDecimal(json['discounts']) ?? 0,
        currency: asString(json['currency']),
        charges: asObjectList(json['charges'])
            .map(PortalChargeLine.fromJson)
            .toList(growable: false),
      );
}

/// One posted charge. Void charges never appear (BR-SEC-012).
class PortalChargeLine {
  const PortalChargeLine({
    required this.chargeNo,
    required this.grossAmount,
    required this.postedAtUtc,
  });

  final String chargeNo;
  final double grossAmount;
  final DateTime? postedAtUtc;

  static PortalChargeLine fromJson(Map<String, dynamic> json) =>
      PortalChargeLine(
        chargeNo: asString(json['chargeNo']),
        grossAmount: asDecimal(json['grossAmount']) ?? 0,
        postedAtUtc: asUtcDate(json['postedAtUtc']),
      );
}

/// The whole family's position in one figure, plus the per-student breakdown.
class PortalStatement {
  const PortalStatement({
    required this.total,
    required this.currency,
    required this.students,
  });

  final double total;
  final String currency;
  final List<PortalFees> students;

  static PortalStatement fromJson(Map<String, dynamic> json) => PortalStatement(
        total: asDecimal(json['total']) ?? 0,
        currency: asString(json['currency']),
        students: asObjectList(json['students'])
            .map(PortalFees.fromJson)
            .toList(growable: false),
      );
}

/// doc/Modules/37 §8.10 — one piece of set work.
///
/// It carries no submission and no mark, because the domain has neither yet.
/// The API says so in as many words, and this app must not imply otherwise by
/// offering an upload button that would have nowhere to post.
class PortalHomework {
  const PortalHomework({
    required this.homeworkId,
    required this.titleAr,
    required this.titleEn,
    required this.instructionsAr,
    required this.instructionsEn,
    required this.subjectNameAr,
    required this.subjectNameEn,
    required this.dueDate,
    required this.maxMarks,
    required this.latePenaltyApplies,
    required this.latePenaltyPercent,
  });

  final int homeworkId;
  final String titleAr;
  final String titleEn;
  final String? instructionsAr;
  final String? instructionsEn;
  final String subjectNameAr;
  final String subjectNameEn;
  final DateTime? dueDate;

  /// BR-LRN-004: null means ungraded practice, which the screen says rather
  /// than showing a blank mark.
  final double? maxMarks;

  final bool latePenaltyApplies;
  final double? latePenaltyPercent;

  static PortalHomework fromJson(Map<String, dynamic> json) => PortalHomework(
        homeworkId: asInt(json['homeworkId']),
        titleAr: asString(json['titleAr']),
        titleEn: asString(json['titleEn']),
        instructionsAr: asStringOrNull(json['instructionsAr']),
        instructionsEn: asStringOrNull(json['instructionsEn']),
        subjectNameAr: asString(json['subjectNameAr']),
        subjectNameEn: asString(json['subjectNameEn']),
        dueDate: asUtcDate(json['dueDate']),
        maxMarks: asDecimal(json['maxMarks']),
        latePenaltyApplies: asBool(json['latePenaltyApplies']),
        latePenaltyPercent: asDecimal(json['latePenaltyPercent']),
      );
}

/// doc/Modules/37 §5 — one published lesson and its material.
class PortalLesson {
  const PortalLesson({
    required this.lessonId,
    required this.weekNumber,
    required this.titleAr,
    required this.titleEn,
    required this.objectivesAr,
    required this.objectivesEn,
    required this.subjectNameAr,
    required this.subjectNameEn,
    required this.resources,
  });

  final int lessonId;
  final int weekNumber;
  final String titleAr;
  final String titleEn;
  final String? objectivesAr;
  final String? objectivesEn;
  final String subjectNameAr;
  final String subjectNameEn;
  final List<PortalLessonResource> resources;

  static PortalLesson fromJson(Map<String, dynamic> json) => PortalLesson(
        lessonId: asInt(json['lessonId']),
        weekNumber: asInt(json['weekNumber']),
        titleAr: asString(json['titleAr']),
        titleEn: asString(json['titleEn']),
        objectivesAr: asStringOrNull(json['objectivesAr']),
        objectivesEn: asStringOrNull(json['objectivesEn']),
        subjectNameAr: asString(json['subjectNameAr']),
        subjectNameEn: asString(json['subjectNameEn']),
        resources: asObjectList(json['resources'])
            .map(PortalLessonResource.fromJson)
            .toList(growable: false),
      );
}

/// One downloadable item. BR-LRN-006: a resource whose current version is not
/// scan-clean is never listed, so everything here was fetchable when listed —
/// and the gate is applied again at the download, which is why this app opens
/// the URL rather than caching bytes.
class PortalLessonResource {
  const PortalLessonResource({
    required this.resourceId,
    required this.titleAr,
    required this.titleEn,
    required this.downloadUrl,
  });

  final int resourceId;
  final String titleAr;
  final String titleEn;
  final String downloadUrl;

  static PortalLessonResource fromJson(Map<String, dynamic> json) =>
      PortalLessonResource(
        resourceId: asInt(json['resourceId']),
        titleAr: asString(json['titleAr']),
        titleEn: asString(json['titleEn']),
        downloadUrl: asString(json['downloadUrl']),
      );
}

/// BR-SEC-012: only a sent announcement ever reaches a family.
class PortalAnnouncement {
  const PortalAnnouncement({
    required this.id,
    required this.titleAr,
    required this.titleEn,
    required this.bodyAr,
    required this.bodyEn,
    required this.sentAtUtc,
  });

  final int id;
  final String titleAr;
  final String titleEn;
  final String? bodyAr;
  final String? bodyEn;
  final DateTime? sentAtUtc;

  static PortalAnnouncement fromJson(Map<String, dynamic> json) =>
      PortalAnnouncement(
        id: asInt(json['id']),
        titleAr: asString(json['titleAr']),
        titleEn: asString(json['titleEn']),
        bodyAr: asStringOrNull(json['bodyAr']),
        bodyEn: asStringOrNull(json['bodyEn']),
        sentAtUtc: asUtcDate(json['sentAtUtc']),
      );
}

/// One week of the student's section timetable, already flattened by the server
/// into the per-day list a phone renders (doc/Modules/15 §11).
class PortalTimetable {
  const PortalTimetable({
    required this.sectionName,
    required this.weekStart,
    required this.entries,
  });

  final String? sectionName;
  final DateTime? weekStart;
  final List<TimetableEntry> entries;

  bool get isEmpty => entries.isEmpty;

  /// Grouped the way the screen shows it: one section per weekday, in period
  /// order. Sunday is 0, per `System.DayOfWeek`.
  Map<int, List<TimetableEntry>> byDay() {
    final Map<int, List<TimetableEntry>> days = <int, List<TimetableEntry>>{};
    for (final TimetableEntry entry in entries) {
      days.putIfAbsent(entry.dayOfWeek, () => <TimetableEntry>[]).add(entry);
    }
    for (final List<TimetableEntry> day in days.values) {
      day.sort((TimetableEntry a, TimetableEntry b) =>
          a.periodSequence.compareTo(b.periodSequence));
    }
    return days;
  }

  static PortalTimetable fromJson(Map<String, dynamic> json) => PortalTimetable(
        sectionName: asStringOrNull(json['sectionName']),
        weekStart: asUtcDate(json['weekStart']),
        entries: asObjectList(json['entries'])
            .map(TimetableEntry.fromJson)
            .toList(growable: false),
      );
}

class TimetableEntry {
  const TimetableEntry({
    required this.dayOfWeek,
    required this.periodSequence,
    required this.startTime,
    required this.endTime,
    required this.subjectNameAr,
    required this.subjectNameEn,
    required this.teacherNameAr,
    required this.teacherNameEn,
    required this.roomName,
    required this.changeKind,
  });

  /// Sunday = 0, per `System.DayOfWeek`.
  final int dayOfWeek;
  final int periodSequence;
  final String? startTime;
  final String? endTime;
  final String subjectNameAr;
  final String subjectNameEn;
  final String? teacherNameAr;
  final String? teacherNameEn;
  final String? roomName;

  /// BR-TTB-008: this week's dated overlay when there is one — a substitution,
  /// a room change, or a cancellation. Null on an ordinary week.
  final String? changeKind;

  static TimetableEntry fromJson(Map<String, dynamic> json) => TimetableEntry(
        dayOfWeek: asInt(json['dayOfWeek']),
        periodSequence: asInt(json['periodSequence']),
        startTime: asStringOrNull(json['startTime']),
        endTime: asStringOrNull(json['endTime']),
        subjectNameAr: asString(json['subjectNameAr']),
        subjectNameEn: asString(json['subjectNameEn']),
        teacherNameAr: asStringOrNull(json['teacherNameAr']),
        teacherNameEn: asStringOrNull(json['teacherNameEn']),
        roomName: asStringOrNull(json['roomName']),
        changeKind: asStringOrNull(json['changeKind']),
      );
}
