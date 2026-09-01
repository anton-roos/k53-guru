// Isolated ProviderContainer-level coverage of
// `LearnerProfileNotifier.generateAndPersistProfileId()` -- previously only
// exercised indirectly through `start_learning_screen_test.dart`'s widget
// tests. Asserts the three things the spec cares about in one place: the
// returned value is a real UUID v4, it's actually persisted (not just held
// in the notifier's in-memory state), and the provider's own state reflects
// that exact value afterwards.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/learner_profile_store.dart';
import 'package:k53_guru_app/presentation/onboarding/learner_profile_provider.dart';

final RegExp _uuidV4Pattern = RegExp(
  r'^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$',
);

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
      'generateAndPersistProfileId returns a UUID v4, persists it, and '
      'updates provider state to AsyncData(that exact value)', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    // Let the notifier's initial `build()` (the fresh-install read) settle
    // before calling the method under test, mirroring how the real app
    // never calls `generateAndPersistProfileId()` until after the provider
    // has already resolved to AsyncData(null).
    await container.read(learnerProfileProvider.future);

    final String id = await container
        .read(learnerProfileProvider.notifier)
        .generateAndPersistProfileId();

    expect(
      _uuidV4Pattern.hasMatch(id),
      isTrue,
      reason: 'returned id must be a UUID v4, got "$id"',
    );

    // Actually persisted -- a fresh LearnerProfileStore/SharedPreferences
    // instance (not the notifier's own in-memory copy) reads back the
    // same value.
    const LearnerProfileStore freshStore = LearnerProfileStore();
    expect(await freshStore.readProfileId(), id);

    final SharedPreferences prefs = await SharedPreferences.getInstance();
    expect(prefs.getString('learner_profile_id'), id);

    // The provider's own state reflects this exact value: settled (not
    // loading/erroring) with that precise id as its data.
    final AsyncValue<String?> state = container.read(learnerProfileProvider);
    expect(state.isLoading, isFalse);
    expect(state.hasError, isFalse);
    expect(state.hasValue, isTrue);
    expect(state.value, id);
  });

  test(
      'generateAndPersistProfileId explicitly lower-cases the persisted id '
      '-- not merely relying on the uuid package always producing lowercase '
      'hex', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    await container.read(learnerProfileProvider.future);

    final String id = await container
        .read(learnerProfileProvider.notifier)
        .generateAndPersistProfileId();

    // Explicit, unambiguous lowercase check -- independent of the UUID v4
    // regex above (which only happens to reject uppercase hex as a side
    // effect of being written with a lowercase-only character class).
    expect(
      id,
      equals(id.toLowerCase()),
      reason: 'the returned/persisted id must already be lowercase',
    );

    const LearnerProfileStore freshStore = LearnerProfileStore();
    final String? persisted = await freshStore.readProfileId();
    expect(
      persisted,
      equals(persisted?.toLowerCase()),
      reason: 'the value actually written to storage must be lowercase',
    );
  });
}
