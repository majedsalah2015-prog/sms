import 'dart:async';

import 'package:flutter/material.dart';
import 'package:provider/provider.dart';

import '../l10n/strings.dart';
import '../models/json.dart';
import '../models/portal.dart';
import '../state/auth_controller.dart';
import 'format.dart';
import 'widgets/async_view.dart';
import 'widgets/panels.dart';

/// BR-SEC-012: only a *sent* announcement ever reaches a family, so a draft the
/// school is still writing cannot appear here.
///
/// This is the one paged endpoint the portal has (§4), and it is paged for a
/// reason — a school with three years of notices behind it is not a list to
/// hand a phone whole.
class AnnouncementsPage extends StatefulWidget {
  const AnnouncementsPage({super.key});

  @override
  State<AnnouncementsPage> createState() => _AnnouncementsPageState();
}

class _AnnouncementsPageState extends State<AnnouncementsPage> {
  final List<PortalAnnouncement> _items = <PortalAnnouncement>[];

  int _page = 0;
  bool _hasMore = true;
  bool _busy = false;
  Object? _error;

  @override
  void initState() {
    super.initState();
    unawaited(_loadNext());
  }

  Future<void> _loadNext() async {
    if (_busy || !_hasMore) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final Paged<PortalAnnouncement> next =
          await context.read<AuthController>().api.announcements(
                page: _page + 1,
              );
      if (!mounted) return;
      setState(() {
        _items.addAll(next.items);
        _page = next.page;
        _hasMore = next.hasMore;
      });
    } on Object catch (e) {
      if (!mounted) return;
      setState(() => _error = e);
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _refresh() async {
    setState(() {
      _items.clear();
      _page = 0;
      _hasMore = true;
    });
    await _loadNext();
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    final String language = s.isArabic ? 'ar' : 'en';

    return Scaffold(
      appBar: AppBar(title: Text(s.announcements)),
      body: RefreshIndicator(
        onRefresh: _refresh,
        child: Builder(
          builder: (BuildContext context) {
            if (_items.isEmpty && _busy) {
              return const Center(child: CircularProgressIndicator());
            }
            if (_items.isEmpty && _error != null) {
              return ListView(
                padding: const EdgeInsets.all(24),
                children: <Widget>[
                  FailureView(error: _error!, onRetry: _refresh),
                ],
              );
            }
            if (_items.isEmpty) {
              return ListView(
                padding: const EdgeInsets.all(24),
                children: <Widget>[EmptyView(message: s.noAnnouncements)],
              );
            }

            return ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: _items.length + (_hasMore || _error != null ? 1 : 0),
              separatorBuilder: (_, __) => const SizedBox(height: 12),
              itemBuilder: (BuildContext context, int index) {
                if (index == _items.length) {
                  // A failure part-way through a list must not throw away the
                  // pages already read; it becomes a retry at the bottom.
                  if (_error != null) {
                    return FailureView(error: _error!, onRetry: _loadNext);
                  }
                  return Center(
                    child: _busy
                        ? const Padding(
                            padding: EdgeInsets.all(16),
                            child: CircularProgressIndicator(),
                          )
                        : OutlinedButton(
                            onPressed: _loadNext,
                            child: Text(s.loadMore),
                          ),
                  );
                }

                final PortalAnnouncement a = _items[index];
                return Panel(
                  children: <Widget>[
                    Text(
                      s.pair(a.titleEn, a.titleAr),
                      style: Theme.of(context).textTheme.titleMedium,
                    ),
                    const SizedBox(height: 4),
                    Text(
                      Fmt.date(a.sentAtUtc, language),
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color:
                                Theme.of(context).colorScheme.onSurfaceVariant,
                          ),
                    ),
                    Prose(title: '', body: s.pair(a.bodyEn, a.bodyAr)),
                  ],
                );
              },
            );
          },
        ),
      ),
    );
  }
}
