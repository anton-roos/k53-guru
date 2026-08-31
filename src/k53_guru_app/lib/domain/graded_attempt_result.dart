import 'code_result.dart';

/// Mirrors the backend's `GradedAttemptResultDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/GradedAttemptResultDto.cs`),
/// returned by `POST /api/v1/attempts/{id}/submit`.
class GradedAttemptResult {
  const GradedAttemptResult({
    required this.attemptId,
    required this.passed,
    required this.codeResults,
  });

  final int attemptId;
  final bool passed;
  final List<CodeResult> codeResults;

  factory GradedAttemptResult.fromJson(Map<String, dynamic> json) {
    return GradedAttemptResult(
      attemptId: json['attemptId'] as int,
      passed: json['passed'] as bool,
      codeResults: (json['codeResults'] as List<dynamic>)
          .map((dynamic e) => CodeResult.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'attemptId': attemptId,
        'passed': passed,
        'codeResults': codeResults.map((CodeResult r) => r.toJson()).toList(),
      };
}
