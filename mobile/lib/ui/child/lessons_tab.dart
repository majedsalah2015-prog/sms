import 'dart:io';

import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';
import 'package:provider/provider.dart';

import '../../core/api_client.dart';
import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../widgets/async_view.dart';
import '../widgets/panels.dart';

/// doc/Modules/37 §5 — the published lesson plans for this student's subjects,
/// and the material filed against them (§8.2).
class LessonsTab extends StatelessWidget {
  const LessonsTab({required this.studentId, super.key});

  final int studentId;

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final AuthController auth = context.watch<AuthController>();

    return AsyncView<List<PortalLesson>>(
      load: () => auth.api.lessons(studentId),
      empty: s.noLessons,
      builder: (BuildContext context, List<PortalLesson> lessons) {
        return ListView.separated(
          padding: const EdgeInsets.all(16),
          itemCount: lessons.length,
          separatorBuilder: (_, __) => const SizedBox(height: 12),
          itemBuilder: (BuildContext context, int index) =>
              _LessonCard(lesson: lessons[index]),
        );
      },
    );
  }
}

class _LessonCard extends StatelessWidget {
  const _LessonCard({required this.lesson});

  final PortalLesson lesson;

  /// Fetched with the session token, then handed to the phone.
  ///
  /// It is not simply opened in the browser: the download endpoint is
  /// `[Authorize]`d like every other, a browser sends no `Authorization`
  /// header, and putting the token in the URL to get around that would leak a
  /// live credential into history and the recents list. BR-LRN-006 also
  /// re-applies the scan verdict at this call, so a resource withdrawn since it
  /// was listed refuses here — with the school's own sentence, which is what
  /// gets shown.
  Future<void> _open(
    BuildContext context,
    PortalLessonResource resource,
  ) async {
    final AuthController auth = context.read<AuthController>();
    final ScaffoldMessengerState messenger = ScaffoldMessenger.of(context);
    final Strings s = Strings.of(context);

    try {
      final DownloadedFile file = await auth.api.downloadResource(resource);

      // The cache directory, not documents: the file is the school's copy and
      // is re-fetched next time so the gate above keeps applying.
      final Directory dir = await getTemporaryDirectory();
      final String name = (file.fileName ?? '').trim().isNotEmpty
          ? file.fileName!.replaceAll(RegExp(r'[\\/:*?"<>|]'), '_')
          : 'resource-${resource.resourceId}';
      final File target = File('${dir.path}${Platform.pathSeparator}$name');
      await target.writeAsBytes(file.bytes, flush: true);

      final OpenResult result = await OpenFilex.open(target.path);
      if (result.type != ResultType.done) {
        messenger.showSnackBar(SnackBar(content: Text(s.openFailed)));
      }
    } on Object catch (e) {
      messenger.showSnackBar(
        SnackBar(content: Text(FailureView.messageFor(e, s))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);

    return Panel(
      children: <Widget>[
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    s.pair(lesson.titleEn, lesson.titleAr),
                    style: theme.textTheme.titleMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    s.pair(lesson.subjectNameEn, lesson.subjectNameAr),
                    style: theme.textTheme.bodySmall?.copyWith(
                      color: theme.colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Pill(text: '${s.week} ${lesson.weekNumber}'),
          ],
        ),
        Prose(
          title: s.objectives,
          body: s.pair(lesson.objectivesEn, lesson.objectivesAr),
        ),
        if (lesson.resources.isNotEmpty) ...<Widget>[
          const SizedBox(height: 12),
          Text(
            s.materials,
            style: theme.textTheme.labelMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          for (final PortalLessonResource resource in lesson.resources)
            ListTile(
              contentPadding: EdgeInsets.zero,
              dense: true,
              leading: const Icon(Icons.attach_file),
              title: Text(s.pair(resource.titleEn, resource.titleAr)),
              trailing: TextButton(
                onPressed: () => _open(context, resource),
                child: Text(s.openResource),
              ),
            ),
        ],
      ],
    );
  }
}
