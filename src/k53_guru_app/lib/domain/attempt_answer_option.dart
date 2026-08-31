/// Mirrors the backend's `AttemptAnswerOptionDto`
/// (`src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs`).
///
/// Deliberately carries no `isCorrect`/correct-key field -- the backend
/// never puts one on the wire for an in-progress attempt (answer
/// confidentiality), so there is nothing here to mirror.
class AttemptAnswerOption {
  const AttemptAnswerOption({
    required this.id,
    required this.text,
    required this.order,
  });

  final int id;
  final String? text;
  final int order;

  factory AttemptAnswerOption.fromJson(Map<String, dynamic> json) {
    return AttemptAnswerOption(
      id: json['id'] as int,
      text: json['text'] as String?,
      order: json['order'] as int,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'text': text,
        'order': order,
      };
}
