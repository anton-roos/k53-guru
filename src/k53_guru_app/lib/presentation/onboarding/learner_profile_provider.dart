import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:uuid/uuid.dart';

import '../../data/local/learner_profile_store.dart';

/// Exposes the learner's anonymous profile id app-wide.
///
/// On first read, [build] checks local storage via [LearnerProfileStore]:
/// - `null` means no profile id has ever been persisted -- this IS the
///   first run. The id is deliberately NOT generated here; that only
///   happens when the learner taps `Start learning` (see
///   `start_learning_screen.dart`) via [generateAndPersistProfileId].
/// - a non-null id means a returning learner; it's exposed immediately, no
///   first-run friction.
///
/// This is the single source of truth any future screen (Epic 5/6) reads
/// to supply the learner's opaque profile key to `K53ApiClient` calls.
class LearnerProfileNotifier extends AsyncNotifier<String?> {
  LearnerProfileNotifier({LearnerProfileStore? store, Uuid? uuid})
      : _store = store ?? const LearnerProfileStore(),
        _uuid = uuid ?? const Uuid();

  final LearnerProfileStore _store;
  final Uuid _uuid;

  @override
  Future<String?> build() => _store.readProfileId();

  /// First-run only: generates a new UUID v4, persists it via
  /// [LearnerProfileStore], and updates provider state so every consumer
  /// -- including the root router in `main.dart` -- sees it immediately.
  ///
  /// Explicitly lower-cased before persisting: the `uuid` package's `v4()`
  /// always produces lowercase hex today, but this call makes that an
  /// explicit guarantee of this method rather than an unstated assumption
  /// about a third-party package's implementation detail -- matching
  /// `ProfileRestoreValidator.validateAndPersist`, which makes the same
  /// guarantee explicitly for the restore path.
  Future<String> generateAndPersistProfileId() async {
    final String id = _uuid.v4().toLowerCase();
    await _store.writeProfileId(id);
    state = AsyncData<String?>(id);
    return id;
  }

  /// Story 4.4's restore flow: persists a caller-supplied [id] as the
  /// learner's profile id and updates provider state, exactly like
  /// [generateAndPersistProfileId] but for an id the learner already had
  /// (from a QR scan or manual paste) rather than a freshly generated one.
  ///
  /// Callers -- specifically `ProfileRestoreValidator` -- are responsible
  /// for format-validating [id] first; this method persists whatever it is
  /// given without re-checking it.
  Future<void> restoreProfileId(String id) async {
    await _store.writeProfileId(id);
    state = AsyncData<String?>(id);
  }
}

/// The app-wide provider for the learner's anonymous profile id.
final AsyncNotifierProvider<LearnerProfileNotifier, String?>
    learnerProfileProvider =
    AsyncNotifierProvider<LearnerProfileNotifier, String?>(
  LearnerProfileNotifier.new,
);
