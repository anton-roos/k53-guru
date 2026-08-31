import 'package:flutter/material.dart';

/// The 6 named type-scale styles from DESIGN.md's frontmatter
/// (`typography.scale`). `TextStyle.height` is a multiplier of the font
/// size, matching the unitless `line` values in DESIGN.md.
///
/// No bundled font asset ships with this story (the spec explicitly adds
/// no dependencies beyond `flutter_riverpod`/`http`), so `fontFamily`
/// names DESIGN.md's preferred family and `fontFamilyFallback` lists its
/// fallbacks; the platform's default sans-serif renders until a real
/// Inter asset is added.
class AppTypography {
  const AppTypography._();

  static const String fontFamily = 'Inter';
  static const List<String> fontFamilyFallback = <String>[
    'system-ui',
    '-apple-system',
    'Segoe UI',
    'Roboto',
  ];

  /// Screen titles. 28px/800, line-height 1.2.
  static const TextStyle display = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 28,
    fontWeight: FontWeight.w800,
    height: 1.2,
  );

  /// Section headings. 22px/800, line-height 1.25.
  static const TextStyle h2 = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 22,
    fontWeight: FontWeight.w800,
    height: 1.25,
  );

  /// Question stem -- deliberately large per the accessibility mandate.
  /// 20px/700, line-height 1.4.
  static const TextStyle question = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 20,
    fontWeight: FontWeight.w700,
    height: 1.4,
  );

  /// Minimum body size -- never smaller than this anywhere in the app.
  /// 17px/400, line-height 1.5.
  static const TextStyle body = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 17,
    fontWeight: FontWeight.w400,
    height: 1.5,
  );

  /// Answer-option label text. 17px/600, line-height 1.4.
  static const TextStyle option = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 17,
    fontWeight: FontWeight.w600,
    height: 1.4,
  );

  /// Chips, meta text. 13px/700, line-height 1.3.
  static const TextStyle label = TextStyle(
    fontFamily: fontFamily,
    fontFamilyFallback: fontFamilyFallback,
    fontSize: 13,
    fontWeight: FontWeight.w700,
    height: 1.3,
  );

  /// Builds a Material `TextTheme` from the 6 named styles above, tinted
  /// with [inkColor], so framework widgets that read `Theme.of(context)
  /// .textTheme` (rather than the named constants directly) still get
  /// DESIGN.md-accurate type.
  static TextTheme textTheme(Color inkColor) {
    final display_ = display.copyWith(color: inkColor);
    final h2_ = h2.copyWith(color: inkColor);
    final question_ = question.copyWith(color: inkColor);
    final body_ = body.copyWith(color: inkColor);
    final option_ = option.copyWith(color: inkColor);
    final label_ = label.copyWith(color: inkColor);

    return TextTheme(
      displayLarge: display_,
      displayMedium: display_,
      displaySmall: display_,
      headlineLarge: h2_,
      headlineMedium: h2_,
      headlineSmall: h2_,
      titleLarge: question_,
      titleMedium: question_,
      titleSmall: question_,
      bodyLarge: body_,
      bodyMedium: option_,
      bodySmall: body_,
      labelLarge: label_,
      labelMedium: label_,
      labelSmall: label_,
    );
  }
}
