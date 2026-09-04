import 'package:flutter/material.dart';

import '../../core/api_exception.dart';
import '../../l10n/strings.dart';

/// One place that turns "loading / failed / empty / here it is" into a screen.
///
/// The failure half matters more than it looks. The API's refusals are already
/// in the caller's language (docs/Integration/03-Mobile-API.md §3), so this
/// widget *shows the server's message* and supplies its own words only for the
/// two cases where there is no server message to show: the network never
/// arrived, and the answer was not something this build understands.
class AsyncView<T> extends StatefulWidget {
  const AsyncView({
    required this.load,
    required this.builder,
    this.empty,
    super.key,
  });

  final Future<T> Function() load;
  final Widget Function(BuildContext context, T value) builder;

  /// Shown instead of [builder] when the loaded value is an empty collection.
  final String? empty;

  @override
  State<AsyncView<T>> createState() => AsyncViewState<T>();
}

class AsyncViewState<T> extends State<AsyncView<T>> {
  late Future<T> _future;

  @override
  void initState() {
    super.initState();
    _future = widget.load();
  }

  /// Re-runs the load. Also what pull-to-refresh calls, which is why the
  /// failure is swallowed here: `FutureBuilder` below is what shows it, and an
  /// error escaping the refresh callback would crash the gesture instead.
  Future<void> reload() async {
    final Future<T> next = widget.load();
    setState(() => _future = next);
    try {
      await next;
    } on Object {
      // Rendered by the builder, not thrown at the RefreshIndicator.
    }
  }

  @override
  Widget build(BuildContext context) {
    return FutureBuilder<T>(
      future: _future,
      builder: (BuildContext context, AsyncSnapshot<T> snapshot) {
        if (snapshot.connectionState == ConnectionState.waiting) {
          return const Center(child: CircularProgressIndicator());
        }

        final Object? error = snapshot.error;
        if (error != null) {
          return RefreshIndicator(
            onRefresh: reload,
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: <Widget>[
                FailureView(error: error, onRetry: reload),
              ],
            ),
          );
        }

        final T? value = snapshot.data;
        final String? emptyMessage = widget.empty;
        if (value is Iterable && value.isEmpty && emptyMessage != null) {
          return RefreshIndicator(
            onRefresh: reload,
            child: ListView(
              padding: const EdgeInsets.all(24),
              children: <Widget>[EmptyView(message: emptyMessage)],
            ),
          );
        }

        return RefreshIndicator(
          onRefresh: reload,
          child: widget.builder(context, value as T),
        );
      },
    );
  }
}

/// A failure, said in the caller's language.
class FailureView extends StatelessWidget {
  const FailureView({required this.error, this.onRetry, super.key});

  final Object error;
  final Future<void> Function()? onRetry;

  /// The server's own sentence wherever there is one; the app's words only
  /// where there is not.
  static String messageFor(Object error, Strings s) {
    if (error is ApiException) {
      return error.message.trim().isNotEmpty ? error.message : s.unexpected;
    }
    if (error is ApiUnreachableException) return s.offline;
    return s.unexpected;
  }

  @override
  Widget build(BuildContext context) {
    final Strings s = Strings.of(context);
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const SizedBox(height: 48),
        Icon(
          error is ApiUnreachableException
              ? Icons.cloud_off_outlined
              : Icons.error_outline,
          size: 40,
          color: Theme.of(context).colorScheme.outline,
        ),
        const SizedBox(height: 12),
        Text(
          messageFor(error, s),
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyLarge,
        ),
        if (onRetry != null) ...<Widget>[
          const SizedBox(height: 16),
          Center(
            child: OutlinedButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(s.retry),
            ),
          ),
        ],
      ],
    );
  }
}

class EmptyView extends StatelessWidget {
  const EmptyView({required this.message, super.key});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: <Widget>[
        const SizedBox(height: 64),
        Icon(
          Icons.inbox_outlined,
          size: 40,
          color: Theme.of(context).colorScheme.outline,
        ),
        const SizedBox(height: 12),
        Text(
          message,
          textAlign: TextAlign.center,
          style: Theme.of(context).textTheme.bodyMedium,
        ),
      ],
    );
  }
}
