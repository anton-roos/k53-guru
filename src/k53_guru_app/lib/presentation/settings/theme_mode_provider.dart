import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/local/settings_store.dart';

/// Exposes the learner's chosen [ThemeMode] app-wide, mirroring
/// `licence_code_provider.dart`'s [LicenceCodeNotifier] shape exactly.
///
/// On first read, [build] checks local storage via [SettingsStore],
/// defaulting to [ThemeMode.light] if the store somehow returns something
/// unexpected -- per the spec, theme is Light by default and only ever
/// `light`/`dark` (never `system`, which is not offered as a choice).
///
/// `main.dart`'s root [MaterialApp] watches this provider directly for its
/// `themeMode:`, so a call to [setThemeMode] re-themes the whole app
/// immediately.
class ThemeModeNotifier extends AsyncNotifier<ThemeMode> {
  ThemeModeNotifier({SettingsStore? store})
      : _store = store ?? const SettingsStore();

  final SettingsStore _store;

  @override
  Future<ThemeMode> build() async {
    final ThemeMode mode = await _store.readThemeMode();
    switch (mode) {
      case ThemeMode.light:
      case ThemeMode.dark:
        return mode;
      case ThemeMode.system:
        return ThemeMode.light;
    }
  }

  /// Persists [mode] as the learner's chosen theme and updates provider
  /// state so every consumer -- including the root [MaterialApp] in
  /// `main.dart` and the Profile tab's Settings section -- sees it
  /// immediately.
  Future<void> setThemeMode(ThemeMode mode) async {
    await _store.writeThemeMode(mode);
    state = AsyncData<ThemeMode>(mode);
  }
}

/// The app-wide provider for the learner's chosen theme mode.
final AsyncNotifierProvider<ThemeModeNotifier, ThemeMode> themeModeProvider =
    AsyncNotifierProvider<ThemeModeNotifier, ThemeMode>(
  ThemeModeNotifier.new,
);
