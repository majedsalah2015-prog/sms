import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../format.dart';
import '../theme.dart';
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
      emptySection: Section.results,
      builder: (BuildContext context, List<PortalResult> results) {
        // Grouped by term, because a parent opens this looking for the term
        // that just closed.
        final Map<String, List<PortalResult>> byTerm =
            <String, List<PortalResult>>{};
        for (final PortalResult r in results) {
          byTerm.putIfAbsent(r.termName ?? '', () => <PortalResult>[]).add(r);
        }

        return ListView(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          children: <Widget>[
            for (final MapEntry<String, List<PortalResult>> term
                in byTerm.entries) ...<Widget>[
              Panel(
                section: Section.results,
                title: term.key.isEmpty ? s.results : term.key,
                subtitle: '${term.value.length} · ${s.results}',
                children: <Widget>[
                  for (int i = 0; i < term.value.length; i++) ...<Widget>[
                    if (i > 0) const Divider(height: 20),
                    _ResultRow(result: term.value[i]),
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

class _ResultRow extends StatelessWidget {
  const _ResultRow({required this.result});

  final PortalResult result;

  /// Green / amber / red by band. The colour is a reading aid, never a verdict
  /// the school did not give: the band code beside it is the school's own word,
  /// and this only tints it.
  Color get _tone {
    if (result.scorePercent >= 80) return AppColors.success;
    if (result.scorePercent >= 50) return AppColors.warning;
    return AppColors.danger;
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);

    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                s.pair(result.subjectNameEn, result.subjectNameAr),
                style: theme.textTheme.bodyLarge
                    ?.copyWith(fontWeight: FontWeight.w500),
              ),
              const SizedBox(height: 3),
              Row(
                children: <Widget>[
                  const Icon(Icons.event_rounded,
                      size: 13, color: AppColors.muted),
                  const SizedBox(width: 4),
                  Text(
                    Fmt.date(
                        result.publishedAtUtc, s.lang),
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: AppColors.muted),
                  ),
                ],
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
                Fmt.percent(result.scorePercent, s.lang),
                style: theme.textTheme.titleMedium?.copyWith(
                  color: _tone,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            if ((result.bandCode ?? '').isNotEmpty) ...<Widget>[
              const SizedBox(height: 4),
              // The band is the school's own code (A, ممتاز, 5) and is shown as
              // stored — translating a grading scale is the school's decision,
              // not this app's.
              Pill(text: result.bandCode!, tone: _tone),
            ],
          ],
        ),
      ],
    );
  }
}
