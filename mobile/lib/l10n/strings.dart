import 'package:flutter/widgets.dart';

/// Every string this app puts on screen, in both languages.
///
/// The server already translates every *refusal* it gives
/// (docs/Integration/03-Mobile-API.md §3) and returns both halves of every
/// stored bilingual pair, so this table covers only what the client itself
/// chose to say: labels, headings, empty states, and the handful of failures
/// that happen before a response exists.
///
/// It is a plain table rather than the `gen_l10n` ARB pipeline on purpose. The
/// rest of this product says `T(en, ar)` in one helper per file and reads well
/// for it; a codegen step whose output is two strings per key would add a build
/// stage without adding a guarantee. The guarantee that matters — that no
/// English leaks into an Arabic screen — comes from there being no way to name
/// a string here without writing both halves.
class Strings {
  const Strings(this.isArabic);

  /// The one place the language is decided. Everything else asks this.
  final bool isArabic;

  static Strings of(BuildContext context) =>
      Strings(Localizations.localeOf(context).languageCode == 'ar');

  static const List<Locale> supportedLocales = <Locale>[
    Locale('ar'),
    Locale('en'),
  ];

  String _t(String en, String ar) => isArabic ? ar : en;

  /// Picks the half of a server-supplied bilingual pair this locale wants,
  /// falling back to the other when the school left one empty — a blank cell is
  /// worse than the wrong language.
  String pair(String? en, String? ar) {
    final String preferred = (isArabic ? ar : en) ?? '';
    if (preferred.trim().isNotEmpty) return preferred;
    return ((isArabic ? en : ar) ?? '').trim();
  }

  // ----------------------------------------------------------------- shell

  String get appTitle => _t('School Portal', 'بوابة المدرسة');
  String get retry => _t('Try again', 'أعد المحاولة');
  String get cancel => _t('Cancel', 'إلغاء');
  String get close => _t('Close', 'إغلاق');
  String get signOut => _t('Sign out', 'تسجيل الخروج');
  String get signOutConfirm =>
      _t('End this session on this device?', 'إنهاء الجلسة على هذا الجهاز؟');
  String get languageToggle => _t('العربية', 'English');
  String get nothingHere => _t('Nothing here yet', 'لا يوجد شيء بعد');
  String get offline => _t(
        'The school could not be reached. Check the connection and try again.',
        'تعذّر الوصول إلى المدرسة. تحقّق من الاتصال وأعد المحاولة.',
      );
  String get unexpected => _t(
        'Something went wrong. Try again in a moment.',
        'حدث خطأ غير متوقع. أعد المحاولة بعد قليل.',
      );

  // --------------------------------------------------------------- sign-in

  String get signIn => _t('Sign in', 'تسجيل الدخول');
  String get userName => _t('Username', 'اسم المستخدم');
  String get password => _t('Password', 'كلمة المرور');
  String get userNameRequired =>
      _t('Enter your username.', 'أدخل اسم المستخدم.');
  String get passwordRequired => _t('Enter your password.', 'أدخل كلمة المرور.');
  String get serverAddress => _t('School address', 'عنوان المدرسة');
  String get serverAddressHint => _t(
        'The address your school gave you, for example http://10.0.2.2:5099',
        'العنوان الذي زوّدتك به مدرستك، مثل http://10.0.2.2:5099',
      );
  String get serverAddressInvalid =>
      _t('That is not a valid address.', 'هذا العنوان غير صالح.');

  String get twoFactorTitle => _t('Verification code', 'رمز التحقق');
  String get twoFactorPrompt => _t(
        'Enter the six-digit code from your authenticator app.',
        'أدخل الرمز المكوّن من ستة أرقام من تطبيق المصادقة.',
      );
  String get twoFactorCode => _t('Code', 'الرمز');
  String get twoFactorRequired => _t('Enter the code.', 'أدخل الرمز.');
  String get verify => _t('Verify', 'تحقّق');

  String get changePasswordTitle =>
      _t('Change your password', 'غيّر كلمة المرور');
  String get changePasswordPrompt => _t(
        'Your school requires a new password before you can continue.',
        'تطلب مدرستك تعيين كلمة مرور جديدة قبل المتابعة.',
      );
  String get currentPassword => _t('Current password', 'كلمة المرور الحالية');
  String get newPassword => _t('New password', 'كلمة المرور الجديدة');
  String get confirmPassword =>
      _t('Confirm new password', 'تأكيد كلمة المرور الجديدة');
  String get passwordsDoNotMatch =>
      _t('The two passwords are not the same.', 'كلمتا المرور غير متطابقتين.');
  String get save => _t('Save', 'حفظ');
  String get passwordChanged =>
      _t('Your password has been changed.', 'تم تغيير كلمة المرور.');

  String get sessionEnded => _t(
        'Your session has ended. Sign in again.',
        'انتهت جلستك. سجّل الدخول من جديد.',
      );

  // ------------------------------------------------------------------ home

