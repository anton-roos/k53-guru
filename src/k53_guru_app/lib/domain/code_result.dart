import 'licence_code.dart';
import 'section_result.dart';

/// Mirrors the backend's `CodeResultDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/GradedAttemptResultDto.cs`).
///
/// `code` is always a single code here -- for a combination sitting the
/// backend grades each code independently and returns one `CodeResultDto`
/// per code, so no field on this DTO is ever a flags combo.
class CodeResult {
  const CodeResult({
    required this.code,
    required this.passed,
    required this.sectionResults,
  });

  final LicenceCode code;
  final bool passed;
  final List<SectionResult> sectionResults;

  factory CodeResult.fromJson(Map<String, dynamic> json) {
    return CodeResult(
      code: LicenceCode.fromJson(json['code'] as String),
      passed: json['passed'] as bool,
      sectionResults: (json['sectionResults'] as List<dynamic>)
          .map((dynamic e) => SectionResult.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'code': code.toJson(),
        'passed': passed,
        'sectionResults':
            sectionResults.map((SectionResult r) => r.toJson()).toList(),
      };
}
