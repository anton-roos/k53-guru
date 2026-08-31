import 'package:flutter/material.dart';

import 'app_colors.dart';

/// Exposes the full DESIGN.md colour palette (beyond the handful of slots
/// Material's [ColorScheme] has room for -- accent/success/danger/muted/
/// line etc.) through `Theme.of(context)`, so both palettes are reachable
/// as part of the shared theme rather than as a parallel global.
@immutable
class AppColorsExtension extends ThemeExtension<AppColorsExtension> {
  const AppColorsExtension(this.colors);

  final AppColorPalette colors;

  @override
  AppColorsExtension copyWith({AppColorPalette? colors}) {
    return AppColorsExtension(colors ?? this.colors);
  }

  @override
  AppColorsExtension lerp(ThemeExtension<AppColorsExtension>? other, double t) {
    if (other is! AppColorsExtension) return this;
    // The two palettes aren't designed to be interpolated colour-by-colour
    // (dark mode is a discrete profile setting, not an animated transition
    // per DESIGN.md), so snap at the midpoint instead of blending.
    return t < 0.5 ? this : other;
  }
}

extension AppColorsBuildContext on BuildContext {
  /// The active DESIGN.md palette for the current theme brightness.
  AppColorPalette get appColors =>
      Theme.of(this).extension<AppColorsExtension>()?.colors ?? AppColors.light;
}
