import 'dart:io';

import 'package:flutter/material.dart';
import 'package:open_filex/open_filex.dart';
import 'package:path_provider/path_provider.dart';
import 'package:provider/provider.dart';

import '../../core/api_client.dart';
import '../../l10n/strings.dart';
import '../../models/portal.dart';
import '../../state/auth_controller.dart';
import '../theme.dart';
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
      emptySection: Section.lessons,
      builder: (BuildContext context, List<PortalLesson> lessons) {
        return ListView.separated(
          padding: const EdgeInsets.fromLTRB(16, 16, 16, 24),
          itemCount: lessons.length,
          separatorBuilder: (_, __) => const SizedBox(height: 12),
          itemBuilder: (BuildContext context, int index) =>
              _LessonCard(lesson: lessons[index]),
        );
      },
    );
  }
}

class _LessonCard extends StatefulWidget {
  const _LessonCard({required this.lesson});

  final PortalLesson lesson;

  @override
  State<_LessonCard> createState() => _LessonCardState();
}

class _LessonCardState extends State<_LessonCard> {
  int? _busyResourceId;

  /// Fetched with the session token, then handed to the phone.
  ///
  /// It is not simply opened in the browser: the download endpoint is
  /// `[Authorize]`d like every other, a browser sends no `Authorization`
  /// header, and putting the token in the URL to get around that would leak a
  /// live credential into history and the recents list. BR-LRN-006 also
  /// re-applies the scan verdict at this call, so a resource withdrawn since it
  /// was listed refuses here — with the school's own sentence, which is what
  /// gets shown.
  Future<void> _open(PortalLessonResource resource) async {
    final AuthController auth = context.read<AuthController>();
    final ScaffoldMessengerState messenger = ScaffoldMessenger.of(context);
    final Strings s = Strings.of(context);

    setState(() => _busyResourceId = resource.resourceId);
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
    } finally {
      if (mounted) setState(() => _busyResourceId = null);
    }
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final ThemeData theme = Theme.of(context);
    final PortalLesson lesson = widget.lesson;

    return Panel(
      children: <Widget>[
        Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            SectionIcon(Section.lessons),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    s.pair(lesson.titleEn, lesson.titleAr),
                    style: theme.textTheme.titleMedium
                        ?.copyWith(fontWeight: FontWeight.w600),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    s.pair(lesson.subjectNameEn, lesson.subjectNameAr),
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: AppColors.muted),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 8),
            Pill(
              text: '${s.week} ${lesson.weekNumber}',
              tone: Section.lessons.color,
            ),
          ],
        ),
        Prose(
          title: s.objectives,
          icon: Icons.flag_rounded,
          body: s.pair(lesson.objectivesEn, lesson.objectivesAr),
        ),
        if (lesson.resources.isNotEmpty) ...<Widget>[
          const SizedBox(height: 14),
          Row(
            children: <Widget>[
              const Icon(Icons.folder_rounded,
                  size: 15, color: AppColors.muted),
              const SizedBox(width: 6),
              Text(
                s.materials,
                style: theme.textTheme.labelMedium
                    ?.copyWith(color: AppColors.muted),
              ),
            ],
          ),
          const SizedBox(height: 6),
          for (final PortalLessonResource resource in lesson.resources)
            _ResourceRow(
              title: s.pair(resource.titleEn, resource.titleAr),
              busy: _busyResourceId == resource.resourceId,
              label: s.openResource,
              onOpen: _busyResourceId == null ? () => _open(resource) : null,
            ),
        ],
      ],
    );
  }
}

class _ResourceRow extends StatelessWidget {
  const _ResourceRow({
    required this.title,
    required this.busy,
    required this.label,
    required this.onOpen,
  });

  final String title;
  final bool busy;
  final String label;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 3),
      child: Row(
        children: <Widget>[
          Container(
            width: 32,
            height: 32,
            decoration: BoxDecoration(
              color: Section.lessons.wash,
              borderRadius: BorderRadius.circular(9),
            ),
            child: Icon(Icons.description_rounded,
                size: 16, color: Section.lessons.color),
          ),
          const SizedBox(width: 10),
          Expanded(
            child: Text(title, style: Theme.of(context).textTheme.bodyMedium),
          ),
          if (busy)
            const Padding(
              padding: EdgeInsets.symmetric(horizontal: 14),
              child: SizedBox(
                width: 16,
                height: 16,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            )
          else
            TextButton.icon(
              onPressed: onOpen,
              icon: const Icon(Icons.download_rounded, size: 16),
              label: Text(label),
            ),
        ],
      ),
    );
  }
}
