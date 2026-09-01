import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../data/local/settings_store.dart';

/// Exposes the learner's TTS ("read questions aloud") opt-in preference
/// app-wide, mirroring `licence_code_provider.dart`'s [LicenceCodeNotifier]
/// shape exactly.
///
/// On first read, [build] checks local storage via [SettingsStore],
/// defaulting to `false` (off) per the spec's documented default.
///
/// This story only captures, persists, and exposes the preference -- there
/// is no Practice/Test screen yet with question/option text to read
/// (Epic 5/6). A future TTS reader only needs to watch this provider, not
/// build any settings UI of its own.
class TtsSettingsNotifier extends AsyncNotifier<bool> {
  TtsSettingsNotifier({SettingsStore? store})
      : _store = store ?? const SettingsStore();

  final SettingsStore _store;

  @override
  Future<bool> build() => _store.readTtsEnabled();

  /// Persists [enabled] as the learner's TTS opt-in preference and updates
  /// provider state so every consumer sees it immediately.
  Future<void> setTtsEnabled(bool enabled) async {
    await _store.writeTtsEnabled(enabled);
    state = AsyncData<bool>(enabled);
  }
}

/// The app-wide provider for the learner's TTS opt-in preference.
final AsyncNotifierProvider<TtsSettingsNotifier, bool> ttsSettingsProvider =
    AsyncNotifierProvider<TtsSettingsNotifier, bool>(
  TtsSettingsNotifier.new,
);
