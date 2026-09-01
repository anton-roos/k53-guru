// Verifies `SettingsStore`'s read/write round-trip and the spec's I/O &
// Edge-Case Matrix rows:
//  - Fresh install, no settings persisted -> Theme is Light, TTS is off.
//  - Change theme / Toggle TTS -> persisted for next launch (round-trip).
//  - Relaunch after changing settings -> previously chosen values restored
//    exactly (also a round-trip, exercised per-value below).
//  - Local storage read fails for settings -> falls back to the documented
//    default (Light theme, TTS off), never a crash -- mirroring
//    `learner_profile_store_test.dart`'s established technique of seeding
//    `SharedPreferences.setMockInitialValues` with a wrong-typed/corrupt
//    value under the same key to simulate a read failure without a crash.

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

import 'package:k53_guru_app/data/local/settings_store.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();

  const SettingsStore store = SettingsStore();

  group('Fresh install, no settings persisted', () {
    test('readThemeMode returns ThemeMode.light', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      expect(await store.readThemeMode(), ThemeMode.light);
    });

    test('readTtsEnabled returns false', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      expect(await store.readTtsEnabled(), isFalse);
    });
  });

  group('Change theme / relaunch -> round-trips exactly', () {
    test('writeThemeMode(dark) then readThemeMode returns ThemeMode.dark',
        () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      await store.writeThemeMode(ThemeMode.dark);

      expect(await store.readThemeMode(), ThemeMode.dark);
    });

    test('writeThemeMode(light) then readThemeMode returns ThemeMode.light',
        () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      await store.writeThemeMode(ThemeMode.light);

      expect(await store.readThemeMode(), ThemeMode.light);
    });

    test(
        'writeThemeMode persists a plain string value usable across a '
        'simulated relaunch (a fresh SharedPreferences.getInstance() read '
        'sees the same value)', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});
      await store.writeThemeMode(ThemeMode.dark);

      final SharedPreferences prefs = await SharedPreferences.getInstance();
      expect(prefs.getString('settings_theme_mode'), 'dark');

      const SettingsStore freshStore = SettingsStore();
      expect(await freshStore.readThemeMode(), ThemeMode.dark);
    });
  });

  group('Toggle TTS / relaunch -> round-trips exactly', () {
    test('writeTtsEnabled(true) then readTtsEnabled returns true', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      await store.writeTtsEnabled(true);

      expect(await store.readTtsEnabled(), isTrue);
    });

    test('writeTtsEnabled(false) then readTtsEnabled returns false',
        () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});

      await store.writeTtsEnabled(false);

      expect(await store.readTtsEnabled(), isFalse);
    });

    test(
        'writeTtsEnabled persists a value usable across a simulated '
        'relaunch (a fresh SharedPreferences.getInstance() read sees the '
        'same value)', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{});
      await store.writeTtsEnabled(true);

      const SettingsStore freshStore = SettingsStore();
      expect(await freshStore.readTtsEnabled(), isTrue);
    });
  });

  group('Local storage read fails for settings -> falls back to defaults', () {
    test(
        'readThemeMode treats a value of the wrong type ("corrupt data") as '
        'the default (Light), and never throws', () async {
      // A non-String value under the same key simulates local storage
      // holding corrupt/incompatible data -- SharedPreferences.getString
      // throws a TypeError in that case, exercised by the store's own
      // catch clause.
      SharedPreferences.setMockInitialValues(<String, Object>{
        'settings_theme_mode': 12345,
      });

      expect(await store.readThemeMode(), ThemeMode.light);
    });

    test(
        'readThemeMode treats a non-empty, unrecognised string ("corrupt '
        'data" from a partial disk write) as the default (Light), and '
        'never throws', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'settings_theme_mode': 'NotARealThemeMode',
      });

      expect(await store.readThemeMode(), ThemeMode.light);
    });

    test('readThemeMode treats an empty string as the default (Light)',
        () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'settings_theme_mode': '',
      });

      expect(await store.readThemeMode(), ThemeMode.light);
    });

    test(
        'readTtsEnabled treats a value of the wrong type ("corrupt data") '
        'as the default (false), and never throws', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'settings_tts_enabled': 'not-a-bool',
      });

      expect(await store.readTtsEnabled(), isFalse);
    });
  });

  group(
      "Persisted 'system' value (a recognised SettingsStore wire name that "
      "this app's UI never itself writes -- Light/Dark are the only two "
      'choices offered)', () {
    test(
        'readThemeMode faithfully round-trips it as ThemeMode.system -- NOT '
        'the default -- since _themeModeFromJson(\'system\') returns '
        'ThemeMode.system (non-null), so it never falls through to '
        'defaultThemeMode. A value from an older/different build, or a '
        'future change elsewhere, must round-trip losslessly here. '
        'Collapsing ThemeMode.system down to Light is a separate, '
        'deliberate decision made one layer up by '
        'ThemeModeNotifier.build() (see theme_mode_provider_test.dart), '
        'not by this store.', () async {
      SharedPreferences.setMockInitialValues(<String, Object>{
        'settings_theme_mode': 'system',
      });

      expect(await store.readThemeMode(), ThemeMode.system);
    });
  });
}
