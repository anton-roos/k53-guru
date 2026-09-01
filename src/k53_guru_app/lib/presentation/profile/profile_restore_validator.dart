import '../onboarding/learner_profile_provider.dart';

/// The outcome of a [ProfileRestoreValidator.validateAndPersist] call.
///
/// Deliberately a closed set rather than throwing exceptions -- both
/// `restore_profile_screen.dart` entry points (QR-scan and manual-paste)
/// need to react to the exact same two outcomes with identical UI, and a
/// sealed result type makes that a `switch` rather than a `try/catch`.
enum ProfileRestoreResult {
  /// The provided value was a well-formed UUID v4 and has been persisted as
  /// the learner's profile id.
  restored,

  /// The provided value failed the UUID v4 format check. Nothing was
  /// persisted and no state changed.
  invalidFormat,
}

/// Matches a UUID v4 string (case-insensitive, optionally surrounded by
/// whitespace from a paste or a QR payload with a trailing newline).
///
/// Kept identical to `LearnerProfileStore`'s own pattern (Story 4.3) -- this
/// is the same format check, applied to learner-supplied input instead of
/// previously-trusted local storage.
final RegExp uuidV4Pattern = RegExp(
  r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
  caseSensitive: false,
);

/// The single shared validate-and-persist function used by both the
/// QR-scan and manual-paste restore paths (spec's "Never" boundary: both
/// entry methods must funnel through one function so their behaviour --
/// including the error case -- is identical regardless of entry method).
///
/// Deliberately decoupled from any widget (in particular, from
/// `mobile_scanner`'s camera preview) so it is independently unit-testable
/// without a physical device/camera -- see
/// `test/presentation/profile_restore_validator_test.dart`.
///
/// Validation is entirely client-side format checking: there is no backend
/// "does this profile exist" endpoint (see Design Notes in the spec). A
/// well-formed-but-never-used UUID cannot be distinguished from a real one
/// here, and is treated identically to any other well-formed UUID --
/// restoring proceeds.
class ProfileRestoreValidator {
  const ProfileRestoreValidator();

  /// Validates [rawInput] as a UUID v4 (after trimming surrounding
  /// whitespace) and, if well-formed, persists it as the learner's profile
  /// id via [notifier]'s own [LearnerProfileNotifier.restoreProfileId] --
  /// which writes it to [LearnerProfileStore] and updates provider state so
  /// the root router in `main.dart` (which watches [learnerProfileProvider])
  /// navigates to `AppShell` immediately, exactly as a freshly generated id
  /// does.
  ///
  /// Returns [ProfileRestoreResult.invalidFormat] without touching storage
  /// or [notifier] at all when [rawInput] fails the format check -- "nothing
  /// persisted and no state change" per the spec's edge-case matrix.
  Future<ProfileRestoreResult> validateAndPersist(
    String rawInput,
    LearnerProfileNotifier notifier,
  ) async {
    final String trimmed = rawInput.trim();
    if (!uuidV4Pattern.hasMatch(trimmed)) {
      return ProfileRestoreResult.invalidFormat;
    }

    // Normalized to lower-case: the persisted value should always match the
    // exact shape `LearnerProfileStore`'s own reader expects, regardless of
    // the case a learner pasted or a QR encoder produced.
    final String normalized = trimmed.toLowerCase();
    await notifier.restoreProfileId(normalized);
    return ProfileRestoreResult.restored;
  }
}
