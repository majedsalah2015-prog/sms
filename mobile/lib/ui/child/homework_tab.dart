import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
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
      builder: (BuildContext context, List<PortalHomework> items) {
        return ListView.separated(
          padding: const EdgeInsets.all(16),
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

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final String language = s.isArabic ? 'ar' : 'en';

    return Panel(
      children: <Widget>[
        Text(
          s.pair(item.titleEn, item.titleAr),
          style: theme.textTheme.titleMedium,
        ),
        const SizedBox(height: 4),
        Text(
          s.pair(item.subjectNameEn, item.subjectNameAr),
          style: theme.textTheme.bodySmall?.copyWith(
            color: theme.colorScheme.onSurfaceVariant,
          ),
        ),
        const SizedBox(height: 10),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: <Widget>[
            Pill(text: '${s.dueOn} ${Fmt.date(item.dueDate, language)}'),
            // BR-LRN-004: no maximum means ungraded practice. Saying that is
            // the point — a blank mark reads as a mark nobody has entered yet.
            if (item.maxMarks == null)
              Pill(text: s.ungraded)
            else
              Pill(text: '${s.outOf} ${Fmt.marks(item.maxMarks)}'),
            if (item.latePenaltyApplies)
              Pill(
                text: item.latePenaltyPercent == null
                    ? s.latePenalty
                    : '${s.latePenalty} ${Fmt.percent(item.latePenaltyPercent)}',
                tone: theme.colorScheme.errorContainer,
              ),
          ],
        ),
        Prose(
          title: s.instructions,
          body: s.pair(item.instructionsEn, item.instructionsAr),
        ),
      ],
    );
  }
}
