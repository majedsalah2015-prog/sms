import 'package:flutter/material.dart';

/// The app's visual identity — the school portal's, not a second one.
///
/// The colours here are the ones `wwwroot/css/site.css` already defines for the
/// web portal (`--sms-primary` `#2563eb`, `--sms-body-bg` `#f4f6fb`,
/// `--sms-muted` `#5b6270`, `--sms-sidebar-border` `#e6e9f2`). A parent who
/// opens the site on a laptop and the app on a phone is looking at one product,
/// and the fastest way to make it feel like two is to let the phone pick its own
/// palette.
abstract final class AppColors {
  /// `--sms-primary`.
  static const Color primary = Color(0xFF2563EB);
  static const Color primaryHover = Color(0xFF1D4ED8);

  /// `--sms-body-bg`. Cards are white on this, which is what gives the shell
  /// its depth without a single shadow.
  static const Color background = Color(0xFFF4F6FB);
  static const Color surface = Color(0xFFFFFFFF);

  /// `--sms-muted`. Chosen on the web for 5.6:1 against the background —
  /// `#6b7280` fell to 4.47:1 and failed WCAG 1.4.3. Same reason applies here.
  static const Color muted = Color(0xFF5B6270);
  static const Color border = Color(0xFFE6E9F2);

  static const Color danger = Color(0xFFDC2626);
  static const Color success = Color(0xFF059669);
  static const Color warning = Color(0xFFD97706);
}

/// One area of the portal: its icon and its colour, in one place.
///
/// Every screen takes its accent from here rather than choosing locally, which
/// is what stops "colourful" from becoming "arbitrary" by the sixth screen. The
/// same nine entries are mirrored in the web portal's own stylesheet, so a
/// section is the same colour in both places.
enum Section {
  family(Icons.groups_rounded, Color(0xFF2563EB)),
  attendance(Icons.event_available_rounded, Color(0xFF0EA5E9)),
  results(Icons.workspace_premium_rounded, Color(0xFF7C3AED)),
  fees(Icons.receipt_long_rounded, Color(0xFF059669)),
  timetable(Icons.schedule_rounded, Color(0xFF0D9488)),
  homework(Icons.assignment_turned_in_rounded, Color(0xFFEA580C)),
  lessons(Icons.menu_book_rounded, Color(0xFF4F46E5)),
  announcements(Icons.campaign_rounded, Color(0xFFDB2777)),
  account(Icons.person_rounded, Color(0xFF475569));

  const Section(this.icon, this.color);

  final IconData icon;
  final Color color;

  /// The wash a section's icon sits on. Kept very light so the icon carries the
  /// colour and the text keeps its contrast — a saturated tile behind dark text
  /// is how a "colourful" screen stops being readable.
  Color get wash => Color.alphaBlend(color.withValues(alpha: 0.10), Colors.white);
}

/// A section's icon in its tinted, rounded tile. The one shape every screen
/// repeats, so the reader learns it once.
class SectionIcon extends StatelessWidget {
  const SectionIcon(
    this.section, {
    this.size = 40,
    this.iconSize = 20,
    super.key,
  });

  final Section section;
  final double size;
  final double iconSize;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: size,
      height: size,
      decoration: BoxDecoration(
        color: section.wash,
        borderRadius: BorderRadius.circular(size * 0.3),
      ),
      child: Icon(section.icon, size: iconSize, color: section.color),
    );
  }
}

/// A heading with its section's icon beside it.
class SectionHeader extends StatelessWidget {
  const SectionHeader({
    required this.section,
    required this.title,
    this.subtitle,
    this.trailing,
    super.key,
  });

  final Section section;
  final String title;
  final String? subtitle;
  final Widget? trailing;

  @override
  Widget build(BuildContext context) {
    final ThemeData theme = Theme.of(context);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: <Widget>[
        SectionIcon(section),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: <Widget>[
              Text(
                title,
                style: theme.textTheme.titleMedium?.copyWith(
                  fontWeight: FontWeight.w600,
                ),
              ),
              if (subtitle != null && subtitle!.trim().isNotEmpty)
                Text(
                  subtitle!,
                  style: theme.textTheme.bodySmall?.copyWith(
                    color: AppColors.muted,
                  ),
                ),
            ],
          ),
        ),
        if (trailing != null) trailing!,
      ],
    );
  }
}

/// The theme both brightnesses are built from.
abstract final class AppTheme {
  static ThemeData light() => _build(Brightness.light);

  static ThemeData dark() => _build(Brightness.dark);

  static ThemeData _build(Brightness brightness) {
    final bool isLight = brightness == Brightness.light;
    final ColorScheme scheme = ColorScheme.fromSeed(
      seedColor: AppColors.primary,
      brightness: brightness,
    ).copyWith(
      primary: isLight ? AppColors.primary : null,
      surface: isLight ? AppColors.surface : null,
      error: isLight ? AppColors.danger : null,
    );

    final Color background = isLight ? AppColors.background : scheme.surface;
    final Color outline = isLight ? AppColors.border : scheme.outlineVariant;

    return ThemeData(
      colorScheme: scheme,
      scaffoldBackgroundColor: background,
      // Cairo and Tajawal are not bundled: shipping a font is a licence
      // decision the school makes, and Android's own Arabic face renders the
      // product's text correctly without one.
      appBarTheme: AppBarTheme(
        backgroundColor: isLight ? AppColors.surface : null,
        foregroundColor: isLight ? const Color(0xFF111827) : null,
        surfaceTintColor: Colors.transparent,
        elevation: 0,
        scrolledUnderElevation: 0,
        shape: Border(bottom: BorderSide(color: outline)),
        titleTextStyle: TextStyle(
          fontSize: 17,
          fontWeight: FontWeight.w600,
          color: isLight ? const Color(0xFF111827) : Colors.white,
        ),
      ),
      cardTheme: CardThemeData(
        elevation: 0,
        margin: EdgeInsets.zero,
        color: isLight ? AppColors.surface : null,
        surfaceTintColor: Colors.transparent,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(color: outline),
        ),
      ),
      dividerTheme: DividerThemeData(color: outline, thickness: 1, space: 1),
      tabBarTheme: TabBarThemeData(
        labelColor: AppColors.primary,
        unselectedLabelColor: AppColors.muted,
        indicatorColor: AppColors.primary,
        indicatorSize: TabBarIndicatorSize.label,
        dividerColor: outline,
        labelStyle: const TextStyle(fontWeight: FontWeight.w600, fontSize: 14),
        unselectedLabelStyle: const TextStyle(fontSize: 14),
      ),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: isLight ? AppColors.surface : null,
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: outline),
        ),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: BorderSide(color: outline),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: AppColors.primary, width: 2),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          minimumSize: const Size.fromHeight(50),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          textStyle: const TextStyle(fontSize: 16, fontWeight: FontWeight.w600),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
          side: BorderSide(color: outline),
        ),
      ),
      snackBarTheme: SnackBarThemeData(
        behavior: SnackBarBehavior.floating,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(12),
        ),
      ),
      listTileTheme: const ListTileThemeData(
        iconColor: AppColors.muted,
      ),
    );
  }
}
