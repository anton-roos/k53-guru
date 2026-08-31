import 'attempt_mode.dart';
import 'attempt_question.dart';
import 'licence_code.dart';

/// Mirrors the backend's `AttemptDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs`).
///
/// `code` is a list because the backend's `LicenceCode` is a `[Flags]`
/// enum -- a sitting (and therefore its attempt) may target a single code
/// or a valid combination (see `parseLicenceCodes`).
class Attempt {
  const Attempt({
    required this.id,
    required this.code,
    required this.mode,
    required this.startedAt,
    required this.attemptQuestions,
  });

  final int id;
  final List<LicenceCode> code;
  final AttemptMode mode;
  final DateTime startedAt;
  final List<AttemptQuestion> attemptQuestions;

  factory Attempt.fromJson(Map<String, dynamic> json) {
    return Attempt(
      id: json['id'] as int,
      code: parseLicenceCodes(json['code'] as String),
      mode: AttemptMode.fromJson(json['mode'] as String),
      startedAt: DateTime.parse(json['startedAt'] as String),
      attemptQuestions: (json['attemptQuestions'] as List<dynamic>)
          .map((dynamic e) => AttemptQuestion.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'code': licenceCodesToJson(code),
        'mode': mode.toJson(),
        'startedAt': startedAt.toIso8601String(),
        'attemptQuestions':
            attemptQuestions.map((AttemptQuestion q) => q.toJson()).toList(),
      };
}
