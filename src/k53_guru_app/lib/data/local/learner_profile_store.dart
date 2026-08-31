import 'package:shared_preferences/shared_preferences.dart';

/// Wraps [SharedPreferences] to persist the learner's anonymous profile id
/// locally. A UUID v4 string (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) is
/// format-compatible with the backend's `Guid`-typed `LearnerProfileId`
/// parameters (Story 3.3's `StartAttemptCommand`) with no conversion
/// needed.
class LearnerProfileStore {
  const LearnerProfileStore();

  static const String _profileIdKey = 'learner_profile_id';

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
}
