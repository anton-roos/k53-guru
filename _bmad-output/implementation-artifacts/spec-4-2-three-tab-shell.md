---
title: 'Provide the three-tab bottom-nav shell'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: '4d6b330548fdd37e6cf3b09ad4e87ee085667f04'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/DESIGN.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The app currently launches straight into Story 4.1's proof screen with no navigation shell at all -- no way to move between Practice, Test, and Profile, and no orientation lock.

**Approach:** Add a persistent bottom-navigation shell with exactly three destinations (Practice/Test/Profile), using an `IndexedStack` so each tab's subtree stays alive (and its scroll position/state survives) across tab switches rather than being rebuilt. Lock the app to portrait. Each tab's actual content is a placeholder for now (real screens are Epic 5/6's job) -- except Practice, which keeps Story 4.1's proof screen as its content, since that's the only real screen that exists yet and discarding it would be pure waste.

## Boundaries & Constraints

**Always:**
- Lock orientation to portrait only (`SystemChrome.setPreferredOrientations` with `portraitUp`/`portraitDown`) at app startup, before `runApp`.
- A single `AppShell` widget (`lib/presentation/shell/app_shell.dart`) becomes `main.dart`'s root widget (replacing direct launch of the Story 4.1 proof screen). Uses `Scaffold` + `NavigationBar` (Material 3's bottom nav widget, the current idiomatic choice over the older `BottomNavigationBar`) with exactly three `NavigationDestination`s in fixed order: Practice, Test, Profile.
- Tab content is an `IndexedStack` over three widgets, selected by the `NavigationBar`'s `selectedIndex` -- this is what "preserves its own state" means: all three subtrees are built once and kept alive (not lazily rebuilt on every switch), so scroll position/form state/in-flight requests in one tab survive switching away and back.
- Practice tab's content is Story 4.1's existing `SittingsListScreen` (moved/reused as-is, not rebuilt) -- Test and Profile tabs are simple placeholder screens (a centered `Text` naming the tab, using the theme's `display`/`body` styles) since their real content belongs to later epics not in this session's scope.
- Tap targets: `NavigationBar`'s default destination height already exceeds the 48px minimum -- verify this via a widget test measuring the actual rendered destination size, rather than assuming the Material default is sufficient.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No real Practice/Test/Profile screen content -- Epic 5/6 (and later Epic 4 stories for Profile) build those. This story only builds the shell and its navigation.
- No badge counts, no deep-linking, no more than three destinations.
- No changes to `lib/theme/`, `lib/data/`, or `lib/domain/` -- this story only adds presentation-layer navigation.

## I/O & Edge-Case Matrix

<!-- No meaningful I/O scenarios -- this is pure navigation/state-preservation UI, covered by widget tests instead of a matrix. -->

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/lib/presentation/shell/app_shell.dart` -- New. `NavigationBar` + `IndexedStack` shell.
- `src/k53_guru_app/lib/presentation/test_mode/test_mode_placeholder_screen.dart` / `src/k53_guru_app/lib/presentation/profile/profile_placeholder_screen.dart` -- New. Minimal placeholder screens.
- `src/k53_guru_app/lib/main.dart` -- Modify. Lock portrait orientation; launch `AppShell` instead of `SittingsListScreen` directly.
- `src/k53_guru_app/test/presentation/app_shell_test.dart` -- New. Verifies exactly 3 destinations in the correct order, tab switching updates the displayed content, previously-displayed tabs' widgets remain in the tree (`IndexedStack` state preservation), and destination tap-target height >= 48px.
- Review fix (verification-gap): the original "state preservation" test only proved static text remained findable in the tree after a tab switch -- since no tab had genuine mutable state, this couldn't actually distinguish real state survival from a fresh rebuild. Strengthened to override the Practice tab's sittings list with 20 items, scroll it to a non-zero offset, switch away and back, and assert the exact scroll position survived -- a genuine `IndexedStack` state-preservation proof.
- Review fix (verification-gap): the portrait orientation lock had zero automated coverage (only verified by reading `main.dart`). Added `test/main_test.dart`, mocking the platform channel and asserting `SystemChrome.setPreferredOrientations` was actually called with exactly `[portraitUp, portraitDown]`.

## Tasks & Acceptance

**Execution:**
- [x] `main.dart` -- lock portrait orientation.
- [x] `app_shell.dart` -- create the `NavigationBar`/`IndexedStack` shell.
- [x] `test_mode_placeholder_screen.dart`/`profile_placeholder_screen.dart` -- create placeholders.
- [x] `main.dart` -- launch `AppShell`.
- [x] `app_shell_test.dart` -- cover destination count/order, state preservation, tap-target size.

**Acceptance Criteria:**
- Given the app is open, when it renders, then a bottom navigation exposes exactly three destinations -- Practice (home), Test, and Profile -- portrait-only, single-column.
- Given I switch tabs, when navigation occurs, then each destination preserves its own state and tap targets are >= 48px.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
