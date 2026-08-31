import 'licence_code.dart';

/// Mirrors the backend's `AvailableSittingDto`
/// (`src/K53Guru/src/Application/Features/Tests/DTOs/AvailableSittingDto.cs`),
/// returned by `GET /api/v1/sittings`.
class AvailableSitting {
  const AvailableSitting({
    required this.id,
    required this.codes,
    required this.name,
  });

  final int id;
  final List<LicenceCode> codes;
  final String? name;

  factory AvailableSitting.fromJson(Map<String, dynamic> json) {
    return AvailableSitting(
      id: json['id'] as int,
      codes: parseLicenceCodes(json['codes'] as String),
      name: json['name'] as String?,
    );
  }

  Map<String, dynamic> toJson() => <String, dynamic>{
        'id': id,
        'codes': licenceCodesToJson(codes),
        'name': name,
      };
}
