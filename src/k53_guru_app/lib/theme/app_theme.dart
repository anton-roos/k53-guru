import 'package:flutter/material.dart';

import 'app_colors.dart';
import 'app_colors_extension.dart';
import 'app_spacing.dart';
import 'app_typography.dart';

/// Builds the app's light/dark `ThemeData` from the DESIGN.md design
/// tokens. This is the only place `ThemeData`/`ThemeData.dark()` get
/// constructed -- widgets should read tokens via `Theme.of(context)`
/// (`.textTheme`, `.extension<AppColorsExtension>()`, or the `appColors`
/// `BuildContext` extension) rather than reaching into `AppColors`
/// directly.
class AppTheme {
  const AppTheme._();

  static ThemeData light() => _build(AppColors.light, Brightness.light);

  static ThemeData dark() => _build(AppColors.dark, Brightness.dark);

  static ThemeData _build(AppColorPalette palette, Brightness brightness) {
    final bool isDark = brightness == Brightness.dark;

    // DESIGN.md's explicit AA-contrast callout: white text on light-mode
    // primary, dark ink (#0b1220) on dark-mode primary.
    final Color onPrimary =
        isDark ? AppColors.darkModePrimaryButtonText : Colors.white;

    final ColorScheme colorScheme = ColorScheme(
      brightness: brightness,
      primary: palette.primary,
      onPrimary: onPrimary,
      secondary: palette.accent,
      onSecondary: Colors.white,
      error: palette.danger,
      onError: Colors.white,
      surface: palette.surface,
      onSurface: palette.ink,
    );

    return ThemeData(
      useMaterial3: true,
      brightness: brightness,
      colorScheme: colorScheme,
      scaffoldBackgroundColor: palette.surface,
      cardColor: palette.card,
      dividerColor: palette.line,
      textTheme: AppTypography.textTheme(palette.ink),
      appBarTheme: AppBarTheme(
        backgroundColor: palette.surface,
        foregroundColor: palette.ink,
        elevation: 0,
        titleTextStyle: AppTypography.h2.copyWith(color: palette.ink),
      ),
      cardTheme: CardThemeData(
        color: palette.card,
        elevation: 0,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(AppRadius.md),
          side: BorderSide(color: palette.line),
        ),
      ),
      // component token: button-primary (DESIGN.md `components.button-primary`)
      // and button-disabled (`components.button-disabled`).
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: palette.primary,
          foregroundColor: onPrimary,
          disabledBackgroundColor: palette.line,
          disabledForegroundColor: palette.muted,
          minimumSize: const Size.fromHeight(AppSpacing.primaryButtonHeight),
          textStyle: AppTypography.option.copyWith(fontWeight: FontWeight.w800),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(AppRadius.md),
          ),
        ),
      ),
      // Accessibility floor (Story 4.7): `elevatedButtonTheme` above already
      // themes a `minimumSize`, but `OutlinedButton`/`TextButton` had no
      // theme at all -- Material's own un-themed defaults for those two
      // variants are smaller than `AppSpacing.minTapTarget` (48px). Themed
      // here once so every current call site (`Copy UUID`'s
      // `OutlinedButton.icon`, `Restore profile`'s `TextButton`, the
      // `Recalibrate`/`Start fresh` dialog `TextButton`s) and any future one
      // meets the floor without a per-call-site override. Width (64) is not
      // part of the accessibility floor -- it's simply Material's own
      // convention for a comfortable minimum button width; height (48) is
      // what the floor actually requires.
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          minimumSize: const Size(64, AppSpacing.minTapTarget),
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(
          minimumSize: const Size(64, AppSpacing.minTapTarget),
        ),
      ),
      extensions: <ThemeExtension<dynamic>>[
        AppColorsExtension(palette),
      ],
    );
  }
}
