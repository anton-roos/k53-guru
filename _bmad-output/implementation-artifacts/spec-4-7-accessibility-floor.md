---
title: 'Meet the accessibility floor'
type: 'feature'
created: '2026-09-01'
status: 'done'
baseline_commit: '22af535'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/DESIGN.md'
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/review-accessibility.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Epic 4's screens (onboarding, restore, licence-code selection, Profile incl. Settings, the three-tab shell, the sittings list) were each built with accessibility notes along the way (QR `semanticsLabel`, 17px type floor, 48px tap-target token), but nothing has audited them together against the stated floor, and the pre-build accessibility review (`review-accessibility.md`) flagged concrete, still-open risks: no confirmed dynamic-type support at 200%, and no confirmed 48px floor on every button variant (only `ElevatedButton` is themed today -- `OutlinedButton`/`TextButton` use Material's un-themed defaults, which are smaller).

**Approach:** Audit every screen that exists today and close any gap against the AC's stated floor (17px body text, 48px tap targets, no colour-only or gesture-only interaction, Semantics labels, sane focus order). Fix gaps at the theme level where possible (one themed `minimumSize` fixes every current and future button, rather than patching call sites one by one). Add a dedicated accessibility test suite that pins these properties down so a future screen can't silently regress them.

**Honest scope boundary:** The AC's screen-reader clause also names "selection/correctness/results are announced via live regions" -- and the accessibility review separately flags colour-independent correctness feedback (item 3), reduced-motion handling for celebratory animation (item 7), and a timing accommodation for the Test-mode countdown (item 8). None of that UI exists yet -- there is no Practice/Test/correctness-feedback screen in this session's scope (Epic 5/6). Building placeholder live-region/motion/timer-accommodation code with nothing real to announce, animate, or time would be dishonest scaffolding, not accessibility work. This story audits and fixes the floor for every screen that exists today and documents the remaining items as Epic 5/6's job in Design Notes, matching the honesty-about-scope precedent Stories 4.5/4.6 already established for Recalibrate/Start-fresh and TTS.

## Boundaries & Constraints

