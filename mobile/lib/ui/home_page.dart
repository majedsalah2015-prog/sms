import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../app_version.dart';
import '../l10n/strings.dart';
import '../models/me.dart';
import '../models/portal.dart';
import '../state/auth_controller.dart';
import 'announcements_page.dart';
import 'change_password_page.dart';
import 'child_page.dart';
import 'format.dart';
import 'statement_page.dart';
import 'theme.dart';
import 'widgets/async_view.dart';
import 'widgets/update_banner.dart';

/// The family, as `GET /portal/children` reports it.
///
/// One call fills this screen: each row already carries the year's attendance
/// percentage and what is outstanding, so a parent with four children does not
/// wait on nine round trips. Either figure may be absent on its own — a
/// guardian who may see the child but not the money is a real arrangement, and
/// the row still belongs here.
class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();
    final Me? me = auth.me;

    return Scaffold(
      appBar: AppBar(
        titleSpacing: 12,
        title: Row(
          children: <Widget>[
            Container(
              width: 32,
              height: 32,
              decoration: BoxDecoration(
                color: AppColors.primary,
                borderRadius: BorderRadius.circular(10),
              ),
              child: const Icon(
                Icons.school_rounded,
                size: 18,
                color: Colors.white,
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                me == null
                    ? s.appTitle
                    : s.pair(me.schoolNameEn, me.schoolNameAr),
                overflow: TextOverflow.ellipsis,
              ),
            ),
          ],
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => auth.setLanguage(auth.isArabic ? 'en' : 'ar'),
            child: Text(s.languageToggle),
          ),
          PopupMenuButton<String>(
            icon: const Icon(Icons.more_vert_rounded),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(14),
            ),
            onSelected: (String value) => _onMenu(context, value),
            itemBuilder: (BuildContext context) => <PopupMenuEntry<String>>[
              if (me != null && me.can(PortalPermissions.statement))
                _menuItem('statement', Section.fees, s.statement),
              if (me != null && me.can(PortalPermissions.announcements))
                _menuItem('announcements', Section.announcements,
                    s.announcements),
              _menuItem('password', Section.account, s.changePasswordTitle),
              const PopupMenuDivider(),
              // Not an action — the answer to "which build am I running?", where
              // a family will look for it once they have been asked to update.
              PopupMenuItem<String>(
                enabled: false,
                height: 36,
                child: Row(
                  children: <Widget>[
                    const Icon(Icons.info_outline_rounded,
                        size: 18, color: AppColors.muted),
                    const SizedBox(width: 12),
                    Text(
                      appVersionLabel,
                      textDirection: TextDirection.ltr,
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.muted),
                    ),
                  ],
                ),
              ),
              PopupMenuItem<String>(
                value: 'signout',
                child: Row(
                  children: <Widget>[
                    const Icon(Icons.logout_rounded,
                        size: 20, color: AppColors.danger),
                    const SizedBox(width: 12),
                    Text(
                      s.signOut,
                      style: const TextStyle(color: AppColors.danger),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ],
      ),
      body: Column(
        children: <Widget>[
          // Draws nothing at all when the school has nothing to say, so it sits
          // here unconditionally rather than behind a second condition that
          // would have to be kept in step with the controller's own.
          const UpdateBanner(),
          Expanded(
            child: me != null && !me.can(PortalPermissions.home)
                // The account signed in but holds no portal home. A staff account
                // reaching this app is the usual cause, and saying so beats an
                // empty list that looks like a school with no students in it.
                ? Padding(
                    padding: const EdgeInsets.all(24),
                    child: EmptyView(
                        message: s.noChildren, section: Section.family),
                  )
                : AsyncView<List<PortalChild>>(
                    load: () => auth.api.children(),
                    empty: s.noChildren,
                    emptySection: Section.family,
                    builder:
                        (BuildContext context, List<PortalChild> children) {
                      final bool self = children.isNotEmpty &&
                          children.every((PortalChild c) => c.isSelf);
                      return ListView.separated(
                        padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
                        itemCount: children.length + 1,
                        separatorBuilder: (_, __) => const SizedBox(height: 12),
                        itemBuilder: (BuildContext context, int index) {
                          if (index == 0) {
                            return Padding(
                              padding: const EdgeInsets.only(bottom: 4),
                              child: SectionHeader(
                                section: Section.family,
                                title: self ? s.myFile : s.myChildren,
                                subtitle: me?.workingAcademicYearName,
                              ),
                            );
                          }
                          return _ChildCard(child: children[index - 1]);
                        },
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }

  static PopupMenuItem<String> _menuItem(
    String value,
    Section section,
    String label,
  ) {
    return PopupMenuItem<String>(
      value: value,
      child: Row(
        children: <Widget>[
          Icon(section.icon, size: 20, color: section.color),
          const SizedBox(width: 12),
          Text(label),
        ],
      ),
    );
  }

  Future<void> _onMenu(BuildContext context, String value) async {
    final AuthController auth = context.read<AuthController>();
    final Strings s = Strings.of(context);

    switch (value) {
      case 'statement':
        await Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const StatementPage()),
        );
        break;
      case 'announcements':
        await Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const AnnouncementsPage()),
        );
        break;
      case 'password':
        await Navigator.of(context).push(
          MaterialPageRoute<void>(builder: (_) => const ChangePasswordPage()),
        );
        break;
      case 'signout':
        final bool confirmed = await showDialog<bool>(
              context: context,
              builder: (BuildContext context) => AlertDialog(
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(18),
                ),
                icon: const Icon(Icons.logout_rounded,
                    color: AppColors.danger, size: 28),
                content: Text(s.signOutConfirm, textAlign: TextAlign.center),
                actions: <Widget>[
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(false),
                    child: Text(s.cancel),
                  ),
                  FilledButton(
                    style: FilledButton.styleFrom(
                      backgroundColor: AppColors.danger,
                      minimumSize: const Size(110, 44),
                    ),
                    onPressed: () => Navigator.of(context).pop(true),
                    child: Text(s.signOut),
                  ),
                ],
              ),
            ) ??
            false;
        if (confirmed) await auth.signOut();
        break;
    }
  }
}

class _ChildCard extends StatelessWidget {
  const _ChildCard({required this.child});

  final PortalChild child;

  /// A stable colour per student, so a parent of three learns which card is
  /// which by its colour before reading the name. Derived from the id rather
  /// than the list position — the row must not change colour when a sibling
  /// leaves the school.
  Color get _accent {
    const List<Color> palette = <Color>[
      Color(0xFF2563EB),
      Color(0xFF7C3AED),
      Color(0xFF0D9488),
      Color(0xFFEA580C),
      Color(0xFFDB2777),
      Color(0xFF4F46E5),
    ];
    return palette[child.studentId.abs() % palette.length];
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final String name = s.pair(child.nameEn, child.nameAr);
    final String placement = <String>[
      if ((child.gradeName ?? '').isNotEmpty) child.gradeName!,
      if ((child.sectionName ?? '').isNotEmpty) child.sectionName!,
    ].join(' · ');

    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (_) => ChildPage(studentId: child.studentId, name: name),
          ),
        ),
        child: Column(
          children: <Widget>[
            Padding(
              padding: const EdgeInsets.fromLTRB(16, 16, 16, 12),
              child: Row(
                children: <Widget>[
                  Container(
                    width: 46,
                    height: 46,
                    decoration: BoxDecoration(
                      gradient: LinearGradient(
                        colors: <Color>[_accent, _accent.withValues(alpha: 0.72)],
                        begin: Alignment.topLeft,
                        end: Alignment.bottomRight,
                      ),
                      borderRadius: BorderRadius.circular(14),
                    ),
                    alignment: Alignment.center,
                    child: Text(
                      name.isEmpty ? '?' : name.characters.first,
                      style: const TextStyle(
                        color: Colors.white,
                        fontSize: 20,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          child.isSelf && name.isEmpty ? s.myFile : name,
                          style: theme.textTheme.titleMedium?.copyWith(
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                        if (placement.isNotEmpty)
                          Padding(
                            padding: const EdgeInsets.only(top: 2),
                            child: Text(
                              placement,
                              style: theme.textTheme.bodySmall
                                  ?.copyWith(color: AppColors.muted),
                            ),
                          ),
                      ],
                    ),
                  ),
                  Icon(
                    Directionality.of(context) == TextDirection.rtl
                        ? Icons.chevron_left_rounded
                        : Icons.chevron_right_rounded,
                    color: AppColors.muted,
                  ),
                ],
              ),
            ),
            const Divider(height: 1),
            Row(
              children: <Widget>[
                Expanded(
                  child: _Stat(
                    section: Section.attendance,
                    label: s.attendance,
                    // Null here is "not shared with this caller", never zero.
                    value: child.attendancePercent == null
                        ? s.notShared
                        : Fmt.percent(child.attendancePercent, s.lang),
                    dimmed: child.attendancePercent == null,
                  ),
                ),
                Container(width: 1, height: 44, color: AppColors.border),
                Expanded(
                  child: _Stat(
                    section: Section.fees,
                    label: s.outstanding,
                    value: child.feeBalance == null
                        ? s.notShared
                        : Fmt.money(child.feeBalance, '', s.lang),
                    dimmed: child.feeBalance == null,
                    alert: (child.feeBalance ?? 0) > 0,
                  ),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _Stat extends StatelessWidget {
  const _Stat({
    required this.section,
    required this.label,
    required this.value,
    this.dimmed = false,
    this.alert = false,
  });

  final Section section;
  final String label;
  final String value;
  final bool dimmed;
  final bool alert;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color tone = dimmed
        ? AppColors.muted
        : alert
            ? AppColors.danger
            : section.color;

    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
      child: Row(
        children: <Widget>[
          Icon(section.icon, size: 18, color: tone),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  label,
                  style: theme.textTheme.bodySmall
                      ?.copyWith(color: AppColors.muted),
                ),
                const SizedBox(height: 1),
                // Figures stay left-to-right in both languages — this product's
                // rule, and the reason a parent can read an amount back to the
                // accounts office and have it match the receipt.
                Directionality(
                  textDirection: TextDirection.ltr,
                  child: Align(
                    alignment: AlignmentDirectional.centerStart,
                    child: Text(
                      value,
                      style: theme.textTheme.titleSmall?.copyWith(
                        color: tone,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
