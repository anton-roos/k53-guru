/// One answer within a `POST /api/v1/attempts/{id}/submit` request body,
/// mirroring the backend's `AnswerSubmission` shape nested in
/// `SubmitAttemptCommand`
/// (`src/K53Guru/src/Application/Features/Attempts/Commands/Submit/SubmitAttemptCommand.cs`).
///
/// This is a request-only shape (never returned by the API), so it lives
/// alongside the API client rather than in `lib/domain/`.
class AttemptAnswerSubmission {
  const AttemptAnswerSubmission({
    required this.attemptQuestionId,
    required this.selectedAttemptAnswerOptionId,
  });

  final int attemptQuestionId;
  final int selectedAttemptAnswerOptionId;

  Map<String, dynamic> toJson() => <String, dynamic>{
        'attemptQuestionId': attemptQuestionId,
        'selectedAttemptAnswerOptionId': selectedAttemptAnswerOptionId,
      };
}
