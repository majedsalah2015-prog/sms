import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../theme.dart';
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
            children: <Widget>[
              EmptyView(message: s.noTimetable, section: Section.timetable),
            ],
          );
        }

        final Map<int, List<TimetableEntry>> days = week.byDay();
        final List<int> ordered = days.keys.toList()..sort();
        final int today = DateTime.now().weekday % 7; // Sunday = 0, as the API sends

        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: <Widget>[
            Row(
              children: <Widget>[
                const Icon(Icons.date_range_rounded,
                    size: 15, color: AppColors.muted),
                const SizedBox(width: 6),
                Text(
                  '${s.weekOf} '
                  '${Fmt.date(week.weekStart, s.lang)}',
                  style: Theme.of(context)
                      .textTheme
                      .bodySmall
                      ?.copyWith(color: AppColors.muted),
                ),
              ],
            ),
            const SizedBox(height: 12),
            for (final int day in ordered) ...<Widget>[
              Panel(
                section: Section.timetable,
                title: s.weekday(day),
                subtitle: '${days[day]!.length} · ${s.period}',
                // Today is marked so a parent checking "what does she have now"
                // does not have to work out which row is which.
                trailing: day == today
                    ? Pill(
                        text: s.today,
                        tone: Section.timetable.color,
                        icon: Icons.today_rounded,
                      )
                    : null,
                children: <Widget>[
                  for (int i = 0; i < days[day]!.length; i++) ...<Widget>[
                    if (i > 0) const Divider(height: 18),
                    _PeriodRow(entry: days[day]![i]),
                  ],
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
    final bool changed = (entry.changeKind ?? '').isNotEmpty;

    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Container(
          width: 38,
          height: 38,
          decoration: BoxDecoration(
            color: Section.timetable.wash,
            borderRadius: BorderRadius.circular(11),
          ),
          alignment: Alignment.center,
          child: Text(
            '${entry.periodSequence}',
            style: theme.textTheme.titleSmall?.copyWith(
              color: Section.timetable.color,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                s.pair(entry.subjectNameEn, entry.subjectNameAr),
                style: theme.textTheme.bodyLarge
                    ?.copyWith(fontWeight: FontWeight.w500),
              ),
              const SizedBox(height: 3),
              Wrap(
                spacing: 10,
                runSpacing: 4,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: <Widget>[
                  if (time.isNotEmpty)
                    _Meta(
                      icon: Icons.schedule_rounded,
                      // A time range reads left-to-right in both languages.
                      child: Directionality(
                        textDirection: TextDirection.ltr,
                        child: Text(
                          time,
                          style: theme.textTheme.bodySmall
                              ?.copyWith(color: AppColors.muted),
                        ),
                      ),
                    ),
                  if (teacher.isNotEmpty)
                    _Meta(
                      icon: Icons.person_outline_rounded,
                      child: Text(
                        teacher,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: AppColors.muted),
                      ),
                    ),
                  if ((entry.roomName ?? '').isNotEmpty)
                    _Meta(
                      icon: Icons.meeting_room_outlined,
                      child: Text(
                        entry.roomName!,
                        style: theme.textTheme.bodySmall
                            ?.copyWith(color: AppColors.muted),
                      ),
                    ),
                ],
              ),
              if (changed) ...<Widget>[
                const SizedBox(height: 6),
                Pill(
                  text: s.changeKind(entry.changeKind),
                  tone: AppColors.warning,
                  icon: Icons.change_circle_rounded,
                ),
              ],
            ],
          ),
        ),
      ],
    );
  }
}

class _Meta extends StatelessWidget {
  const _Meta({required this.icon, required this.child});

  final IconData icon;
  final Widget child;

  @override
  Widget build(BuildContext context) {
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: <Widget>[
        Icon(icon, size: 13, color: AppColors.muted),
        const SizedBox(width: 4),
        child,
      ],
    );
  }
}
