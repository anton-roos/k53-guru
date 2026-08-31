import 'attempt_answer_option.dart';
import 'licence_code.dart';
import 'section_type.dart';

/// Mirrors the backend's `AttemptQuestionDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs`).
///
/// This is the immutable per-attempt snapshot of a question. `code` is a
/// list because the backend's `LicenceCode` is a `[Flags]` enum with a
/// dual meaning here (see `AttemptQuestion.Code`'s doc comment in
/// `src/K53Guru/src/Domain/Entities/AttemptQuestion.cs`): for Rules/Signs
/// questions shared across a combination attempt, this is the FULL
/// combination value (e.g. `"Code1, Code2"`); only for VehicleControls
/// questions is it a single constituent code. Always parse/serialize it
/// via [parseLicenceCodes]/[licenceCodesToJson], never
/// `LicenceCode.fromJson`.
class AttemptQuestion {
  const AttemptQuestion({
    required this.id,
    required this.section,
    required this.code,
    required this.displayOrder,
    required this.stem,
    required this.signRef,
    required this.attemptAnswerOptions,
  });

  final int id;
  final SectionType section;
  final List<LicenceCode> code;
  final int displayOrder;
  final String? stem;
  final String? signRef;
  final List<AttemptAnswerOption> attemptAnswerOptions;

  factory AttemptQuestion.fromJson(Map<String, dynamic> json) {
    return AttemptQuestion(
      id: json['id'] as int,
      section: SectionType.fromJson(json['section'] as String),
      code: parseLicenceCodes(json['code'] as String),
      displayOrder: json['displayOrder'] as int,
      stem: json['stem'] as String?,
      signRef: json['signRef'] as String?,
      attemptAnswerOptions: (json['attemptAnswerOptions'] as List<dynamic>)
          .map((dynamic e) =>
              AttemptAnswerOption.fromJson(e as Map<String, dynamic>))
          .toList(),
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'section': section.toJson(),
        'code': licenceCodesToJson(code),
        'displayOrder': displayOrder,
        'stem': stem,
        'signRef': signRef,
        'attemptAnswerOptions':
            attemptAnswerOptions.map((AttemptAnswerOption o) => o.toJson()).toList(),
      };
}
