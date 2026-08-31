/// Mirrors the backend's `LicenceCode` domain enum
/// (`src/K53Guru/src/Domain/Enums/LicenceCode.cs`).
///
/// The backend enum is a `[Flags]` enum (`None = 0, Code1 = 1, Code2 = 2,
/// Code3 = 4`) so a combination sitting/attempt serializes as a
/// comma-joined string (e.g. `"Code1, Code2"`) via the server's global
/// `JsonStringEnumConverter`. A single code serializes as just its name
/// (e.g. `"Code1"`). There is no Dart equivalent of a C# flags enum, so a
/// wire value is represented here as `List<LicenceCode>`.
enum LicenceCode {
  code1,
  code2,
  code3;

  /// The exact wire name for a single code, matching the C# enum member
  /// name (e.g. `LicenceCode.code1` -> `"Code1"`).
  String toJson() {
    switch (this) {
      case LicenceCode.code1:
        return 'Code1';
      case LicenceCode.code2:
        return 'Code2';
      case LicenceCode.code3:
        return 'Code3';
    }
  }

  static LicenceCode _fromSingle(String value) {
    switch (value.trim()) {
      case 'Code1':
        return LicenceCode.code1;
      case 'Code2':
        return LicenceCode.code2;
      case 'Code3':
        return LicenceCode.code3;
      default:
        throw FormatException('Unknown LicenceCode: $value');
    }
  }

  /// Parses a single-code wire value (used by fields that are always a
  /// single code, e.g. `AttemptQuestionDto.Code`, `CodeResultDto.Code`).
  static LicenceCode fromJson(String value) => LicenceCode._fromSingle(value);
}

/// Parses a possibly comma-joined flags string (e.g. `"Code1, Code2"`,
/// or `"None"`/`""` for no codes) into the individual [LicenceCode]s it
/// represents. Used by fields that may carry a combination
/// (`AvailableSittingDto.Codes`, `AttemptDto.Code`).
List<LicenceCode> parseLicenceCodes(String value) {
  final String trimmed = value.trim();
  if (trimmed.isEmpty || trimmed == 'None') return const <LicenceCode>[];
  return trimmed
      .split(',')
      .map((String part) => LicenceCode._fromSingle(part))
      .toList();
}

/// Serializes a list of [LicenceCode]s back to the backend's comma-joined
/// flags format.
String licenceCodesToJson(List<LicenceCode> codes) =>
    codes.map((LicenceCode c) => c.toJson()).join(', ');
