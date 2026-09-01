---
title: 'Provide theme and TTS settings'
type: 'feature'
created: '2026-09-01'
status: 'done'
baseline_commit: '142ace4'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The app currently follows the OS light/dark setting (`ThemeMode.system`, a stopgap `main.dart` has flagged since Story 4.1) and has no TTS opt-in anywhere, even though DESIGN.md and EXPERIENCE.md both call dark mode "a profile setting, not a system toggle," and the accessibility floor calls for an opt-in TTS reader.

**Approach:** The Profile tab (which EXPERIENCE.md already names as the home for "settings: theme, TTS") gains a Settings section: a Light/Dark choice (no "follow system" option — the AC is explicit that this is a profile setting) and a TTS opt-in switch. Both persist locally and are exposed app-wide via providers, mirroring the exact `LearnerProfileStore`/`licenceCodeProvider` shape Story 4.5 established. Theme selection visibly and immediately re-themes the whole app (`main.dart`'s `MaterialApp.themeMode` starts reading this provider instead of hardcoding `ThemeMode.system`). TTS has nothing to read aloud yet -- no Practice/Test screen exists (Epic 5/6) -- so "takes effect immediately" for TTS means the persisted preference is immediately available to any consumer, not an audible effect; this mirrors Story 4.5's Recalibrate/Start-fresh precedent of building the real interaction faithfully while being honest that the downstream effect doesn't exist yet.

## Boundaries & Constraints

**Always:**
- New `lib/data/local/settings_store.dart` (a new file, not a further extension of `LearnerProfileStore` -- theme/TTS are app preferences, not identity, and EXPERIENCE.md itself separates "Profile" identity content from "Settings" as a distinct list item) with `readThemeMode()`/`writeThemeMode(ThemeMode)` and `readTtsEnabled()`/`writeTtsEnabled(bool)`, same `SharedPreferences`-backed pattern and graceful-degradation-on-failure behavior as `LearnerProfileStore` (a read failure or absent value means the documented default, never a crash): light theme, TTS off.
- Only `ThemeMode.light`/`ThemeMode.dark` are ever persisted or offered in the UI -- `ThemeMode.system` is never written by this story's UI (per DESIGN.md/EXPERIENCE.md: "not a system toggle"). `main.dart`'s `MaterialApp.themeMode` reads the persisted value (falling back to `ThemeMode.light` while it loads, matching the documented default) instead of hardcoding `ThemeMode.system`.
- Two new Riverpod providers, `lib/presentation/settings/theme_mode_provider.dart` and `lib/presentation/settings/tts_settings_provider.dart`, each an `AsyncNotifier` mirroring `LicenceCodeNotifier`'s exact shape (`build()` reads the store; a setter method writes through the store then updates `state` synchronously so every watcher, including `main.dart`, sees the change immediately).
- Profile tab (`profile_screen.dart`) gains a "Settings" section below the existing `Change code` row: a two-option Light/Dark control (e.g. `SegmentedButton<ThemeMode>`) and a `SwitchListTile` for "Read questions aloud" (TTS opt-in), each wired straight to its provider's setter on change -- no separate "Save" step, matching this app's established "the action IS the save" pattern (licence code selection, change-code flow).
- The TTS toggle's label and any nearby copy are honest that it is a preference for future screens to use (no question/option content exists yet to read) -- do not imply a working reader exists today.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No actual text-to-speech engine/package (e.g. `flutter_tts`) and no speech playback of any kind -- there is no Practice/Test screen yet with question/option text to read (Epic 5/6, not built this session). This story only captures, persists, and exposes the opt-in preference.
- No "follow system" theme option in the UI -- contradicts the explicit "not a system toggle" requirement.
- No changes to `lib/data/api/`, the backend, or the existing `Change code` / licence-code flow beyond adding the new Settings section alongside it.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh install, no settings persisted | First app launch | Theme is Light; TTS toggle is off | N/A |
| Change theme | Tap "Dark" in Settings | Whole app re-themes immediately (`MaterialApp` rebuilds with `AppTheme.dark()`); persisted for next launch | N/A |
| Toggle TTS on | Tap the TTS switch | Switch shows on; persisted for next launch; no audible effect yet (nothing to read) | N/A |
| Relaunch after changing settings | App restarted | Previously chosen theme and TTS state are restored exactly | N/A |
| Local storage read fails for settings | `SharedPreferences` throws reading theme or TTS | Falls back to the documented default (Light theme, TTS off) -- never a crash | N/A |

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/lib/data/local/settings_store.dart` -- New. `readThemeMode`/`writeThemeMode`/`readTtsEnabled`/`writeTtsEnabled`.
- `src/k53_guru_app/lib/presentation/settings/theme_mode_provider.dart` -- New. `AsyncNotifier<ThemeMode>` mirroring `LicenceCodeNotifier`.
- `src/k53_guru_app/lib/presentation/settings/tts_settings_provider.dart` -- New. `AsyncNotifier<bool>` mirroring `LicenceCodeNotifier`.
- `src/k53_guru_app/lib/main.dart` -- Modify. `MaterialApp.themeMode` reads `themeModeProvider` (fallback `ThemeMode.light`) instead of hardcoding `ThemeMode.system`.
- `src/k53_guru_app/lib/presentation/profile/profile_screen.dart` -- Modify. Add the Settings section (theme control + TTS switch) below `Change code`.
- `src/k53_guru_app/test/data/settings_store_test.dart` -- New. Covers all matrix rows for both settings.
- `src/k53_guru_app/test/presentation/theme_mode_provider_test.dart` -- New.
- `src/k53_guru_app/test/presentation/tts_settings_provider_test.dart` -- New.
- `src/k53_guru_app/test/presentation/profile_screen_test.dart` -- Modify. Add Settings-section coverage (theme switch re-themes, TTS toggle persists).
- `src/k53_guru_app/test/main_test.dart` -- Modify. Add coverage that `themeMode` follows the provider, not `ThemeMode.system`.
- Review fix (blind-hunter + verification-gap, both independently converging on the top finding): added a genuine end-to-end test in `main_test.dart` mounting the real `K53GuruApp`, navigating to the real Profile tab, tapping the real "Dark" `SegmentedButton` segment, and asserting the root `MaterialApp.themeMode` actually changes -- previously the tap-side and root-`MaterialApp`-side of this wiring were only proven in isolation on two different widget trees. Confirmed the real wiring already worked correctly; no production bug found. Also: strengthened the "no System option" assertion to check the segmented button's `segments` list is exactly `{light, dark}` (the prior `containsAllInOrder` check didn't rule out an added third segment); added coverage for the previously-untested `'system'`-value handling at both the store layer (round-trips faithfully, does not collapse -- that's the provider's job) and the provider layer (`build()` collapses a persisted `'system'` to `ThemeMode.light`); added coverage for `profile_screen.dart`'s own local loading-state fallback (Settings section shows Light/off defaults during actual `AsyncLoading`, not just after `pumpAndSettle`).

## Tasks & Acceptance

**Execution:**
- [x] `settings_store.dart` -- create with theme/TTS read/write.
- [x] `theme_mode_provider.dart` / `tts_settings_provider.dart` -- create both providers.
- [x] `main.dart` -- wire `themeMode` to `themeModeProvider`.
- [x] `profile_screen.dart` -- add the Settings section UI.
- [x] Tests -- cover all 5 matrix rows plus provider/router wiring.

**Acceptance Criteria:**
- Given Settings, when I open it, then I can set theme (light default; dark mode as a profile setting, not a system toggle) and toggle TTS opt-in.
- Given I change a setting, when it is saved, then it persists to my profile and takes effect immediately.

## Design Notes

TTS opt-in has no audible effect in this story -- there is no question/option content or reading screen yet (Epic 5/6). The preference is captured and persisted faithfully so the future TTS reader only needs to check `ttsSettingsProvider`, not build any settings UI of its own. This is the same honesty-about-scope pattern Story 4.5 used for Recalibrate/Start-fresh.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
