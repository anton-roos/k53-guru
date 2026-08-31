import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../domain/attempt.dart';
import '../../domain/attempt_mode.dart';
import '../../domain/check_answer_result.dart';
import '../../domain/graded_attempt_result.dart';
import '../api/attempt_answer_submission.dart';
import '../api/k53_api_client.dart';
import 'providers.dart';

/// The only layer widgets are allowed to depend on for attempt data --
/// wraps [K53ApiClient] so no HTTP call ever appears in a widget's
/// `build()` method.
class AttemptsRepository {
  const AttemptsRepository(this._apiClient);

  final K53ApiClient _apiClient;

  Future<Attempt> startAttempt({
    required String learnerProfileId,
    required int testId,
    required AttemptMode mode,
  }) {
    return _apiClient.startAttempt(
      learnerProfileId: learnerProfileId,
      testId: testId,
      mode: mode,
    );
  }

  Future<Attempt> getAttempt({
    required int attemptId,
    required String learnerProfileId,
  }) {
    return _apiClient.getAttempt(
      attemptId: attemptId,
      learnerProfileId: learnerProfileId,
    );
  }

  Future<GradedAttemptResult> submitAttempt({
    required int attemptId,
    required String learnerProfileId,
    required List<AttemptAnswerSubmission> answers,
    DateTime? clientSubmittedAt,
  }) {
    return _apiClient.submitAttempt(
      attemptId: attemptId,
      learnerProfileId: learnerProfileId,
      answers: answers,
      clientSubmittedAt: clientSubmittedAt,
    );
  }

  Future<CheckAnswerResult> checkAnswer({
    required int attemptId,
    required String learnerProfileId,
    required int attemptQuestionId,
    required int selectedAttemptAnswerOptionId,
  }) {
    return _apiClient.checkAnswer(
      attemptId: attemptId,
      learnerProfileId: learnerProfileId,
      attemptQuestionId: attemptQuestionId,
      selectedAttemptAnswerOptionId: selectedAttemptAnswerOptionId,
    );
  }
}

final Provider<AttemptsRepository> attemptsRepositoryProvider =
    Provider<AttemptsRepository>((Ref ref) {
  return AttemptsRepository(ref.watch(k53ApiClientProvider));
});
