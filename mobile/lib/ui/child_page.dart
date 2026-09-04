import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../models/me.dart';
import '../state/auth_controller.dart';
import 'child/attendance_tab.dart';
import 'child/fees_tab.dart';
import 'child/homework_tab.dart';
import 'child/lessons_tab.dart';
import 'child/results_tab.dart';
import 'child/timetable_tab.dart';
import 'widgets/async_view.dart';

/// One student, in as many tabs as this caller is allowed.
///
/// The tab list is built from `GET /auth/me`'s permissions, which are the
/// server's own evaluation — the same `IPermissionService` the endpoints guard
/// with. Hiding a tab is therefore not the security decision; the endpoint
/// answering 404 is (BR-SEC-010). This is only the half that stops a family
/// tapping into a refusal.
class ChildPage extends StatelessWidget {
  const ChildPage({required this.studentId, required this.name, super.key});

  final int studentId;
  final String name;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final Me? me = context.watch<AuthController>().me;

    final List<_Tab> tabs = <_Tab>[
      if (me == null || me.can(PortalPermissions.child)) ...<_Tab>[
        _Tab(s.overview, AttendanceTab(studentId: studentId)),
        _Tab(s.results, ResultsTab(studentId: studentId)),
        _Tab(s.timetable, TimetableTab(studentId: studentId)),
      ],
      if (me == null || me.can(PortalPermissions.statement))
        _Tab(s.fees, FeesTab(studentId: studentId)),
      if (me == null || me.can(PortalPermissions.work))
        _Tab(s.homework, HomeworkTab(studentId: studentId)),
      if (me == null || me.can(PortalPermissions.lessons))
        _Tab(s.lessons, LessonsTab(studentId: studentId)),
    ];

    if (tabs.isEmpty) {
      return Scaffold(
        appBar: AppBar(title: Text(name)),
        body: Padding(
          padding: const EdgeInsets.all(24),
          child: EmptyView(message: s.nothingHere),
        ),
      );
    }

    return DefaultTabController(
      length: tabs.length,
      child: Scaffold(
        appBar: AppBar(
          title: Text(name),
          bottom: TabBar(
            isScrollable: true,
            tabs: <Widget>[
              for (final _Tab tab in tabs) Tab(text: tab.label),
            ],
          ),
        ),
        body: TabBarView(
          children: <Widget>[
            for (final _Tab tab in tabs) tab.body,
          ],
        ),
      ),
    );
  }
}

class _Tab {
  const _Tab(this.label, this.body);

  final String label;
  final Widget body;
}
