/// Mirrors the backend's `AttemptMode` domain enum
/// (`src/K53Guru/src/Domain/Enums/AttemptMode.cs`), serialized as a string
/// by the server's global `JsonStringEnumConverter`.
enum AttemptMode {
  practice,
  test;

  String toJson() {
    switch (this) {
      case AttemptMode.practice:
        return 'Practice';
      case AttemptMode.test:
        return 'Test';
    }
  }

  static AttemptMode fromJson(String value) {
    switch (value.trim()) {
      case 'Practice':
        return AttemptMode.practice;
      case 'Test':
        return AttemptMode.test;
      default:
        throw FormatException('Unknown AttemptMode: $value');
    }
  }
}
