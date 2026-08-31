import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'package:k53_guru_app/theme/app_colors.dart';
import 'package:k53_guru_app/theme/app_colors_extension.dart';
import 'package:k53_guru_app/theme/app_spacing.dart';
import 'package:k53_guru_app/theme/app_theme.dart';
import 'package:k53_guru_app/theme/app_typography.dart';

void main() {
  group('AppTypography', () {
    test('body is exactly 17px/400, the accessibility-mandated minimum', () {
      expect(AppTypography.body.fontSize, 17);
      expect(AppTypography.body.fontWeight, FontWeight.w400);
      expect(AppTypography.body.height, 1.5);
    });

    test('option matches body size at 17px but with 600 weight', () {
      expect(AppTypography.option.fontSize, 17);
      expect(AppTypography.option.fontWeight, FontWeight.w600);
      expect(AppTypography.option.height, 1.4);
    });

    test('question renders at 20px/700', () {
      expect(AppTypography.question.fontSize, 20);
      expect(AppTypography.question.fontWeight, FontWeight.w700);
      expect(AppTypography.question.height, 1.4);
    });

    test('display renders at 28px/800', () {
      expect(AppTypography.display.fontSize, 28);
      expect(AppTypography.display.fontWeight, FontWeight.w800);
      expect(AppTypography.display.height, 1.2);
    });

    test('h2 renders at 22px/800', () {
      expect(AppTypography.h2.fontSize, 22);
      expect(AppTypography.h2.fontWeight, FontWeight.w800);
      expect(AppTypography.h2.height, 1.25);
    });

    test('label renders at 13px/700', () {
      expect(AppTypography.label.fontSize, 13);
      expect(AppTypography.label.fontWeight, FontWeight.w700);
      expect(AppTypography.label.height, 1.3);
    });
  });

  group('AppSpacing', () {
    test('exposes the full 4px-based scale', () {
      expect(AppSpacing.space4, 4);
      expect(AppSpacing.space8, 8);
      expect(AppSpacing.space12, 12);
      expect(AppSpacing.space16, 16);
      expect(AppSpacing.space20, 20);
      expect(AppSpacing.space24, 24);
      expect(AppSpacing.space32, 32);
      expect(AppSpacing.space48, 48);
    });

    test('minimum tap target is 48px', () {
      expect(AppSpacing.minTapTarget, 48);
    });
  });

  group('AppRadius', () {
    test('exposes the sm/md/lg/pill scale', () {
      expect(AppRadius.sm, 8);
      expect(AppRadius.md, 14);
      expect(AppRadius.lg, 20);
      expect(AppRadius.pill, 999);
    });
  });

  group('AppColors', () {
    test('light and dark palettes carry every DESIGN.md token', () {
      const AppColorPalette light = AppColors.light;
      expect(light.primary, const Color(0xFF4338CA));
      expect(light.primaryStrong, const Color(0xFF3730A3));
      expect(light.accent, const Color(0xFF0EA5A4));
      expect(light.success, const Color(0xFF16A34A));
      expect(light.successSoft, const Color(0xFFE7F7EC));
      expect(light.danger, const Color(0xFFDC2626));
      expect(light.dangerSoft, const Color(0xFFFDEAEA));
      expect(light.surface, const Color(0xFFF8FAFC));
      expect(light.card, const Color(0xFFFFFFFF));
      expect(light.ink, const Color(0xFF1E293B));
      expect(light.muted, const Color(0xFF64748B));
      expect(light.line, const Color(0xFFE2E8F0));

      const AppColorPalette dark = AppColors.dark;
      expect(dark.primary, const Color(0xFF818CF8));
      expect(dark.surface, const Color(0xFF0B1220));
      expect(dark.ink, const Color(0xFFE8EEFC));
    });
  });

  group('AppTheme', () {
    test('light() and dark() expose ThemeData with the correct brightness', () {
      final ThemeData light = AppTheme.light();
      final ThemeData dark = AppTheme.dark();

      expect(light.brightness, Brightness.light);
      expect(dark.brightness, Brightness.dark);
    });

    test('primary button is 56px tall with md radius and 800 weight text', () {
      final ButtonStyle? style = AppTheme.light().elevatedButtonTheme.style;
      expect(style, isNotNull);

      final Size? minimumSize = style!.minimumSize?.resolve(<WidgetState>{});
      expect(minimumSize?.height, AppSpacing.primaryButtonHeight);
      expect(minimumSize?.height, 56);

      final OutlinedBorder? shape = style.shape?.resolve(<WidgetState>{});
      expect(shape, isA<RoundedRectangleBorder>());
      final RoundedRectangleBorder roundedShape = shape! as RoundedRectangleBorder;
      expect(
        (roundedShape.borderRadius as BorderRadius).topLeft.x,
        AppRadius.md,
      );

      final TextStyle? textStyle = style.textStyle?.resolve(<WidgetState>{});
      expect(textStyle?.fontWeight, FontWeight.w800);
    });

    test('light-mode primary button text is white', () {
      final ButtonStyle style = AppTheme.light().elevatedButtonTheme.style!;
      final Color? foreground = style.foregroundColor?.resolve(<WidgetState>{});
      expect(foreground, Colors.white);
    });

    test(
        'dark-mode primary button text is dark ink (#0b1220), not white, '
        'for AA contrast per DESIGN.md', () {
      final ButtonStyle style = AppTheme.dark().elevatedButtonTheme.style!;
      final Color? foreground = style.foregroundColor?.resolve(<WidgetState>{});
      expect(foreground, AppColors.darkModePrimaryButtonText);
      expect(foreground, isNot(Colors.white));
    });

    test('disabled button uses line background and muted text', () {
      final ButtonStyle style = AppTheme.light().elevatedButtonTheme.style!;
      final Color? disabledBackground = style.backgroundColor
          ?.resolve(<WidgetState>{WidgetState.disabled});
      final Color? disabledForeground = style.foregroundColor
          ?.resolve(<WidgetState>{WidgetState.disabled});

      expect(disabledBackground, AppColors.light.line);
      expect(disabledForeground, AppColors.light.muted);
    });

    test('AppColorsExtension exposes the full palette via ThemeData', () {
      final AppColorsExtension? lightExtension =
          AppTheme.light().extension<AppColorsExtension>();
      final AppColorsExtension? darkExtension =
          AppTheme.dark().extension<AppColorsExtension>();

      expect(lightExtension?.colors, AppColors.light);
      expect(darkExtension?.colors, AppColors.dark);
    });

    // These assert against the assembled ThemeData.textTheme/colorScheme --
    // what `Theme.of(context)` actually hands a widget -- rather than the
    // AppTypography/AppColors source constants directly, so a regression in
    // AppTypography.textTheme()'s or AppTheme._build()'s mapping logic
    // itself would be caught even though every constant-level assertion
    // above still passes.
    test('light().textTheme.bodyLarge reflects AppTypography.body (17px/400)',
        () {
      final TextStyle? bodyLarge = AppTheme.light().textTheme.bodyLarge;
      expect(bodyLarge?.fontSize, 17);
      expect(bodyLarge?.fontWeight, FontWeight.w400);
      expect(bodyLarge?.height, 1.5);
    });

    test(
        'light().textTheme.bodyMedium reflects AppTypography.option '
        '(17px/600)', () {
      final TextStyle? bodyMedium = AppTheme.light().textTheme.bodyMedium;
      expect(bodyMedium?.fontSize, 17);
      expect(bodyMedium?.fontWeight, FontWeight.w600);
      expect(bodyMedium?.height, 1.4);
    });

    test(
        'light().textTheme.titleLarge reflects AppTypography.question '
        '(20px/700)', () {
      final TextStyle? titleLarge = AppTheme.light().textTheme.titleLarge;
      expect(titleLarge?.fontSize, 20);
      expect(titleLarge?.fontWeight, FontWeight.w700);
      expect(titleLarge?.height, 1.4);
    });

    test(
        'light().textTheme.labelLarge reflects AppTypography.label '
        '(13px/700)', () {
      final TextStyle? labelLarge = AppTheme.light().textTheme.labelLarge;
      expect(labelLarge?.fontSize, 13);
      expect(labelLarge?.fontWeight, FontWeight.w700);
      expect(labelLarge?.height, 1.3);
    });

    test('light().textTheme styles are tinted with AppColors.light.ink', () {
      expect(AppTheme.light().textTheme.bodyLarge?.color, AppColors.light.ink);
    });

    test('dark().textTheme styles are tinted with AppColors.dark.ink', () {
      expect(AppTheme.dark().textTheme.bodyLarge?.color, AppColors.dark.ink);
    });

    test(
        'dark().colorScheme.error/surface reflect AppColors.dark.danger/'
        'surface', () {
      final ColorScheme colorScheme = AppTheme.dark().colorScheme;
      expect(colorScheme.error, AppColors.dark.danger);
      expect(colorScheme.surface, AppColors.dark.surface);
    });

    test(
        'light().colorScheme.error/surface reflect AppColors.light.danger/'
        'surface', () {
      final ColorScheme colorScheme = AppTheme.light().colorScheme;
      expect(colorScheme.error, AppColors.light.danger);
      expect(colorScheme.surface, AppColors.light.surface);
    });
  });
}
