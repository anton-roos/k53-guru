---
title: 'Choose licence code once, with a change-code flow'
type: 'feature'
created: '2026-09-01'
status: 'done'
baseline_commit: '1886bb5f6e5f4fdbaa283cae6181f3641d2d474c'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A profile (Story 4.3/4.4) has a UUID but no licence code -- nothing exists yet to record which of Code 1/2/3 a learner is studying for, and content filtering (Epic 5/6, not built) will need it.

**Approach:** First-run flow gains a mandatory step after the UUID is established (freshly generated OR restored): pick Code 1, 2, or 3. This choice is persisted locally, becomes a third router state (`main.dart` now distinguishes "no profile" / "profile, no code chosen yet" / "profile and code both set"), and is exposed app-wide via a provider for future content-filtering screens to read. The Profile tab gains a `Change code` action offering the AC's explicit `Recalibrate`/`Start fresh` choice.

## Boundaries & Constraints

**Always:**
- Reuse the existing `LicenceCode` enum from `lib/domain/licence_code.dart` (Story 4.1) -- a learner's own choice is always exactly one of `code1`/`code2`/`code3`, never a combination (the flags/`List<LicenceCode>` shape used elsewhere in the domain layer for Test/Question tagging does not apply to a single learner's own selection).
- `lib/data/local/learner_profile_store.dart` gains `readLicenceCode()`/`writeLicenceCode(LicenceCode)` alongside the existing profile-id methods -- same `SharedPreferences`-backed pattern, same graceful-degradation-on-failure behavior (a read failure or absent value means "no code chosen yet", never a crash).
- `main.dart`'s router gains a third state: no profile id -> `StartLearningScreen`; profile id present but no licence code -> `LicenceCodeSelectionScreen` (new); both present -> `AppShell`. This applies uniformly whether the profile id was freshly generated (`Start learning`) or restored (Story 4.4) -- a restored profile has no local licence-code preference either (licence code is a purely device-local setting, never synced to or read from the backend, since no such backend concept exists), so it also routes through code selection.
- `lib/presentation/onboarding/licence_code_selection_screen.dart` -- three large, equally-weighted tappable options (Code 1, Code 2, Code 3) using the theme's `option-card` tokens; tapping one persists it and proceeds (no separate "confirm" step needed -- picking IS confirming, matching the first-run screen's own "nothing else to decide" simplicity).
- Profile tab gains a `Change code` row/action showing the current code, opening a confirmation dialog with exactly two choices per the AC: `Recalibrate` (map existing progress to the new code) and `Start fresh` (reset progress) -- selecting either then re-presents `LicenceCodeSelectionScreen` to pick the new code, and persists it via `writeLicenceCode` on selection, replacing the old value.
- No progress/mastery data model exists yet (Epic 5/6, not built this session) -- `Recalibrate` and `Start fresh` are therefore both, honestly, a no-op on data that doesn't exist yet beyond persisting the new code; this is documented explicitly in code and Design Notes, not silently hidden, since the interaction/UI itself is still built faithfully per the AC.
- The persisted/selected licence code is exposed via a Riverpod provider (mirroring `learnerProfileProvider`'s shape) for any future screen to read.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No actual content-filtering logic anywhere -- no Practice/Test screen exists yet to filter (Epic 5/6). This story only captures and persists the choice.
- No real progress-recalibration/reset logic -- there is no progress data yet to recalibrate or reset.
- No changes to `lib/data/api/`, `lib/theme/`, or the backend.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh profile, no code chosen | Profile id exists, no licence code persisted | `LicenceCodeSelectionScreen` renders instead of `AppShell` | N/A |
| Pick a code, first time | Tap Code 2 | Persisted; app proceeds to `AppShell`; never re-prompted on future launches | N/A |
| Restored profile, no local code | A profile id was just restored (Story 4.4) on a fresh install | Also routes to `LicenceCodeSelectionScreen` (licence code is device-local, not restored) | N/A |
| Change code from Profile | Tap `Change code`, confirm `Recalibrate` or `Start fresh`, pick a new code | The new code replaces the old one; `Change code`'s displayed value updates | N/A |
| Local storage read fails for code | `SharedPreferences` throws reading the code | Treated as "no code chosen" (same graceful-degradation pattern as the profile id) | N/A |

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/lib/data/local/learner_profile_store.dart` -- Modify. Add `readLicenceCode()`/`writeLicenceCode(LicenceCode)`.
- `src/k53_guru_app/lib/presentation/onboarding/licence_code_provider.dart` -- New. Riverpod provider mirroring `learnerProfileProvider`'s shape.
- `src/k53_guru_app/lib/presentation/onboarding/licence_code_selection_screen.dart` -- New.
- `src/k53_guru_app/lib/main.dart` -- Modify. Add the third router state.
- `src/k53_guru_app/lib/presentation/profile/profile_screen.dart` -- Modify. Add the `Change code` action + confirmation dialog.
- `src/k53_guru_app/test/data/learner_profile_store_test.dart` -- Modify. Add licence-code read/write coverage.
- `src/k53_guru_app/test/presentation/licence_code_selection_screen_test.dart` -- New. Covers the matrix rows.
- `src/k53_guru_app/test/presentation/k53_guru_app_router_test.dart` -- Modify. Add the third router state to the existing state-matrix test.
- `src/k53_guru_app/test/presentation/profile_screen_test.dart` -- Modify. Add `Change code` flow coverage.
- Review fix (verification-gap, blind-hunter): `_ProfileContent` converted from `ConsumerWidget` to `ConsumerStatefulWidget` so `_onChangeCodeTapped` could gain a `_isChangingCode` double-tap guard (matching `StartLearningScreen._isGenerating`/`LicenceCodeSelectionScreen._isSelecting`/`RestoreProfileScreen._isProcessing`) -- the "Change code" row previously had no protection against a rapid double-tap stacking two `AlertDialog`s; added a test asserting exactly one dialog appears. Also added a router test covering the `AsyncData(profile)`/`AsyncLoading(licenceCode)` interleaving (profile resolved, licence code still loading), previously untested, confirming it correctly falls through to `LaunchingScreen` rather than crashing or racing ahead. The pre-existing "storage read fails for licence code" test's inherited `SharedPreferences` static-completer-cache fragility (same root cause as Story 4.3's equivalent test) was deferred, not patched -- see `deferred-work.md`.

## Tasks & Acceptance

**Execution:**
- [x] `learner_profile_store.dart` -- add licence-code read/write.
- [x] `licence_code_provider.dart` -- create the provider.
- [x] `licence_code_selection_screen.dart` -- create the selection UI.
- [x] `main.dart` -- add the third router state.
- [x] `profile_screen.dart` -- add `Change code` + confirmation dialog.
- [x] Tests -- cover all 5 matrix rows plus the router state.

**Acceptance Criteria:**
- Given first-run profile creation, when I choose Code 1, 2, or 3, then all content is silently filtered to that code (the choice is captured and available app-wide) and I am never asked to pick a category again.
- Given I change my code in Profile, when I confirm, then I am offered Recalibrate or Start fresh, and my choice is applied.

## Design Notes

`Recalibrate`/`Start fresh` are both currently no-ops beyond persisting the new code, since no progress/mastery data model exists yet in this session's scope (Epic 5/6). The interaction is built faithfully (the dialog, the two named choices, re-selecting the code) so a future story adding real progress data only needs to implement the two branches' actual data effects, not rebuild the flow.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