  String get myChildren => _t('My children', 'أبنائي');
  String get myFile => _t('My file', 'ملفي');
  String get noChildren => _t(
        'This account is not linked to any student yet. Ask the school office.',
        'هذا الحساب غير مرتبط بأي طالب بعد. راجع إدارة المدرسة.',
      );
  String get attendance => _t('Attendance', 'الحضور');
  String get outstanding => _t('Outstanding', 'المستحق');
  String get notShared => _t('Not shared', 'غير متاح');

  // ----------------------------------------------------------------- child

  String get overview => _t('Overview', 'نظرة عامة');
  String get results => _t('Results', 'النتائج');
  String get fees => _t('Fees', 'الرسوم');
  String get timetable => _t('Timetable', 'الجدول');
  String get homework => _t('Homework', 'الواجبات');
  String get lessons => _t('Lessons', 'الدروس');
  String get statement => _t('Statement', 'كشف الحساب');
  String get announcements => _t('Announcements', 'الإعلانات');

  String get scheduledDays => _t('School days', 'الأيام الدراسية');
  String get absentDays => _t('Absences', 'أيام الغياب');
  String get exemptedDays => _t('Excused', 'أيام معذورة');
  String get attendanceRate => _t('Attendance rate', 'نسبة الحضور');
  String get noAttendance => _t(
        'No attendance has been recorded for this year yet.',
        'لم يُسجَّل حضور لهذا العام بعد.',
      );

  String get noResults => _t(
        'No results have been published yet.',
        'لم تُعتمد أي نتائج بعد.',
      );
  String get term => _t('Term', 'الفصل');
  String get gradeBand => _t('Grade', 'التقدير');
  String get publishedOn => _t('Published', 'تاريخ الاعتماد');

  String get grossCharges => _t('Charges', 'إجمالي الرسوم');
  String get discounts => _t('Discounts', 'الخصومات');
  String get balance => _t('Balance', 'الرصيد');
  String get postedCharges => _t('Posted charges', 'الرسوم المُقيَّدة');
  String get chargeNo => _t('Charge no.', 'رقم القيد');
  String get noCharges =>
      _t('No charges have been posted.', 'لم تُقيَّد أي رسوم.');
  String get familyTotal => _t('Family total', 'إجمالي الأسرة');
  String get settled => _t('Nothing outstanding', 'لا يوجد مستحق');

  String get noTimetable => _t(
        'No timetable has been published for this section.',
        'لم يُنشر جدول لهذا الفصل.',
      );
  String get weekOf => _t('Week of', 'أسبوع');
  String get period => _t('Period', 'الحصة');
  String get room => _t('Room', 'القاعة');
  String get teacher => _t('Teacher', 'المعلم');

  String get noHomework =>
      _t('No homework has been set.', 'لا توجد واجبات مطلوبة.');
  String get dueOn => _t('Due', 'موعد التسليم');
  String get outOf => _t('Out of', 'من');
  String get ungraded => _t('Practice — not graded', 'تدريب — بدون درجة');
  String get latePenalty => _t('Late penalty', 'خصم التأخير');
  String get instructions => _t('Instructions', 'التعليمات');

  String get noLessons => _t(
        'No lessons have been published for this student yet.',
        'لم تُنشر دروس لهذا الطالب بعد.',
      );
  String get week => _t('Week', 'الأسبوع');
  String get objectives => _t('Objectives', 'الأهداف');
  String get materials => _t('Materials', 'المرفقات');
  String get openResource => _t('Open', 'فتح');
  String get openFailed => _t(
        'This device could not open that file.',
        'تعذّر على الجهاز فتح هذا الملف.',
      );

  String get noAnnouncements => _t('No announcements.', 'لا توجد إعلانات.');
  String get loadMore => _t('Load more', 'عرض المزيد');

  // ---------------------------------------------------------------- values

  /// The API sends `System.DayOfWeek`, where Sunday is 0.
  String weekday(int dayOfWeek) {
    const List<String> en = <String>[
      'Sunday',
      'Monday',
      'Tuesday',
      'Wednesday',
      'Thursday',
      'Friday',
      'Saturday',
    ];
    const List<String> ar = <String>[
      'الأحد',
      'الاثنين',
      'الثلاثاء',
      'الأربعاء',
      'الخميس',
      'الجمعة',
      'السبت',
    ];
    if (dayOfWeek < 0 || dayOfWeek > 6) return '';
    return isArabic ? ar[dayOfWeek] : en[dayOfWeek];
  }

  /// BR-TTB-008's dated overlay, as the timetable endpoint spells it.
  String changeKind(String? kind) {
    switch (kind) {
      case 'Substitution':
        return _t('Substitute teacher', 'معلم بديل');
      case 'RoomChange':
        return _t('Room changed', 'تغيير القاعة');
      case 'Cancellation':
        return _t('Cancelled', 'ملغاة');
      default:
        // An overlay kind this build does not know is still worth flagging;
        // inventing a translation for it would be worse than showing the code.
        return kind ?? '';
    }
  }
}
