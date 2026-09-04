import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../models/me.dart';
import '../models/portal.dart';
import '../state/auth_controller.dart';
import 'announcements_page.dart';
import 'change_password_page.dart';
import 'child_page.dart';
import 'format.dart';
import 'statement_page.dart';
import 'widgets/async_view.dart';

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
        title: Text(
          me == null ? s.appTitle : s.pair(me.schoolNameEn, me.schoolNameAr),
        ),
        actions: <Widget>[
          TextButton(
            onPressed: () => auth.setLanguage(auth.isArabic ? 'en' : 'ar'),
            child: Text(s.languageToggle),
          ),
          PopupMenuButton<String>(
            onSelected: (String value) => _onMenu(context, value),
            itemBuilder: (BuildContext context) => <PopupMenuEntry<String>>[
              if (me != null && me.can(PortalPermissions.statement))
                PopupMenuItem<String>(
                  value: 'statement',
                  child: Text(s.statement),
                ),
              if (me != null && me.can(PortalPermissions.announcements))
                PopupMenuItem<String>(
                  value: 'announcements',
                  child: Text(s.announcements),
                ),
              PopupMenuItem<String>(
                value: 'password',
                child: Text(s.changePasswordTitle),
              ),
              const PopupMenuDivider(),
              PopupMenuItem<String>(
                value: 'signout',
                child: Text(s.signOut),
              ),
            ],
          ),
        ],
      ),
      body: me != null && !me.can(PortalPermissions.home)
          // The account signed in but holds no portal home. A staff account
          // reaching this app is the usual cause, and saying so beats an empty
          // list that looks like a school with no students in it.
          ? Padding(
              padding: const EdgeInsets.all(24),
              child: EmptyView(message: s.noChildren),
            )
          : AsyncView<List<PortalChild>>(
              load: () => auth.api.children(),
              empty: s.noChildren,
              builder: (BuildContext context, List<PortalChild> children) {
                return ListView.separated(
                  padding: const EdgeInsets.all(16),
                  itemCount: children.length,
                  separatorBuilder: (_, __) => const SizedBox(height: 12),
                  itemBuilder: (BuildContext context, int index) =>
                      _ChildCard(child: children[index]),
                );
              },
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
                content: Text(s.signOutConfirm),
                actions: <Widget>[
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(false),
                    child: Text(s.cancel),
                  ),
                  FilledButton(
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
      child: InkWell(
        borderRadius: BorderRadius.circular(14),
        onTap: () => Navigator.of(context).push(
          MaterialPageRoute<void>(
            builder: (_) => ChildPage(studentId: child.studentId, name: name),
          ),
        ),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Row(
                children: <Widget>[
                  CircleAvatar(
                    backgroundColor: theme.colorScheme.primaryContainer,
                    child: Text(
                      name.isEmpty ? '?' : name.characters.first,
                      style: TextStyle(
                        color: theme.colorScheme.onPrimaryContainer,
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
                          style: theme.textTheme.titleMedium,
                        ),
                        if (placement.isNotEmpty)
                          Text(
                            placement,
                            style: theme.textTheme.bodySmall?.copyWith(
                              color: theme.colorScheme.onSurfaceVariant,
                            ),
                          ),
                      ],
                    ),
                  ),
                  const Icon(Icons.chevron_right),
                ],
              ),
              const SizedBox(height: 16),
              Row(
                children: <Widget>[
                  Expanded(
                    child: _Figure(
                      label: s.attendance,
                      // Null here is "not shared with this caller", never zero.
                      value: child.attendancePercent == null
                          ? s.notShared
                          : Fmt.percent(child.attendancePercent),
                    ),
                  ),
                  Expanded(
                    child: _Figure(
                      label: s.outstanding,
                      value: child.feeBalance == null
                          ? s.notShared
                          : Fmt.money(child.feeBalance, ''),
                      emphasis: (child.feeBalance ?? 0) > 0,
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _Figure extends StatelessWidget {
  const _Figure({
    required this.label,
    required this.value,
    this.emphasis = false,
  });

  final String label;
  final String value;
  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          label,
          style: theme.textTheme.bodySmall?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 2),
        // Figures stay left-to-right in both languages — this product's rule,
        // and the reason a parent can read an amount back to the accounts
        // office and have it match the receipt.
        Directionality(
          textDirection: TextDirection.ltr,
          child: Align(
            alignment: AlignmentDirectional.centerStart,
            child: Text(
              value,
              style: theme.textTheme.titleMedium?.copyWith(
                color: emphasis ? theme.colorScheme.error : null,
              ),
            ),
          ),
        ),
      ],
    );
  }
}
