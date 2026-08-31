/// Mirrors the backend's `CheckAnswerResultDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/CheckAnswerResultDto.cs`),
/// returned by `POST /api/v1/attempts/{id}/check-answer` (Practice mode
/// only).
class CheckAnswerResult {
  const CheckAnswerResult({
    required this.isCorrect,
    required this.correctAttemptAnswerOptionId,
    required this.explanation,
  });

  final bool isCorrect;
  final int correctAttemptAnswerOptionId;
  final String? explanation;

  factory CheckAnswerResult.fromJson(Map<String, dynamic> json) {
    return CheckAnswerResult(
      isCorrect: json['isCorrect'] as bool,
      correctAttemptAnswerOptionId: json['correctAttemptAnswerOptionId'] as int,
      explanation: json['explanation'] as String?,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'isCorrect': isCorrect,
        'correctAttemptAnswerOptionId': correctAttemptAnswerOptionId,
        'explanation': explanation,
      };
}
