// Covers spec-4-4's I/O & Edge-Case Matrix's format-validation rows directly
// against `ProfileRestoreValidator.validateAndPersist`, independent of
// either UI entry method (QR-scan camera widget or manual-paste text
// field) -- both funnel through this exact function, so its own behaviour
// is what the matrix actually describes. The QR-scan camera path itself is
// not meaningfully unit-testable without a physical device/camera; this
// file is where that path's real coverage lives instead (a scanned payload
// is just a `String` by the time it reaches this validator).
//
// Also documents the spec's Design Notes call-out: a well-formed UUID that
// was never actually used to start an attempt against the backend ("unknown"
// per epics.md's matrix) is NOT independently detectable client-side, and is
// treated identically to any other well-formed UUID -- restoring proceeds.
// There is no separate "unknown" test case because there is, by design, no
// separate code path: every well-formed UUID takes the same "restored"
// branch below.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';
import 'package:k53_guru_app/presentation/profile/profile_restore_validator.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const ProfileRestoreValidator validator = ProfileRestoreValidator();

  late ProviderContainer container;

  setUp(() async {
    SharedPreferences.setMockInitialValues(<String, Object>{});
    container = ProviderContainer();
    // Let the notifier's initial build() (fresh-install read: no id yet)
    // settle before any test calls the method under test -- mirrors how
    // the real restore screen is only ever shown once that has resolved.
    await container.read(learnerProfileProvider.future);
  });

  tearDown(() => container.dispose());

  test(
      'Restore, valid format (fresh install) -> restored, persisted, and '
      'provider state updated to that exact id', () async {
    const String id = '11111111-2222-4333-8444-555555555555';

    final ProfileRestoreResult result = await validator.validateAndPersist(
      id,
      container.read(learnerProfileProvider.notifier),
    );

    expect(result, ProfileRestoreResult.restored);

    // Actually persisted -- a fresh LearnerProfileStore/SharedPreferences
    // instance (not any in-memory copy) reads back the same value, exactly
    // as `Start learning` persisting a freshly generated id would.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), id);

    final AsyncValue<String?> state = container.read(learnerProfileProvider);
    expect(state.hasValue, isTrue);
    expect(state.value, id);
  });

  test(
      'Restore via QR-scan-shaped payload: upper-case UUID with surrounding '
      'whitespace (as a QR payload might carry a trailing newline) -> '
      'restored and normalized to the same lower-case shape '
      '`LearnerProfileStore` expects', () async {
    const String canonical = '99999999-8888-4777-8666-555555555555';
    final ProfileRestoreResult result = await validator.validateAndPersist(
      '  ${canonical.toUpperCase()}\n',
      container.read(learnerProfileProvider.notifier),
    );

    expect(result, ProfileRestoreResult.restored);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), canonical);

    final AsyncValue<String?> state = container.read(learnerProfileProvider);
    expect(state.value, canonical);
  });

  test(
      'Restore, invalid format (malformed string) -> rejected, nothing '
      'persisted, no state change', () async {
    final ProfileRestoreResult result = await validator.validateAndPersist(
      'not-a-real-uuid',
      container.read(learnerProfileProvider.notifier),
    );

    expect(result, ProfileRestoreResult.invalidFormat);

    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(
      await freshStore.readProfileId(),
      isNull,
      reason: 'a rejected restore attempt must never persist anything',
    );

    final AsyncValue<String?> state = container.read(learnerProfileProvider);
    expect(
      state.value,
      isNull,
      reason: 'a rejected restore attempt must never change provider state',
    );
  });

  test('Restore, empty input -> rejected as invalid format', () async {
    final ProfileRestoreResult result = await validator.validateAndPersist(
      '',
      container.read(learnerProfileProvider.notifier),
    );

    expect(result, ProfileRestoreResult.invalidFormat);
  });

  test(
      'Restore, well-formed UUID but wrong version nibble (not v4) -> '
      'rejected as invalid format', () async {
    // Version nibble is '1', not '4' -- same shape `LearnerProfileStore`'s
    // own reader rejects (Story 4.3), applied here to learner-supplied
    // input instead of previously-trusted local storage.
    final ProfileRestoreResult result = await validator.validateAndPersist(
      '11111111-2222-1333-8444-555555555555',
      container.read(learnerProfileProvider.notifier),
    );

    expect(result, ProfileRestoreResult.invalidFormat);
  });
}
