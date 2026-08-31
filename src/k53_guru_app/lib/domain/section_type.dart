/// Mirrors the backend's `SectionType` domain enum
/// (`src/K53Guru/src/Domain/Enums/SectionType.cs`), serialized as a string
/// by the server's global `JsonStringEnumConverter`. Section order is
/// always fixed: Rules of the Road -> Road Signs -> Vehicle Controls.
enum SectionType {
  rules,
  signs,
  vehicleControls;

  String toJson() {
    switch (this) {
      case SectionType.rules:
        return 'Rules';
      case SectionType.signs:
        return 'Signs';
      case SectionType.vehicleControls:
        return 'VehicleControls';
    }
  }

  static SectionType fromJson(String value) {
    switch (value.trim()) {
      case 'Rules':
        return SectionType.rules;
      case 'Signs':
        return SectionType.signs;
      case 'VehicleControls':
        return SectionType.vehicleControls;
      default:
        throw FormatException('Unknown SectionType: $value');
    }
  }
}
