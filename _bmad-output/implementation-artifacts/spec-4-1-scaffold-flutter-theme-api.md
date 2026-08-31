---
title: 'Scaffold the Flutter app with the design-token theme and API repository'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'b508173f198e5e161940c2f4496ce943291bc92d'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/DESIGN.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `src/k53_guru_app/` is a bare, unmodified `flutter create` scaffold -- no layering, no theme, no way to reach the `/api/v1` backend Epic 3 just finished building.

**Approach:** Establish the four-layer architecture (presentation -> state (Riverpod) -> data (repository + typed API client + DTOs) -> domain) and a complete theme implementing every token in `DESIGN.md`'s frontmatter (light/dark palettes, the 6-step type scale, spacing/radius scale, and the primary/disabled button + option-card component tokens). The data layer targets exactly the DTO shapes Epic 3 already ships (`AvailableSittingDto`, `AttemptDto`, `GradedAttemptResultDto`, `CheckAnswerResultDto`) so nothing here waits on future backend work. A single minimal proof screen (list of available sittings) demonstrates the full stack wired together -- not real UI, which is Epic 5/6's job.

## Boundaries & Constraints

**Always:**
- Add `flutter_riverpod` (state) and `http` (API calls) to `pubspec.yaml` -- no other new dependencies this story (no `dio`, no local-storage/QR/TTS packages -- those belong to later Epic 4 stories that actually need them).
- Folder structure: `lib/theme/` (design tokens), `lib/domain/` (plain Dart models -- `AvailableSitting`, `Attempt`, `AttemptQuestion`, `AttemptAnswerOption`, `GradedAttemptResult`, `CodeResult`, `SectionResult`, `CheckAnswerResult`, mirroring the backend DTOs' shapes exactly), `lib/data/api/` (a typed `K53ApiClient` wrapping every existing `/api/v1` endpoint: `GET /sittings`, `POST /attempts`, `GET /attempts/{id}`, `POST /attempts/{id}/submit`, `POST /attempts/{id}/check-answer`), `lib/data/repository/` (`AttemptsRepository`/`SittingsRepository` wrapping the API client, the ONLY layer widgets are allowed to depend on), `lib/presentation/` (the one proof screen + its Riverpod provider).
- JSON field names are camelCase, matching ASP.NET Core's default `System.Text.Json` serialization for controllers (e.g. `learnerProfileId`, `testId`, `correctAttemptAnswerOptionId`) -- Dart DTOs' `fromJson`/`toJson` must use camelCase keys, not a naming-convention mismatch.
- `lib/theme/app_theme.dart` implements every token in `DESIGN.md`'s frontmatter: `ColorScheme`-equivalent constants for both light and dark palettes (primary/primary-strong/accent/success/success-soft/danger/danger-soft/surface/card/ink/muted/line), a `TextTheme` with all 6 named styles at their exact size/weight/line-height (display 28/800, h2 22/800, question 20/700, body 17/400 -- the accessibility-mandated minimum, option 17/600, label 13/700), the spacing scale (4/8/12/16/20/24/32/48px) and radius scale (sm 8, md 14, lg 20, pill 999) as named constants, and `ThemeData`/`ThemeData.dark()` wiring the primary-button (56px height, md radius, 800 weight, theme-aware text colour -- white on light-mode primary, dark ink `#0b1220` on dark-mode primary for AA contrast per DESIGN.md's explicit callout) and disabled-button (line background, muted text) component styles.
- `K53ApiClient`'s base URL is a single, clearly-named constant (`ApiConfig.baseUrl`) -- not yet environment-switched (no dev/staging/prod split, no Android-emulator `10.0.2.2` handling) -- this story only needs ONE reachable target; environment configuration is deferred.
- The one proof screen (`lib/presentation/sittings/sittings_list_screen.dart` or similar) uses a Riverpod `FutureProvider`/`AsyncNotifier` to call `SittingsRepository.getAvailableSittings()` and render a plain, unstyled-beyond-theme list -- proving the layering (widget -> provider -> repository -> API client -> HTTP) without any HTTP call appearing in a widget's `build()` method anywhere.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No bottom-nav/tab shell (Story 4.2), no UUID/profile generation (Story 4.3), no QR (Story 4.4), no code-selection flow (Story 4.5), no settings screen (Story 4.6), no accessibility pass beyond what the theme itself provides for free (Story 4.7) -- this story is the architectural foundation only.
- No offline/sync/local-persistence logic -- `EXPERIENCE.md`'s "Persistence & Sync" section is explicitly out of scope for this session's Epic 4 stories (not one of the 7 ACs) and must not be built speculatively.
- No streaks/XP/Lottie/haptics/sound -- `EXPERIENCE.md`'s behavioral/reward specs belong to Epic 5 (Practice Experience), not requested this session.
- No changes to any backend (`src/K53Guru/`) file -- the API surface already exists and is correct; this story only consumes it.

