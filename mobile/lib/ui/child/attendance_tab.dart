import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
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
            children: <Widget>[EmptyView(message: s.noAttendance)],
          );
        }

        return ListView(
          padding: const EdgeInsets.all(16),
          children: <Widget>[
            Panel(
              children: <Widget>[
                _Rate(percent: a.attendancePercent),
                const SizedBox(height: 8),
                Fact(
                  label: s.scheduledDays,
                  value: Fmt.marks(a.scheduledDays.toDouble()),
                  numeric: true,
                ),
                Fact(
                  label: s.absentDays,
                  value: Fmt.marks(a.absentDays.toDouble()),
                  numeric: true,
                ),
                Fact(
                  label: s.exemptedDays,
                  value: Fmt.marks(a.exemptedDays.toDouble()),
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

class _Rate extends StatelessWidget {
  const _Rate({required this.percent});

  final double percent;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Text(
          s.attendanceRate,
          style: theme.textTheme.bodyMedium?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 4),
        Directionality(
          textDirection: TextDirection.ltr,
          child: Align(
            alignment: AlignmentDirectional.centerStart,
            child: Text(
              Fmt.percent(percent),
              style: theme.textTheme.displaySmall,
            ),
          ),
        ),
        const SizedBox(height: 12),
        ClipRRect(
          borderRadius: BorderRadius.circular(999),
          child: LinearProgressIndicator(
            value: (percent / 100).clamp(0, 1).toDouble(),
            minHeight: 8,
          ),
        ),
      ],
    );
  }
}
