import 'package:flutter/material.dart';

import '../theme.dart';

/// The small pieces every detail screen is built from. Kept together so a
/// change to the card's shape is one edit, not eleven.

/// A card, optionally introduced by its section's icon and title.
class Panel extends StatelessWidget {
  const Panel({
    required this.children,
    this.title,
    this.section,
    this.subtitle,
    this.trailing,
    super.key,
  });

  final String? title;

  /// Gives the card's heading its icon and colour. Omitted for a card that is
  /// a continuation of the one above it rather than a new subject.
  final Section? section;

  final String? subtitle;
  final Widget? trailing;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final String? heading = title;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: <Widget>[
            if (heading != null) ...<Widget>[
              if (section != null)
                SectionHeader(
                  section: section!,
                  title: heading,
                  subtitle: subtitle,
                  trailing: trailing,
                )
              else
                Row(
                  children: <Widget>[
                    Expanded(
                      child: Text(
                        heading,
                        style: theme.textTheme.titleSmall
                            ?.copyWith(fontWeight: FontWeight.w600),
                      ),
                    ),
                    if (trailing != null) trailing!,
                  ],
                ),
              const SizedBox(height: 14),
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
    this.icon,
    this.iconColor,
    this.numeric = false,
    this.emphasis = false,
    super.key,
  });

  final String label;
  final String value;

  /// A small leading glyph. Used where a row is one of several similar figures
  /// and the icon is what tells them apart at a glance.
  final IconData? icon;
  final Color? iconColor;

  /// Renders the value LTR. Set it for money, percentages, marks and times.
  final bool numeric;

  final bool emphasis;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Widget text = Text(
      value,
      textAlign: TextAlign.end,
      style: (emphasis ? theme.textTheme.titleMedium : theme.textTheme.bodyLarge)
          ?.copyWith(
        color: emphasis ? (iconColor ?? AppColors.primary) : null,
        fontWeight: emphasis ? FontWeight.w700 : FontWeight.w500,
      ),
    );

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 7),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: <Widget>[
          if (icon != null) ...<Widget>[
            Icon(icon, size: 17, color: iconColor ?? AppColors.muted),
            const SizedBox(width: 8),
          ],
          Expanded(
            child: Text(
              label,
              style: theme.textTheme.bodyMedium?.copyWith(
                color: AppColors.muted,
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
  const Prose({required this.title, required this.body, this.icon, super.key});

  final String title;
  final String body;
  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    if (body.trim().isEmpty) return const SizedBox.shrink();
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        const SizedBox(height: 10),
        if (title.trim().isNotEmpty) ...<Widget>[
          Row(
            children: <Widget>[
              if (icon != null) ...<Widget>[
                Icon(icon, size: 15, color: AppColors.muted),
                const SizedBox(width: 6),
              ],
              Text(
                title,
                style: theme.textTheme.labelMedium
                    ?.copyWith(color: AppColors.muted),
              ),
            ],
          ),
          const SizedBox(height: 4),
        ],
        Text(
          body,
          style: theme.textTheme.bodyMedium?.copyWith(height: 1.5),
        ),
      ],
    );
  }
}

/// A small status word — a grade band, a timetable overlay, "not graded".
class Pill extends StatelessWidget {
  const Pill({required this.text, this.tone, this.icon, super.key});

  final String text;

  /// The colour the pill is *about*; it is tinted, never filled solid, so the
  /// label keeps its contrast.
  final Color? tone;

  final IconData? icon;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    final Color color = tone ?? AppColors.primary;
    if (text.trim().isEmpty) return const SizedBox.shrink();

    return Container(
      padding: EdgeInsets.only(
        left: icon == null ? 10 : 8,
        right: 10,
        top: 5,
        bottom: 5,
      ),
      decoration: BoxDecoration(
        color: Color.alphaBlend(color.withValues(alpha: 0.10), Colors.white),
        borderRadius: BorderRadius.circular(999),
        border: Border.all(
          color: Color.alphaBlend(color.withValues(alpha: 0.22), Colors.white),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: <Widget>[
          if (icon != null) ...<Widget>[
            Icon(icon, size: 13, color: color),
            const SizedBox(width: 5),
          ],
          Text(
            text,
            style: theme.textTheme.labelSmall?.copyWith(
              color: color,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

/// One big number with its icon — the figure a screen exists to show.
class BigStat extends StatelessWidget {
  const BigStat({
    required this.section,
    required this.label,
    required this.value,
    this.progress,
    super.key,
  });

  final Section section;
  final String label;
  final String value;

  /// 0..1. Drawn under the figure when the number is a proportion.
  final double? progress;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: <Widget>[
        Row(
          children: <Widget>[
            SectionIcon(section, size: 44, iconSize: 22),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Text(
                    label,
                    style: theme.textTheme.bodySmall
                        ?.copyWith(color: AppColors.muted),
                  ),
                  Directionality(
                    textDirection: TextDirection.ltr,
                    child: Align(
                      alignment: AlignmentDirectional.centerStart,
                      child: Text(
                        value,
                        style: theme.textTheme.headlineSmall?.copyWith(
                          fontWeight: FontWeight.w700,
                          color: section.color,
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        if (progress != null) ...<Widget>[
          const SizedBox(height: 14),
          ClipRRect(
            borderRadius: BorderRadius.circular(999),
            child: LinearProgressIndicator(
              value: progress!.clamp(0, 1).toDouble(),
              minHeight: 8,
              backgroundColor: section.wash,
              valueColor: AlwaysStoppedAnimation<Color>(section.color),
            ),
          ),
        ],
      ],
    );
  }
}
