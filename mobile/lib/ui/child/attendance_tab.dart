import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../theme.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// BR-ATD-009 for the working academic year.
class AttendanceTab extends StatelessWidget {
  const AttendanceTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<PortalAttendance>(
      load: () => auth.api.attendance(studentId),
      builder: (BuildContext context, PortalAttendance a) {
        if (a.isEmpty) {
          // Nothing has been recorded yet. Saying so is the honest answer; a
          // confident 0% would read as a child who never attended.
          return ListView(
            padding: const EdgeInsets.all(24),
            children: <Widget>[
              EmptyView(message: s.noAttendance, section: Section.attendance),
            ],
          );
        }

        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: <Widget>[
            Panel(
              children: <Widget>[
                BigStat(
                  section: Section.attendance,
                  label: s.attendanceRate,
                  value: Fmt.percent(a.attendancePercent, s.lang),
                  progress: a.attendancePercent / 100,
                ),
              ],
            ),
            const SizedBox(height: 12),
            Panel(
              children: <Widget>[
                Fact(
                  label: s.scheduledDays,
                  value: Fmt.marks(a.scheduledDays.toDouble(), s.lang),
                  icon: Icons.calendar_month_rounded,
                  iconColor: Section.timetable.color,
                  numeric: true,
                ),
                const Divider(height: 18),
                Fact(
                  label: s.absentDays,
                  value: Fmt.marks(a.absentDays.toDouble(), s.lang),
                  icon: Icons.event_busy_rounded,
                  // Absences are the figure a parent came to check; red when
                  // there are any, muted when the answer is none.
                  iconColor:
                      a.absentDays > 0 ? AppColors.danger : AppColors.muted,
                  numeric: true,
                ),
                const Divider(height: 18),
                Fact(
                  label: s.exemptedDays,
                  value: Fmt.marks(a.exemptedDays.toDouble(), s.lang),
                  icon: Icons.verified_user_rounded,
                  iconColor: AppColors.success,
                  numeric: true,
                ),
              ],
            ),
          ],
        );
      },
    );
  }
}
