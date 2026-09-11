import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/api/attempt_answer_submission.dart';
import '../../data/repository/attempts_repository.dart';
import '../../domain/attempt.dart';
import '../../domain/attempt_answer_option.dart';
import '../../domain/attempt_mode.dart';
import '../../domain/attempt_question.dart';
import '../../domain/available_sitting.dart';
import '../../domain/check_answer_result.dart';
import '../../domain/section_type.dart';
import '../../theme/app_colors_extension.dart';
import '../../theme/app_spacing.dart';
import '../../theme/app_typography.dart';
import '../onboarding/learner_profile_provider.dart';
import 'attempt_result_screen.dart';

/// A minimal, functional Practice-mode attempt flow: start an attempt for
/// the tapped sitting, answer its snapshotted questions one at a time with
/// immediate Practice-mode feedback ([AttemptsRepository.checkAnswer]),
/// then submit for grading. Deliberately plain -- the full Epic 5/6
/// experience (warm feedback animations, resume-in-place, streaks/XP,
/// Test-mode timing and answer confidentiality) is not built here. This
/// exists so the real backend content (questions, sittings) already
/// seeded has something to exercise end-to-end from the app; the sign
/// image itself isn't shown (no public API exposes a road sign's image by
/// code yet -- only the Admin Panel can see it), just the sign's
/// legislation code and the question stem.
///
/// Always starts a fresh attempt rather than resuming one in progress --
/// resume-in-place is Epic 5/6 scope (see `GetAttemptQuery`, unused by
/// this screen).
class AttemptScreen extends ConsumerStatefulWidget {
  const AttemptScreen({super.key, required this.sitting});

  final AvailableSitting sitting;

  @override
  ConsumerState<AttemptScreen> createState() => _AttemptScreenState();
}

class _AttemptScreenState extends ConsumerState<AttemptScreen> {
  late final Future<Attempt> _attemptFuture;
  int _currentIndex = 0;
  int? _selectedOptionId;
  CheckAnswerResult? _checkResult;
  bool _isChecking = false;
  bool _isSubmitting = false;
  final Map<int, int> _answersByQuestionId = <int, int>{};

  @override
  void initState() {
    super.initState();
    _attemptFuture = _startAttempt();
  }

  Future<Attempt> _startAttempt() async {
    // Awaits the provider's own Future rather than reading `.value`
    // synchronously -- `.value` is only populated once the provider has
    // already resolved elsewhere first, which happens to be true in the
    // real app (main.dart's root router watches it before AppShell is ever
    // reachable) but is not something this screen should rely on
    // implicitly. `.future` resolves correctly whether the provider is
    // already resolved, still in flight, or has never been read before.
    final String? learnerProfileId = await ref.read(learnerProfileProvider.future);
    if (learnerProfileId == null) {
      throw StateError('No learner profile id available.');
    }
    return ref.read(attemptsRepositoryProvider).startAttempt(
          learnerProfileId: learnerProfileId,
          testId: widget.sitting.id,
          mode: AttemptMode.practice,
        );
  }

  Future<void> _onCheckPressed(Attempt attempt, AttemptQuestion question) async {
    if (_selectedOptionId == null || _isChecking) return;
    final int selected = _selectedOptionId!;
    setState(() => _isChecking = true);
    try {
      final String learnerProfileId =
          (await ref.read(learnerProfileProvider.future))!;
      final CheckAnswerResult result =
          await ref.read(attemptsRepositoryProvider).checkAnswer(
                attemptId: attempt.id,
                learnerProfileId: learnerProfileId,
                attemptQuestionId: question.id,
                selectedAttemptAnswerOptionId: selected,
              );
      _answersByQuestionId[question.id] = selected;
      if (!mounted) return;
      setState(() {
        _checkResult = result;
        _isChecking = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() => _isChecking = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Failed to check answer: $e')),
      );
    }
  }

  void _onNextPressed() {
    setState(() {
      _currentIndex++;
      _selectedOptionId = null;
      _checkResult = null;
    });
  }

  Future<void> _onFinishPressed(Attempt attempt) async {
    if (_isSubmitting) return;
    setState(() => _isSubmitting = true);
    try {
      final String learnerProfileId =
          (await ref.read(learnerProfileProvider.future))!;
      final List<AttemptAnswerSubmission> answers = _answersByQuestionId.entries
          .map((MapEntry<int, int> e) => AttemptAnswerSubmission(
                attemptQuestionId: e.key,
                selectedAttemptAnswerOptionId: e.value,
              ))
          .toList();
      final result = await ref.read(attemptsRepositoryProvider).submitAttempt(
            attemptId: attempt.id,
            learnerProfileId: learnerProfileId,
            answers: answers,
          );
      if (!mounted) return;
      Navigator.of(context).pushReplacement(
        MaterialPageRoute<void>(
          builder: (_) => AttemptResultScreen(result: result),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text('Failed to submit attempt: $e')),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(widget.sitting.name ?? 'Practice')),
      body: SafeArea(
        child: FutureBuilder<Attempt>(
          future: _attemptFuture,
          builder: (BuildContext context, AsyncSnapshot<Attempt> snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return Center(
                child: Padding(
                  padding: const EdgeInsets.all(AppSpacing.space16),
                  child: Text(
                    'Failed to start attempt: ${snapshot.error}',
                    style: AppTypography.body,
                    textAlign: TextAlign.center,
                  ),
                ),
              );
            }
            final Attempt attempt = snapshot.data!;
            if (attempt.attemptQuestions.isEmpty) {
              return Center(
                child: Text(
                  'This sitting has no questions yet.',
                  style: AppTypography.body,
                ),
              );
            }
            if (_currentIndex >= attempt.attemptQuestions.length) {
              // Not normally reachable (Finish navigates away on the last
              // question) -- defensive guard against a stray rebuild.
              return const Center(child: CircularProgressIndicator());
            }
            final AttemptQuestion question =
                attempt.attemptQuestions[_currentIndex];
            final bool isLast =
                _currentIndex == attempt.attemptQuestions.length - 1;
            return _QuestionView(
              question: question,
              questionNumber: _currentIndex + 1,
              totalQuestions: attempt.attemptQuestions.length,
              selectedOptionId: _selectedOptionId,
              checkResult: _checkResult,
              isChecking: _isChecking,
              isSubmitting: _isSubmitting,
              isLast: isLast,
              onOptionSelected: _checkResult == null
                  ? (int id) => setState(() => _selectedOptionId = id)
                  : null,
              onCheckPressed: () => _onCheckPressed(attempt, question),
              onNextPressed: _onNextPressed,
              onFinishPressed: () => _onFinishPressed(attempt),
            );
          },
        ),
      ),
    );
  }
}

