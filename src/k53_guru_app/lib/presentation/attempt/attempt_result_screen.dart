import 'package:flutter/material.dart';

import '../../domain/code_result.dart';
import '../../domain/graded_attempt_result.dart';
import '../../domain/licence_code.dart';
import '../../domain/section_result.dart';
import '../../domain/section_type.dart';
import '../../theme/app_colors_extension.dart';
import '../../theme/app_spacing.dart';
import '../../theme/app_typography.dart';

/// Minimal results screen for the interactive attempt flow: overall
/// pass/fail plus a per-code, per-section breakdown from
/// [GradedAttemptResult]. Deliberately plain -- the full Epic 5/6 result
/// presentation (celebratory/encouraging framing, progression tracking) is
/// not built here. Exists so the real backend grading (Story 3.5/3.6) has
/// somewhere to actually surface its result in the app.
class AttemptResultScreen extends StatelessWidget {
  const AttemptResultScreen({super.key, required this.result});

  final GradedAttemptResult result;

  String _codeLabel(LicenceCode code) =>
      code.toJson().replaceAll('Code', 'Code ');

  String _sectionLabel(SectionType section) {
    switch (section) {
      case SectionType.rules:
        return 'Rules of the Road';
      case SectionType.signs:
        return 'Road Signs';
      case SectionType.vehicleControls:
        return 'Vehicle Controls';
    }
  }

  @override
  Widget build(BuildContext context) {
    final palette = context.appColors;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Result'),
        automaticallyImplyLeading: false,
      ),
      body: SafeArea(
        child: ListView(
          padding: const EdgeInsets.all(AppSpacing.space16),
          children: <Widget>[
            Container(
              padding: const EdgeInsets.all(AppSpacing.space16),
              decoration: BoxDecoration(
                color: result.passed ? palette.successSoft : palette.dangerSoft,
                borderRadius: BorderRadius.circular(AppRadius.md),
              ),
              child: Row(
                children: <Widget>[
                  Icon(
                    result.passed ? Icons.emoji_events : Icons.replay,
                    color: result.passed ? palette.success : palette.danger,
                    size: 32,
                  ),
                  const SizedBox(width: AppSpacing.space12),
                  Expanded(
                    child: Text(
                      result.passed ? 'Passed!' : 'Not yet -- keep practising',
                      style: AppTypography.question.copyWith(color: palette.ink),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.space24),
            ...result.codeResults.map(
              (CodeResult codeResult) => Card(
                margin: const EdgeInsets.only(bottom: AppSpacing.space12),
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.space16),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: <Widget>[
                      Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: <Widget>[
                          Text(
                            _codeLabel(codeResult.code),
                            style: AppTypography.option.copyWith(color: palette.ink),
                          ),
                          Icon(
                            codeResult.passed ? Icons.check_circle : Icons.cancel,
                            color: codeResult.passed ? palette.success : palette.danger,
                          ),
                        ],
                      ),
                      const SizedBox(height: AppSpacing.space8),
                      ...codeResult.sectionResults.map(
                        (SectionResult sr) => Padding(
                          padding: const EdgeInsets.symmetric(
                              vertical: AppSpacing.space4),
                          child: Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: <Widget>[
                              Text(
                                _sectionLabel(sr.section),
                                style: AppTypography.body.copyWith(color: palette.ink),
                              ),
                              Text(
                                '${sr.correctCount} correct (need ${sr.passMark}) '
                                '${sr.passed ? "✓" : "✗"}',
                                style: AppTypography.body.copyWith(
                                  color: sr.passed ? palette.success : palette.danger,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: AppSpacing.space16),
            OutlinedButton(
              onPressed: () =>
                  Navigator.of(context).popUntil((Route<dynamic> route) => route.isFirst),
              child: const Text('Back to sittings'),
            ),
          ],
        ),
      ),
    );
  }
}
