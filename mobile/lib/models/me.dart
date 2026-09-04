import 'json.dart';

/// `GET /api/v1/auth/me` — who is signed in and what they may open.
///
/// This is the app's first call after sign-in and is cached for the session.
/// [permissions] is the whole point of it: the menu is built from the list the
/// server's own `IPermissionService` produced, rather than from calling
/// endpoints to see which ones answer 404. The two cannot drift apart that way
/// (BR-SEC-010).
class Me {
  const Me({
    required this.userAccountId,
    required this.userName,
    required this.accountType,
    required this.schoolNameAr,
    required this.schoolNameEn,
    required this.workingAcademicYearName,
    required this.mustChangePassword,
    required this.sessionExpiresAtUtc,
    required this.subject,
    required this.children,
    required this.permissions,
  });

  final int userAccountId;
  final String userName;

  /// `Staff` / `Parent` / `Student` / `System`.
  final String accountType;

  final String schoolNameAr;
  final String schoolNameEn;
  final String? workingAcademicYearName;
  final bool mustChangePassword;
  final DateTime? sessionExpiresAtUtc;

  /// The person this account is, when it is one.
  final MeSubject? subject;

  /// A parent's children, or a student's own record. The ids the portal
  /// endpoints take.
  final List<MeChild> children;

  /// Every catalogued permission, as `MODULE/Screen/Verb`.
  final Set<String> permissions;

  bool get isStudent => accountType == 'Student';
  bool get isParent => accountType == 'Parent';

  /// A portal account is what this app is for. A staff sign-in reaches the API
  /// but none of the `POR/*` screens, and saying so beats an empty home screen.
  bool get isPortalAccount => isStudent || isParent;

  bool can(String permission) => permissions.contains(permission);

  static Me fromJson(Map<String, dynamic> json) => Me(
        userAccountId: asInt(json['userAccountId']),
        userName: asString(json['userName']),
        accountType: asString(json['accountType']),
        schoolNameAr: asString(json['schoolNameAr']),
        schoolNameEn: asString(json['schoolNameEn']),
        workingAcademicYearName: asStringOrNull(json['workingAcademicYearName']),
        mustChangePassword: asBool(json['mustChangePassword']),
        sessionExpiresAtUtc: asUtcDate(json['sessionExpiresAtUtc']),
        subject: json['subject'] is Map<String, dynamic>
            ? MeSubject.fromJson(json['subject'] as Map<String, dynamic>)
            : null,
        children: asObjectList(json['children'])
            .map(MeChild.fromJson)
            .toList(growable: false),
        permissions: (json['permissions'] is List)
            ? (json['permissions'] as List)
                .whereType<String>()
                .toSet()
            : <String>{},
      );
}

/// The screens this app opens, named exactly as `ScreenCatalog` catalogues them.
/// Naming them once here keeps a typo from silently hiding a tab forever.
abstract final class PortalPermissions {
  static const String home = 'POR/Home/View';
  static const String child = 'POR/Child/View';
  static const String statement = 'POR/Statement/View';
  static const String work = 'POR/Work/View';
  static const String lessons = 'POR/Lessons/View';
  static const String announcements = 'POR/Announcements/View';
}

class MeSubject {
  const MeSubject({
    required this.kind,
    required this.id,
    required this.nameAr,
    required this.nameEn,
    required this.reference,
  });

  /// `Student`, `Parent` or `Employee`.
  final String kind;
  final int id;
  final String nameAr;
  final String nameEn;

  /// Student number or employee number, whichever applies.
  final String? reference;

  static MeSubject fromJson(Map<String, dynamic> json) => MeSubject(
        kind: asString(json['kind']),
        id: asInt(json['id']),
        nameAr: asString(json['nameAr']),
        nameEn: asString(json['nameEn']),
        reference: asStringOrNull(json['reference']),
      );
}

class MeChild {
  const MeChild({
    required this.studentId,
    required this.studentNo,
    required this.nameAr,
    required this.nameEn,
  });

  final int studentId;
  final String studentNo;
  final String nameAr;
  final String nameEn;

  static MeChild fromJson(Map<String, dynamic> json) => MeChild(
        studentId: asInt(json['studentId']),
        studentNo: asString(json['studentNo']),
        nameAr: asString(json['nameAr']),
        nameEn: asString(json['nameEn']),
      );
}

/// What `POST /auth/login` and `POST /auth/two-factor` answer. Exactly one of
/// [token] and [twoFactorToken] is ever set.
class LoginResult {
  const LoginResult({
    required this.token,
    required this.expiresAtUtc,
    required this.requiresTwoFactor,
    required this.twoFactorToken,
    required this.mustChangePassword,
  });

  final String? token;
  final DateTime? expiresAtUtc;
  final bool requiresTwoFactor;

  /// Five minutes of proof that the password was accepted. Not a session, and
  /// it grants nothing on its own.
  final String? twoFactorToken;

  final bool mustChangePassword;

  static LoginResult fromJson(Map<String, dynamic> json) => LoginResult(
        token: asStringOrNull(json['token']),
        expiresAtUtc: asUtcDate(json['expiresAtUtc']),
        requiresTwoFactor: asBool(json['requiresTwoFactor']),
        twoFactorToken: asStringOrNull(json['twoFactorToken']),
        mustChangePassword: asBool(json['mustChangePassword']),
      );
}