class _QuestionView extends StatelessWidget {
  const _QuestionView({
    required this.question,
    required this.questionNumber,
    required this.totalQuestions,
    required this.selectedOptionId,
    required this.checkResult,
    required this.isChecking,
    required this.isSubmitting,
    required this.isLast,
    required this.onOptionSelected,
    required this.onCheckPressed,
    required this.onNextPressed,
    required this.onFinishPressed,
  });

  final AttemptQuestion question;
  final int questionNumber;
  final int totalQuestions;
  final int? selectedOptionId;
  final CheckAnswerResult? checkResult;
  final bool isChecking;
  final bool isSubmitting;
  final bool isLast;
  final ValueChanged<int>? onOptionSelected;
  final VoidCallback onCheckPressed;
  final VoidCallback onNextPressed;
  final VoidCallback onFinishPressed;

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
    return SingleChildScrollView(
      padding: const EdgeInsets.all(AppSpacing.space16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: <Widget>[
          Text(
            '${_sectionLabel(question.section)} · Question $questionNumber of $totalQuestions',
            style: AppTypography.label.copyWith(color: palette.muted),
          ),
          const SizedBox(height: AppSpacing.space12),
          if (question.signRef != null) ...<Widget>[
            Container(
              padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.space12,
                vertical: AppSpacing.space8,
              ),
              decoration: BoxDecoration(
                color: palette.card,
                borderRadius: BorderRadius.circular(AppRadius.sm),
                border: Border.all(color: palette.line),
              ),
              child: Text(
                'Sign: ${question.signRef}',
                style: AppTypography.label.copyWith(color: palette.muted),
              ),
            ),
            const SizedBox(height: AppSpacing.space12),
          ],
          Text(
            question.stem ?? 'Question',
            style: AppTypography.question.copyWith(color: palette.ink),
          ),
          const SizedBox(height: AppSpacing.space16),
          RadioGroup<int>(
            groupValue: selectedOptionId,
            // RadioGroup's onChanged isn't nullable the way a single
            // RadioListTile's is (that's how it disabled selection once an
            // answer's been checked) -- always pass a callback, and no-op
            // inside it when selection should be locked.
            onChanged: (int? value) {
              if (value != null) onOptionSelected?.call(value);
            },
            child: Column(
              children: question.attemptAnswerOptions.map((AttemptAnswerOption option) {
                final bool isSelected = selectedOptionId == option.id;
                Color? tileColor;
                if (checkResult != null) {
                  final bool isThisCorrect =
                      option.id == checkResult!.correctAttemptAnswerOptionId;
                  if (isThisCorrect) {
                    tileColor = palette.successSoft;
                  } else if (isSelected) {
                    tileColor = palette.dangerSoft;
                  }
                }
                return Card(
                  color: tileColor,
                  margin: const EdgeInsets.only(bottom: AppSpacing.space8),
                  child: RadioListTile<int>(
                    value: option.id,
                    enabled: onOptionSelected != null,
                    title: Text(option.text ?? ''),
                  ),
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: AppSpacing.space8),
          if (checkResult != null) ...<Widget>[
            Container(
              padding: const EdgeInsets.all(AppSpacing.space12),
              decoration: BoxDecoration(
                color: checkResult!.isCorrect ? palette.successSoft : palette.dangerSoft,
                borderRadius: BorderRadius.circular(AppRadius.sm),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: <Widget>[
                  Icon(
                    checkResult!.isCorrect ? Icons.check_circle : Icons.cancel,
                    color: checkResult!.isCorrect ? palette.success : palette.danger,
                  ),
                  const SizedBox(width: AppSpacing.space8),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: <Widget>[
                        Text(
                          checkResult!.isCorrect ? 'Correct' : 'Incorrect',
                          style: AppTypography.option.copyWith(color: palette.ink),
                        ),
                        if (checkResult!.explanation != null) ...<Widget>[
                          const SizedBox(height: AppSpacing.space4),
                          Text(
                            checkResult!.explanation!,
                            style: AppTypography.body.copyWith(color: palette.ink),
                          ),
                        ],
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: AppSpacing.space16),
          ],
          if (checkResult == null)
            ElevatedButton(
              onPressed: selectedOptionId == null || isChecking ? null : onCheckPressed,
              child: isChecking
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Check answer'),
            )
          else if (isLast)
            ElevatedButton(
              onPressed: isSubmitting ? null : onFinishPressed,
              child: isSubmitting
                  ? const SizedBox(
                      width: 20,
                      height: 20,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    )
                  : const Text('Finish'),
            )
          else
            ElevatedButton(
              onPressed: onNextPressed,
              child: const Text('Next question'),
            ),
        ],
      ),
    );
  }
}
