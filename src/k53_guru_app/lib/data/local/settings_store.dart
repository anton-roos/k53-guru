import 'package:flutter/material.dart';
import 'package:shared_preferences/shared_preferences.dart';

/// Wraps [SharedPreferences] to persist the learner's app-wide preferences
/// -- theme mode and TTS opt-in -- separately from [LearnerProfileStore]'s
/// identity data (profile id, licence code): these are app preferences, not
/// identity, per EXPERIENCE.md's own separation of "Profile" from
/// "Settings".
///
/// Mirrors [LearnerProfileStore]'s exact `SharedPreferences`-backed pattern
/// and graceful-degradation-on-failure behaviour: a read failure, an absent
/// value, or an unrecognised persisted string always falls back to the
/// documented default and never throws -- [ThemeMode.light] for theme,
/// `false` for TTS.
class SettingsStore {
  const SettingsStore();

  static const String _themeModeKey = 'settings_theme_mode';
  static const String _ttsEnabledKey = 'settings_tts_enabled';

  /// The default theme, used on a fresh install and whenever the persisted
  /// value can't be read or recognised.
  static const ThemeMode defaultThemeMode = ThemeMode.light;

  /// The default TTS opt-in state, used on a fresh install and whenever the
  /// persisted value can't be read.
  static const bool defaultTtsEnabled = false;

  /// The exact wire string for a [ThemeMode], matching what [writeThemeMode]
  /// persists. Only `light`/`dark` are ever written by this app's UI --
  /// `ThemeMode.system` is never offered as a choice (DESIGN.md/
  /// EXPERIENCE.md: theme is "a profile setting, not a system toggle") -- but
  /// it is still given a wire name here for completeness/robustness.
  static String _themeModeToJson(ThemeMode mode) {
    switch (mode) {
      case ThemeMode.light:
        return 'light';
      case ThemeMode.dark:
        return 'dark';
      case ThemeMode.system:
        return 'system';
    }
  }

  static ThemeMode? _themeModeFromJson(String value) {
    switch (value) {
      case 'light':
        return ThemeMode.light;
      case 'dark':
        return ThemeMode.dark;
      case 'system':
        return ThemeMode.system;
      default:
        return null;
    }
  }

  /// Reads the persisted theme mode.
  ///
  /// Returns [defaultThemeMode] when no value has ever been written (a
  /// fresh install), when local storage cannot be read at all, or when the
  /// persisted string doesn't match a recognised [ThemeMode] name (corrupted
  /// by a partial disk write). Per the spec's edge-case matrix, any storage
  /// failure or corrupt value is always treated as the documented default
  /// and must never crash the app.
  Future<ThemeMode> readThemeMode() async {
    try {
      final SharedPreferences prefs = await SharedPreferences.getInstance();
      final String? value = prefs.getString(_themeModeKey);
      if (value == null || value.isEmpty) {
        return defaultThemeMode;
      }
      return _themeModeFromJson(value) ?? defaultThemeMode;
    } catch (_) {
      return defaultThemeMode;
    }
  }

  /// Persists [mode] as the learner's chosen theme.
  Future<void> writeThemeMode(ThemeMode mode) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setString(_themeModeKey, _themeModeToJson(mode));
  }

  /// Reads the persisted TTS opt-in preference.
  ///
  /// Returns [defaultTtsEnabled] when no value has ever been written (a
  /// fresh install) or when local storage cannot be read at all. Per the
  /// spec's edge-case matrix, any storage failure is always treated as the
  /// documented default and must never crash the app.
  Future<bool> readTtsEnabled() async {
    try {
      final SharedPreferences prefs = await SharedPreferences.getInstance();
      return prefs.getBool(_ttsEnabledKey) ?? defaultTtsEnabled;
    } catch (_) {
      return defaultTtsEnabled;
    }
  }

  /// Persists [enabled] as the learner's TTS opt-in preference.
  Future<void> writeTtsEnabled(bool enabled) async {
    final SharedPreferences prefs = await SharedPreferences.getInstance();
    await prefs.setBool(_ttsEnabledKey, enabled);
  }
}