## I/O & Edge-Case Matrix

<!-- No meaningful I/O scenarios beyond straightforward JSON (de)serialization -- covered by unit tests instead of a matrix. -->

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/pubspec.yaml` -- Modify. Add `flutter_riverpod`, `http`.
- `src/k53_guru_app/lib/theme/app_theme.dart` (+`app_colors.dart`/`app_typography.dart`/`app_spacing.dart` as needed for organization) -- New. Full `DESIGN.md` token implementation.
- `src/k53_guru_app/lib/domain/*.dart` -- New. Plain Dart models mirroring the backend DTOs.
- `src/k53_guru_app/lib/data/api/api_config.dart` / `k53_api_client.dart` -- New.
- `src/k53_guru_app/lib/data/repository/sittings_repository.dart` / `attempts_repository.dart` -- New.
- `src/k53_guru_app/lib/presentation/sittings/sittings_list_screen.dart` (+ its Riverpod provider) -- New.
- `src/k53_guru_app/lib/main.dart` -- Modify. Wrap in `ProviderScope`, apply `AppTheme.light()`/`AppTheme.dark()`, launch the proof screen.
- `src/k53_guru_app/test/theme/app_theme_test.dart` -- New. Verifies key token values (e.g. body text is exactly 17px, primary button height is 56px).
- `src/k53_guru_app/test/data/k53_api_client_test.dart` -- New. Verifies JSON (de)serialization round-trips for each DTO, using a mocked `http.Client`.
- Review fix (blind-hunter, critical): `AttemptQuestion.code` used a single-value `LicenceCode` parser with a doc comment claiming "always a single code, never a combo" -- factually wrong. The backend's `AttemptQuestion.Code` carries the FULL combination value for shared Rules/Signs questions in any combination sitting (Story 3.4); calling `startAttempt`/`getAttempt` against a combination sitting would have thrown an uncaught `FormatException`. Changed to `List<LicenceCode>` parsed via the existing `parseLicenceCodes` helper, matching `Attempt.code`/`AvailableSitting.codes`'s already-correct pattern. Added a regression test using a genuine combination-sitting payload.
- Review fix (verification-gap, converging findings): added an end-to-end wiring test overriding only `k53ApiClientProvider` (not `availableSittingsProvider` directly) to prove the full widget -> provider -> repository -> client -> HTTP chain is actually connected, not just each layer in isolation; added tests against the ACTUAL assembled `ThemeData.textTheme`/`colorScheme` (previous tests only checked raw source constants); added `toJson()` round-trip tests for all 8 response domain models (previously dead, unverified code); added coverage for `AttemptsRepository` (previously completely untested).

## Tasks & Acceptance

**Execution:**
- [x] `pubspec.yaml` -- add `flutter_riverpod`/`http`.
- [x] `lib/theme/*.dart` -- implement the full token set.
- [x] `lib/domain/*.dart` -- create the plain Dart models.
- [x] `lib/data/api/*.dart` -- create the typed API client.
- [x] `lib/data/repository/*.dart` -- create the repositories.
- [x] `lib/presentation/sittings/*.dart` -- create the one proof screen + provider.
- [x] `lib/main.dart` -- wire `ProviderScope`, theme, launch screen.
- [x] Tests -- cover theme tokens and API client (de)serialization.

**Acceptance Criteria:**
- Given the Flutter app scaffold, when it builds, then it implements the layered structure and widgets never call HTTP directly.
- Given the design tokens from DESIGN.md, when the theme is applied, then light and dark palettes, the typography scale, spacing scale, radii, and component tokens are available as a shared theme.
- Given the API contract, when the repository calls the backend, then it targets the versioned `/api/v1` DTOs and surfaces typed models to the app.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.

**Manual checks (if no CLI):**
- Run the app against a live backend instance, confirm the proof screen lists published sittings; toggle system dark mode and confirm the theme switches.
