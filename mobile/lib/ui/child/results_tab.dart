import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// Published term results only. BR-SEC-012: a draft marksheet does not exist
/// out here, so an empty list means "nothing has been approved yet" rather than
/// "nothing has been marked".
class ResultsTab extends StatelessWidget {
  const ResultsTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<List<PortalResult>>(
      load: () => auth.api.results(studentId),
      empty: s.noResults,
      builder: (BuildContext context, List<PortalResult> results) {
        // Grouped by term, newest first, because a parent opens this looking
        // for the term that just closed.
        final Map<String, List<PortalResult>> byTerm =
            <String, List<PortalResult>>{};
        for (final PortalResult r in results) {
          byTerm.putIfAbsent(r.termName ?? '', () => <PortalResult>[]).add(r);
        }

        return ListView(
          padding: const EdgeInsets.all(16),
          children: <Widget>[
            for (final MapEntry<String, List<PortalResult>> term
                in byTerm.entries) ...<Widget>[
              Panel(
                title: term.key.isEmpty ? s.results : term.key,
                children: <Widget>[
                  for (final PortalResult r in term.value)
                    _ResultRow(result: r),
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

class _ResultRow extends StatelessWidget {
  const _ResultRow({required this.result});

  final PortalResult result;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: <Widget>[
                Text(
                  s.pair(result.subjectNameEn, result.subjectNameAr),
                  style: theme.textTheme.bodyLarge,
                ),
                const SizedBox(height: 4),
                Text(
                  '${s.publishedOn}: '
                  '${Fmt.date(result.publishedAtUtc, s.isArabic ? 'ar' : 'en')}',
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: theme.colorScheme.onSurfaceVariant,
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(width: 12),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: <Widget>[
              Directionality(
                textDirection: TextDirection.ltr,
                child: Text(
                  Fmt.percent(result.scorePercent),
                  style: theme.textTheme.titleMedium,
                ),
              ),
              if ((result.bandCode ?? '').isNotEmpty) ...<Widget>[
                const SizedBox(height: 4),
                // The band is the school's own code (A, ممتاز, 5) and is shown
                // as stored — translating a grading scale is the school's
                // decision, not this app's.
                Pill(text: result.bandCode!),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
