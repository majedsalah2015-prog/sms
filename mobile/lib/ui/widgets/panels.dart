import 'package:flutter/material.dart';

/// The small pieces every detail screen is built from. Kept together so a
/// change to the card's shape is one edit, not eleven.

/// A titled card.
class Panel extends StatelessWidget {
  const Panel({required this.children, this.title, super.key});

  final String? title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            if (title != null) ...<Widget>[
              Text(title!, style: theme.textTheme.titleSmall),
              const SizedBox(height: 12),
            ],
            ...children,
          ],
        ),
      ),
    );
  }
}

/// Label on one side, value on the other — and the value stays left-to-right
/// when it is a number, because this product does not switch numeral systems
/// for money or for a mark.
class Fact extends StatelessWidget {
  const Fact({
    required this.label,
    required this.value,
    this.numeric = false,
    this.emphasis = false,
    super.key,
  });

  final String label;
  final String value;

  /// Renders the value LTR. Set it for money, percentages, marks and times.
  final bool numeric;

  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Widget text = Text(
      value,
      textAlign: TextAlign.end,
      style: (emphasis
              ? theme.textTheme.titleMedium
              : theme.textTheme.bodyLarge)
          ?.copyWith(color: emphasis ? theme.colorScheme.primary : null),
    );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 6),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: <Widget>[
          Expanded(
            child: Text(
              label,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: theme.colorScheme.onSurfaceVariant,
              ),
            ),
          ),
          const SizedBox(width: 12),
          numeric
              ? Directionality(textDirection: TextDirection.ltr, child: text)
              : Flexible(child: text),
        ],
      ),
    );
  }
}

/// A short caption above a block of body text — objectives, instructions, the
/// body of an announcement.
class Prose extends StatelessWidget {
  const Prose({required this.title, required this.body, super.key});

  final String title;
  final String body;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    if (body.trim().isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const SizedBox(height: 8),
        if (title.trim().isNotEmpty) ...<Widget>[
          Text(
            title,
            style: theme.textTheme.labelMedium?.copyWith(
              color: theme.colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 2),
        ],
        Text(body, style: theme.textTheme.bodyMedium),
      ],
    );
  }
}

/// A small status word — a grade band, a timetable overlay, "not graded".
class Pill extends StatelessWidget {
  const Pill({required this.text, this.tone, super.key});

  final String text;
  final Color? tone;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color background = tone ?? theme.colorScheme.secondaryContainer;
    if (text.trim().isEmpty) return const SizedBox.shrink();
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        text,
        style: theme.textTheme.labelSmall?.copyWith(
          color: ThemeData.estimateBrightnessForColor(background) ==
                  Brightness.dark
              ? Colors.white
              : Colors.black87,
        ),
      ),
    );
  }
}
