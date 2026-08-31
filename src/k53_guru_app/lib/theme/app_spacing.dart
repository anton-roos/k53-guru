/// Spacing scale from DESIGN.md's frontmatter (`spacing.scale`), on a 4px
/// base unit. Use these named constants instead of raw numbers so the scale
/// stays a single source of truth across the app.
class AppSpacing {
  const AppSpacing._();

  static const double space4 = 4;
  static const double space8 = 8;
  static const double space12 = 12;
  static const double space16 = 16;
  static const double space20 = 20;
  static const double space24 = 24;
  static const double space32 = 32;
  static const double space48 = 48;

  /// DESIGN.md `spacing.min-tap-target` -- every interactive control must be
  /// at least this tall/wide.
  static const double minTapTarget = 48;

  /// DESIGN.md `components.button-primary.height` / `button-disabled.height`.
  static const double primaryButtonHeight = 56;
}

/// Corner-radius scale from DESIGN.md's frontmatter (`rounded`).
class AppRadius {
  const AppRadius._();

  /// Small rounding.
  static const double sm = 8;

  /// Option cards, buttons.
  static const double md = 14;

  /// Tiles, sheets.
  static const double lg = 20;

  /// Chips, progress bars -- fully pill-shaped.
  static const double pill = 999;
}
