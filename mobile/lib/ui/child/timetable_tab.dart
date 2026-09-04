import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// The student's week (doc/Modules/15 §11), as a list per day rather than a
/// grid — a five-by-eight grid is unreadable on a phone, which is why the
/// server flattens it before sending.
///
/// BR-TTB-008's dated overlays are already folded in, so a substitution or a
/// room change shows here the moment it is made.
class TimetableTab extends StatelessWidget {
  const TimetableTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<PortalTimetable>(
      load: () => auth.api.timetable(studentId),
      builder: (BuildContext context, PortalTimetable week) {
        if (week.isEmpty) {
          return ListView(
            padding: const EdgeInsets.all(24),
            children: <Widget>[EmptyView(message: s.noTimetable)],
          );
        }

        final Map<int, List<TimetableEntry>> days = week.byDay();
        final List<int> ordered = days.keys.toList()..sort();

        return ListView(
          padding: const EdgeInsets.all(16),
          children: <Widget>[
            Text(
              '${s.weekOf} '
              '${Fmt.date(week.weekStart, s.isArabic ? 'ar' : 'en')}',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                    color: Theme.of(context).colorScheme.onSurfaceVariant,
                  ),
            ),
            const SizedBox(height: 12),
            for (final int day in ordered) ...<Widget>[
              Panel(
                title: s.weekday(day),
                children: <Widget>[
                  for (final TimetableEntry entry in days[day]!)
                    _PeriodRow(entry: entry),
                ],
              ),
              const SizedBox(height: 12),
            ],
          ],
        );
      },
    );
  }
}

class _PeriodRow extends StatelessWidget {
  const _PeriodRow({required this.entry});

  final TimetableEntry entry;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final String time = Fmt.timeRange(entry.startTime, entry.endTime);
    final String teacher =
        s.pair(entry.teacherNameEn, entry.teacherNameAr).trim();
    final String detail = <String>[
      if (teacher.isNotEmpty) teacher,
      if ((entry.roomName ?? '').isNotEmpty) '${s.room} ${entry.roomName}',
    ].join(' · ');

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          SizedBox(
            width: 56,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  '${entry.periodSequence}',
                  style: theme.textTheme.titleMedium,
                ),
                if (time.isNotEmpty)
                  // A time range reads left-to-right in both languages.
                  Directionality(
                    textDirection: TextDirection.ltr,
                    child: Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: Text(
                        time,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: theme.colorScheme.onSurfaceVariant,
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  s.pair(entry.subjectNameEn, entry.subjectNameAr),
                  style: theme.textTheme.bodyLarge,
                ),
                if (detail.isNotEmpty)
                  Text(
                    detail,
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                if ((entry.changeKind ?? '').isNotEmpty) ...<Widget>[
                  const SizedBox(height: 6),
                  Pill(
                    text: s.changeKind(entry.changeKind),
                    tone: theme.colorScheme.tertiaryContainer,
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}
