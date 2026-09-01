import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/local/learner_profile_store.dart';
import '../../domain/licence_code.dart';

/// Exposes the learner's chosen [LicenceCode] app-wide, mirroring
/// `learner_profile_provider.dart`'s shape exactly.
///
/// On first read, [build] checks local storage via [LearnerProfileStore]:
/// - `null` means no code has ever been persisted -- either a genuinely
///   fresh profile or one just restored (Story 4.4), since licence code is
///   a purely device-local setting that is never restored/synced from the
///   backend. This IS the "profile exists, no code chosen yet" state the
///   root router in `main.dart` watches for.
/// - a non-null code means a returning learner who already picked one; it's
///   exposed immediately, no repeat prompt.
///
/// This is the single source of truth any future content-filtering screen
/// (Epic 5/6) reads to know which code the learner is studying for.
class LicenceCodeNotifier extends AsyncNotifier<LicenceCode?> {
  LicenceCodeNotifier({LearnerProfileStore? store})
      : _store = store ?? const LearnerProfileStore();

  final LearnerProfileStore _store;

  @override
  Future<LicenceCode?> build() => _store.readLicenceCode();

  /// Persists [code] as the learner's chosen licence code and updates
  /// provider state so every consumer -- including the root router in
  /// `main.dart` and the Profile tab's `Change code` row -- sees it
  /// immediately. Used both by first-run selection
  /// (`licence_code_selection_screen.dart`) and by the Profile tab's
  /// `Change code` flow, where it simply replaces the previous value.
  Future<void> selectLicenceCode(LicenceCode code) async {
    await _store.writeLicenceCode(code);
    state = AsyncData<LicenceCode?>(code);
  }
}

/// The app-wide provider for the learner's chosen licence code.
final AsyncNotifierProvider<LicenceCodeNotifier, LicenceCode?>
    licenceCodeProvider =
    AsyncNotifierProvider<LicenceCodeNotifier, LicenceCode?>(
  LicenceCodeNotifier.new,
);
