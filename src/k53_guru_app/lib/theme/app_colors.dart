import 'package:flutter/widgets.dart';

/// One full palette of DESIGN.md's colour tokens (`colors.light` /
/// `colors.dark` in the frontmatter). Every named field mirrors a token
/// name exactly so the mapping back to DESIGN.md stays obvious.
@immutable
class AppColorPalette {
  const AppColorPalette({
    required this.primary,
    required this.primaryStrong,
    required this.accent,
    required this.success,
    required this.successSoft,
    required this.danger,
    required this.dangerSoft,
    required this.surface,
    required this.card,
    required this.ink,
    required this.muted,
    required this.line,
  });

  final Color primary;
  final Color primaryStrong;
  final Color accent;
  final Color success;
  final Color successSoft;
  final Color danger;
  final Color dangerSoft;
  final Color surface;
  final Color card;
  final Color ink;
  final Color muted;
  final Color line;
}

/// The two DESIGN.md palettes. Dark mode is a profile setting per
/// DESIGN.md, but at the Flutter theming layer both palettes are exposed
/// as plain `ThemeData`/`ThemeData.dark()` variants (see `app_theme.dart`).
class AppColors {
  const AppColors._();

  static const AppColorPalette light = AppColorPalette(
    primary: Color(0xFF4338CA),
    primaryStrong: Color(0xFF3730A3),
    accent: Color(0xFF0EA5A4),
    success: Color(0xFF16A34A),
    successSoft: Color(0xFFE7F7EC),
    danger: Color(0xFFDC2626),
    dangerSoft: Color(0xFFFDEAEA),
    surface: Color(0xFFF8FAFC),
    card: Color(0xFFFFFFFF),
    ink: Color(0xFF1E293B),
    muted: Color(0xFF64748B),
    line: Color(0xFFE2E8F0),
  );

  static const AppColorPalette dark = AppColorPalette(
    primary: Color(0xFF818CF8),
    primaryStrong: Color(0xFFA5B4FC),
    accent: Color(0xFF2DD4BF),
    success: Color(0xFF4ADE80),
    successSoft: Color(0xFF0F2A1B),
    danger: Color(0xFFF87171),
    dangerSoft: Color(0xFF2A1516),
    surface: Color(0xFF0B1220),
    card: Color(0xFF131C2E),
    ink: Color(0xFFE8EEFC),
    muted: Color(0xFF94A3B8),
    line: Color(0xFF24314B),
  );

  /// DESIGN.md's explicit AA-contrast callout: the primary button keeps
  /// dark ink text (not white) when it sits on the dark-mode primary
  /// colour, because `dark.primary` (`#818cf8`) is too light for white
  /// text to meet AA contrast.
  static const Color darkModePrimaryButtonText = Color(0xFF0B1220);
}
