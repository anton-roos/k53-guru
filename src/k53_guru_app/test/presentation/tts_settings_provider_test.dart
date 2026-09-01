// Isolated ProviderContainer-level coverage of `TtsSettingsNotifier`,
// mirroring `learner_profile_provider_test.dart`'s shape: default state on a
// fresh install, and that the setter persists through the real
// `SettingsStore`/`SharedPreferences` (not just held in memory) and updates
// provider state synchronously.

import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/settings_store.dart';
import 'package:k53_guru_app/presentation/settings/tts_settings_provider.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test('Fresh install (nothing persisted) -> build() resolves to false',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    final bool enabled = await container.read(ttsSettingsProvider.future);

    expect(enabled, isFalse);
  });

  test('A previously-persisted true value is restored on build()', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'settings_tts_enabled': true,
    });

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    final bool enabled = await container.read(ttsSettingsProvider.future);

    expect(enabled, isTrue);
  });

  test(
      'setTtsEnabled persists the new value and updates provider state to '
      'AsyncData(that exact value) synchronously', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    await container.read(ttsSettingsProvider.future);

    await container.read(ttsSettingsProvider.notifier).setTtsEnabled(true);

    final AsyncValue<bool> state = container.read(ttsSettingsProvider);
    expect(state.hasValue, isTrue);
    expect(state.value, isTrue);

    const SettingsStore freshStore = SettingsStore();
    expect(await freshStore.readTtsEnabled(), isTrue);
  });

  test(
      'setTtsEnabled(false) after a persisted true value replaces the '
      'previous value', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'settings_tts_enabled': true,
    });

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);
    await container.read(ttsSettingsProvider.future);

    await container.read(ttsSettingsProvider.notifier).setTtsEnabled(false);

    expect(container.read(ttsSettingsProvider).value, isFalse);
    const SettingsStore freshStore = SettingsStore();
    expect(await freshStore.readTtsEnabled(), isFalse);
  });
}
