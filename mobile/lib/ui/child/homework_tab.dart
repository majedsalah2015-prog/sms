import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../theme.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// doc/Modules/37 §8.10 — the work that has been set.
///
/// Read-only, and deliberately so: the domain has no submission entity yet, so
/// there is nothing for an upload button to post to. Offering one would promise
/// a family something the school could never receive. The gap is recorded in
/// docs/Integration/03-Mobile-API.md §6.
class HomeworkTab extends StatelessWidget {
  const HomeworkTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<List<PortalHomework>>(
      load: () => auth.api.homework(studentId),
      empty: s.noHomework,
      emptySection: Section.homework,
      builder: (BuildContext context, List<PortalHomework> items) {
        return ListView.separated(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          itemCount: items.length,
          separatorBuilder: (_, __) => const SizedBox(height: 12),
          itemBuilder: (BuildContext context, int index) =>
              _HomeworkCard(item: items[index]),
        );
      },
    );
  }
}

class _HomeworkCard extends StatelessWidget {
  const _HomeworkCard({required this.item});

  final PortalHomework item;

  /// Due today or already past is the thing a parent is scanning for, so it is
  /// the one state that gets a colour of its own.
  bool get _urgent {
    final DateTime? due = item.dueDate;
    if (due == null) return false;
    final DateTime today = DateTime.now().toUtc();
    return !due.isAfter(DateTime.utc(today.year, today.month, today.day));
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final String language = s.lang;

    return Panel(
      children: <Widget>[
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            SectionIcon(Section.homework),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    s.pair(item.titleEn, item.titleAr),
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    s.pair(item.subjectNameEn, item.subjectNameAr),
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: AppColors.muted),
                  ),
                ],
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: <Widget>[
            Pill(
              text: '${s.dueOn} ${Fmt.date(item.dueDate, language)}',
              icon: Icons.event_rounded,
              tone: _urgent ? AppColors.danger : Section.homework.color,
            ),
            // BR-LRN-004: no maximum means ungraded practice. Saying that is
            // the point — a blank mark reads as a mark nobody has entered yet.
            if (item.maxMarks == null)
              Pill(
                text: s.ungraded,
                icon: Icons.edit_note_rounded,
                tone: AppColors.muted,
              )
            else
              Pill(
                text: '${s.outOf} ${Fmt.marks(item.maxMarks, s.lang)}',
                icon: Icons.grade_rounded,
                tone: Section.results.color,
              ),
            if (item.latePenaltyApplies)
              Pill(
                text: item.latePenaltyPercent == null
                    ? s.latePenalty
                    : '${s.latePenalty} ${Fmt.percent(item.latePenaltyPercent, s.lang)}',
                icon: Icons.timer_off_rounded,
                tone: AppColors.warning,
              ),
          ],
        ),
        Prose(
          title: s.instructions,
          icon: Icons.notes_rounded,
          body: s.pair(item.instructionsEn, item.instructionsAr),
        ),
      ],
    );
  }
}