**Always:**
- Theme every button variant to meet the 48px tap-target floor, not just `ElevatedButton` (already themed in `app_theme.dart`): add `outlinedButtonTheme`/`textButtonTheme` with a themed `minimumSize` so `OutlinedButton`/`TextButton` everywhere -- current call sites (`Copy UUID`, `Restore profile`, the `Recalibrate`/`Start fresh` dialog actions) and any future one -- meet `AppSpacing.minTapTarget` without a per-call-site override.
- Audit every currently-existing screen's interactive controls (buttons, `ListTile`/`SwitchListTile` rows, `SegmentedButton`, the licence-code option cards' `InkWell`, `NavigationBar` destinations) against the 48px floor; fix any that the theme-level change doesn't already cover.
- Audit every currently-existing screen for dynamic-type support: none may hardcode `TextScaler.noScaling`/a fixed `textScaleFactor`; every screen must render without clipping, overflow, or a thrown layout exception at a 200% linear text scale (`MediaQuery.textScaler`). Fix any concrete clipping found (e.g. give a `Column` more room to grow, avoid a fixed-height container around scalable text).
- Audit every currently-existing screen for Semantics coverage: every interactive control has an accessible label (a visible `Text` label already satisfies this for most current controls; anything icon-only would need an explicit label -- confirm none currently exists, or add one if found), decorative icons are not separately announced, and focus/traversal order matches the visual top-to-bottom layout.
- Confirm (audit, not necessarily code change) that no interaction on any currently-existing screen depends on colour alone, and that no path is gesture-only (tap is the only required gesture) -- both already hold per the interaction patterns used so far (no colour-coded state exists yet; everything is a plain tap), but this story is what makes that confirmation explicit and test-backed rather than assumed.
- Confirm the dark-mode primary-button contrast fix from Story 4.1 (`AppTheme`'s theme-aware `onPrimary`) still holds -- no new work expected here, just verification that `app_theme_test.dart`'s existing coverage still passes.
- New accessibility-focused test suite (`test/accessibility/`) that pins down tap-target sizes, 200%-scale rendering, and Semantics-label presence across every current screen, so a future screen change that regresses any of these is caught.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No live-region announcement code for selection/correctness/results, no reduced-motion handling, no Test-mode timing accommodation -- none of the UI these would attach to exists yet (Epic 5/6). Documented as forward scope in Design Notes, not built as scaffolding with nothing real behind it.
- No new dependencies (no accessibility/animation/TTS package) -- this story audits and fixes Semantics/dynamic-type/tap-target properties of existing widgets using Flutter's own framework support.
- No changes to `lib/data/api/`, the backend, or any Epic 4 flow's actual behavior (only accessibility-attribute/sizing fixes and their tests).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Any current screen renders normally | Default text scale, default theme | Body text is never below 17px; every tap target (buttons, dialog actions, list rows, switch, segmented control, nav bar) is at least 48x48 | N/A |
| OS text scale set to 200% | `MediaQuery.textScaler = TextScaler.linear(2.0)` on any current screen | Renders without a thrown exception or clipped/overflowing content; scrollable containers absorb the extra height | N/A |
| Screen reader active | TalkBack/VoiceOver-style traversal of any current screen | Every interactive control exposes a non-empty accessible label; decorative icons are not separately announced; traversal order matches the visual top-to-bottom layout | N/A |
| Colour-only interaction audit | Every current screen | Confirmed: no affordance is conveyed by colour alone (none exists yet -- correctness/state colour-coding is Epic 5/6) | N/A |
| Gesture-only path audit | Every current screen | Confirmed: every action is a plain tap; no drag/swipe/long-press-only path exists | N/A |

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/lib/theme/app_theme.dart` -- Modify. Add `outlinedButtonTheme`/`textButtonTheme` with a themed `minimumSize` meeting `AppSpacing.minTapTarget`.
- `src/k53_guru_app/lib/presentation/profile/profile_screen.dart` -- Verify (modify only if a gap remains after the theme-level fix, e.g. the "Copy UUID" button or dialog actions).
- `src/k53_guru_app/lib/presentation/onboarding/start_learning_screen.dart` -- Verify (modify only if a gap remains, e.g. the "Restore profile" secondary action).
- `src/k53_guru_app/lib/presentation/onboarding/licence_code_selection_screen.dart` -- Verify the option cards' tap target and Semantics; fix only if a gap is found.
- `src/k53_guru_app/lib/presentation/shell/app_shell.dart` -- Verify `NavigationBar` destinations meet the floor; fix only if a gap is found.
- `src/k53_guru_app/test/accessibility/dynamic_type_test.dart` -- New. Pumps every current screen at a 200% linear text scale; asserts no exception/overflow.
- `src/k53_guru_app/test/accessibility/tap_target_test.dart` -- New. Asserts every identified interactive control's rendered size is at least 48x48.
- `src/k53_guru_app/test/accessibility/semantics_audit_test.dart` -- New. Asserts every interactive control on every current screen exposes a non-empty Semantics label and that focus/traversal order is sane.
- Review fix (3-layer: blind-hunter + edge-case-hunter + verification-gap, two findings independently converged by two reviewers each): `start_learning_screen.dart` had the identical un-scrollable-Column overflow bug class this story had already fixed in `licence_code_selection_screen.dart` -- both reviewers reproduced a real `RenderFlex` overflow at 200% text scale on a short/narrow viewport (masked by `flutter_test`'s generous default 800x600 surface); fixed with the same `LayoutBuilder`/`SingleChildScrollView`/`ConstrainedBox` pattern, plus a regression test using a realistic small viewport. The `AppShell`/`NavigationBar` tap-target test was tautological (measured the bar's fixed outer height via an `Expanded` ancestor, not the real tappable region) -- replaced with a semantics-rect-based assertion, verified to actually fail against a deliberately shrunk nav bar before being reverted. Also: strengthened two dynamic-type tests (`AppShell` tab switching, the `Recalibrate`/`Start fresh` dialog) to assert label text stays findable post-scale, not just that no exception was thrown; added a narrow-viewport regression test for the Settings `SegmentedButton` per an edge-case-hunter risk analysis (ran clean -- confirmed a false alarm, kept as a permanent guard); corrected `tap_target_test.dart`'s docstring, which had overstated what the per-screen "real button" tests protect against (Flutter's own `_InputPadding` already guarantees the floor for standard buttons regardless of theme -- the isolated theme-property tests are what actually guard the app's own `minimumSize` configuration).

## Tasks & Acceptance

**Execution:**
- [x] `app_theme.dart` -- theme `OutlinedButton`/`TextButton` minimum size to the 48px floor.
- [x] Audit + fix (if needed) remaining tap targets: Copy UUID, Restore profile, dialog actions, licence-code option cards, nav bar.
- [x] Audit + fix (if needed) dynamic-type support at 200% across all current screens.
- [x] Audit + fix (if needed) Semantics coverage and focus order across all current screens.
- [x] Confirm colour-independence and no-gesture-only-path hold (documented, no code change expected).
- [x] Tests -- new `test/accessibility/` suite covering the I/O matrix.

**Acceptance Criteria:**
- Given any screen, when it renders, then body text is never below 17px, tap targets are ≥ 48px, and no interaction depends on colour alone or a gesture-only path.
- Given a screen reader is active, when I navigate, then every option has a Flutter `Semantics` label, selection/correctness/results are announced via live regions (separate from the opt-in TTS reader), and focus order is sane.

## Design Notes

The AC's "selection/correctness/results are announced via live regions" clause, and the accessibility review's colour-independence (item 3), reduced-motion (item 7), and timed-content (item 8) findings, all apply to Practice/Test/correctness-feedback UI that is Epic 5/6's job to build, not Epic 4's -- there is nothing in this session's scope for a live region to announce, an animation to make motion-aware, or a timer to make an accommodation for. This story closes every part of the floor that today's built screens can actually be held to, and leaves the rest as an explicit, documented dependency for whichever Epic 5/6 story first builds correctness feedback and the Test-mode timer.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
