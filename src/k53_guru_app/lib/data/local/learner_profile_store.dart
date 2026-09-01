import 'package:shared_preferences/shared_preferences.dart';

import '../../domain/licence_code.dart';

/// Wraps [SharedPreferences] to persist the learner's anonymous profile id
/// locally. A UUID v4 string (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) is
/// format-compatible with the backend's `Guid`-typed `LearnerProfileId`
/// parameters (Story 3.3's `StartAttemptCommand`) with no conversion
/// needed.
///
/// Story 4.5 adds [readLicenceCode]/[writeLicenceCode]: the learner's own
/// chosen [LicenceCode] (always exactly one -- never a combination) is a
/// purely device-local setting, same `SharedPreferences`-backed pattern and
/// the same graceful-degradation-on-failure behaviour as the profile id
/// above, and is never synced to or read from the backend.
class LearnerProfileStore {
  const LearnerProfileStore();

  static const String _profileIdKey = 'learner_profile_id';
  static const String _licenceCodeKey = 'learner_licence_code';

  /// Matches a UUID v4 string (case-insensitive), the only shape a value
  /// written by [writeProfileId] can ever take.
  static final RegExp _uuidV4Pattern = RegExp(
    r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
    caseSensitive: false,
  );

  /// Reads the persisted profile id.
  ///
  /// Returns `null` when no id has ever been written (a fresh install --
  /// the genuine first-run case), when local storage cannot be read at
  /// all, or when the persisted string is non-empty but not a well-formed
  /// UUID v4 (e.g. corrupted by a partial disk write) -- "corrupt data" is
  /// only ever an id-shaped value in practice, so a format check is the
  /// concrete way to detect it. Per the spec's edge-case matrix, any
  /// storage failure or corrupt value is always treated as "no profile
  /// id" (the first-run path) and must never crash the app.
  Future<String?> readProfileId() async {
    try {
      final SharedPreferences prefs = await SharedPreferences.getInstance();
      final String? id = prefs.getString(_profileIdKey);
      if (id == null || id.isEmpty || !_uuidV4Pattern.hasMatch(id)) {
        return null;
      }
      return id;
    } catch (_) {
      return null;
    }
  }

  /// Persists [id] as the learner's profile id.
  Future<void> writeProfileId(String id) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString(_profileIdKey, id);
  }

  /// Reads the persisted licence code.
  ///
  /// Returns `null` when no code has ever been written (no code chosen
  /// yet -- either a genuinely fresh profile or one just restored per Story
  /// 4.4, since licence code is never restored/synced), when local storage
  /// cannot be read at all, or when the persisted string doesn't match one
  /// of [LicenceCode]'s wire names (corrupted by a partial disk write).
  /// Per the spec's edge-case matrix, any storage failure or corrupt value
  /// is always treated as "no code chosen yet" and must never crash the
  /// app.
  Future<LicenceCode?> readLicenceCode() async {
    try {
      final SharedPreferences prefs = await SharedPreferences.getInstance();
      final String? value = prefs.getString(_licenceCodeKey);
      if (value == null || value.isEmpty) {
        return null;
      }
      return LicenceCode.fromJson(value);
    } catch (_) {
      return null;
    }
  }

  /// Persists [code] as the learner's chosen licence code, replacing
  /// whatever was previously stored (used both by first-run selection and
  /// by the Profile tab's `Change code` flow).
  Future<void> writeLicenceCode(LicenceCode code) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString(_licenceCodeKey, code.toJson());
  }
}
