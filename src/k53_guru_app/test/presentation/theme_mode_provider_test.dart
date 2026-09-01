// Isolated ProviderContainer-level coverage of `ThemeModeNotifier`, mirroring
// `learner_profile_provider_test.dart`'s shape: default state on a fresh
// install, and that the setter persists through the real `SettingsStore`/
// `SharedPreferences` (not just held in memory) and updates provider state
// synchronously.

import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/settings_store.dart';
import 'package:k53_guru_app/presentation/settings/theme_mode_provider.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  test(
      'Fresh install (nothing persisted) -> build() resolves to '
      'ThemeMode.light', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    final ThemeMode mode = await container.read(themeModeProvider.future);

    expect(mode, ThemeMode.light);
  });

  test(
      'A previously-persisted ThemeMode.dark is restored on build()',
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'settings_theme_mode': 'dark',
    });

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    final ThemeMode mode = await container.read(themeModeProvider.future);

    expect(mode, ThemeMode.dark);
  });

  test(
      'A persisted ThemeMode.system value (never written by this app\'s UI, '
      'but a recognised SettingsStore wire name) is collapsed to '
      'ThemeMode.light on build() -- the defensive branch documented on '
      'ThemeModeNotifier.build() ("theme is Light by default and only ever '
      "light/dark (never system...)\"), exercised here for the first time",
      () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'settings_theme_mode': 'system',
    });

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    final ThemeMode mode = await container.read(themeModeProvider.future);

    expect(mode, ThemeMode.light);
  });

  test(
      'setThemeMode persists the new value and updates provider state to '
      'AsyncData(that exact value) synchronously', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{});

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);

    // Let the initial build() (the fresh-install read) settle first,
    // mirroring how the real app never calls setThemeMode() before the
    // provider has already resolved.
    await container.read(themeModeProvider.future);

    await container.read(themeModeProvider.notifier).setThemeMode(
          ThemeMode.dark,
        );

    // Provider state reflects the new value immediately, synchronously
    // after the write completes -- no separate rebuild/read needed.
    final AsyncValue<ThemeMode> state = container.read(themeModeProvider);
    expect(state.hasValue, isTrue);
    expect(state.value, ThemeMode.dark);

    // Actually persisted -- a fresh SettingsStore/SharedPreferences
    // instance (not the notifier's own in-memory copy) reads back the
    // same value.
    const SettingsStore freshStore = SettingsStore();
    expect(await freshStore.readThemeMode(), ThemeMode.dark);
  });

  test(
      'setThemeMode(light) after a persisted dark mode replaces the '
      'previous value', () async {
    SharedPreferences.setMockInitialValues(<String, Object>{
      'settings_theme_mode': 'dark',
    });

    final ProviderContainer container = ProviderContainer();
    addTearDown(container.dispose);
    await container.read(themeModeProvider.future);

    await container.read(themeModeProvider.notifier).setThemeMode(
          ThemeMode.light,
        );

    expect(container.read(themeModeProvider).value, ThemeMode.light);
    const SettingsStore freshStore = SettingsStore();
    expect(await freshStore.readThemeMode(), ThemeMode.light);
  });
}
