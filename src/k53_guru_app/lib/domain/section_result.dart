import 'section_type.dart';

/// Mirrors the backend's `SectionResultDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/GradedAttemptResultDto.cs`).
class SectionResult {
  const SectionResult({
    required this.section,
    required this.correctCount,
    required this.passMark,
    required this.passed,
  });

  final SectionType section;
  final int correctCount;
  final int passMark;
  final bool passed;

  factory SectionResult.fromJson(Map<String, dynamic> json) {
    return SectionResult(
      section: SectionType.fromJson(json['section'] as String),
      correctCount: json['correctCount'] as int,
      passMark: json['passMark'] as int,
      passed: json['passed'] as bool,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'section': section.toJson(),
        'correctCount': correctCount,
        'passMark': passMark,
        'passed': passed,
      };
}
