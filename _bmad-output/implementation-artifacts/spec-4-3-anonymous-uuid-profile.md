---
title: "Generate an anonymous UUID profile silently on first run"
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'c2f2405726430135800e27f9c68af704c46c9a14'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md'
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No learner identity exists in the app at all -- every screen (Story 4.2's shell) is reachable with nothing behind it, and the API client (Story 4.1) has no UUID to actually send.

**Approach:** A `Start learning` first-run gate generates a UUID entirely client-side (matching the backend's already-shipped design: `StartAttemptCommand` upserts `LearnerProfile` on its own first call -- there is no dedicated "create profile" endpoint, by Story 3.3's deliberate choice, so nothing here waits on new backend work) and persists it locally. On every subsequent launch, the persisted UUID is found and the app goes straight to the main shell -- no repeat friction. The UUID becomes available app-wide via a Riverpod provider so any future screen that calls the API (Epic 5/6) can supply it as the opaque profile key.

## Boundaries & Constraints

**Always:**
- Add `uuid` (v4 generation) and `shared_preferences` (local persistence) packages -- no other new dependencies this story.
- `lib/data/local/learner_profile_store.dart` -- wraps `SharedPreferences`, exposing `Future<String?> readProfileId()` and `Future<void> writeProfileId(String id)`. A UUID v4 string (`xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx`) is format-compatible with the backend's `Guid`-typed `LearnerProfileId` parameters with no conversion needed.
- `lib/presentation/onboarding/learner_profile_provider.dart` (or similar) -- a Riverpod provider that, on first read, checks local storage for an existing profile id; if absent, this IS the first run (the id is NOT generated here -- see below), if present, exposes it immediately.
- `lib/presentation/onboarding/start_learning_screen.dart` -- the first-run gate: a single centered `Start learning` button (EXPERIENCE.md's explicit first-run pattern: "one `Start learning` CTA; nothing else to decide"), using the theme's `display`/`button-primary` styles. Tapping it generates a new UUID v4, persists it via `learner_profile_store.dart`, updates the provider state, and navigates to `AppShell` (Story 4.2). No account, password, form field, or any other input is ever collected -- the button press is the entire interaction.
- `main.dart`'s root widget becomes a small router: on startup, read the persisted profile id; if present, launch `AppShell` directly (returning learner, zero friction); if absent, launch `StartLearningScreen` (first run).
- The UUID is generated and persisted ENTIRELY client-side, with no network call -- this matches the backend's own design exactly (`StartAttemptCommand` is the profile's actual first server-side write, upserting on demand; that call doesn't happen until a later epic's screens actually start an attempt). This story does not add a dedicated "register profile" API call, because the backend deliberately doesn't have one to call.
- The persisted/generated profile id is exposed via a single Riverpod provider any future screen can read -- confirm (via a test) that `K53ApiClient` calls in later work would receive this exact value if wired up, even though no screen in THIS story actually calls an attempt-related endpoint yet.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No backend changes -- the existing implicit-upsert-on-first-attempt design (Story 3.3) is correct and is not being replaced by a dedicated endpoint.
- No account/password/email/form of any kind -- anonymous only, per the epic's explicit architecture ("no accounts, no PII").
- No QR code display/scanning (Story 4.4), no licence-code selection (Story 4.5) -- this story only generates and persists the UUID and gates first-run vs. returning-learner routing.
- No changes to `lib/theme/`, `lib/data/api/`, or `lib/data/repository/` beyond what's needed to expose the stored id via a provider.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Fresh install, first launch | No persisted profile id | `StartLearningScreen` renders | N/A |
| Tap `Start learning` | First run, button tapped | A new UUID v4 is generated, persisted, and the app navigates to `AppShell` | N/A |
| Second launch, same device | A profile id is already persisted | App launches directly into `AppShell` -- no `Start learning` screen shown again | N/A |
| Local storage read fails | `SharedPreferences` throws or returns corrupt data | Treated as "no profile id" (first-run path), never crashes | N/A |

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/pubspec.yaml` -- Modify. Add `uuid`, `shared_preferences`.
- `src/k53_guru_app/lib/data/local/learner_profile_store.dart` -- New.
- `src/k53_guru_app/lib/presentation/onboarding/learner_profile_provider.dart` -- New.
- `src/k53_guru_app/lib/presentation/onboarding/start_learning_screen.dart` -- New.
- `src/k53_guru_app/lib/main.dart` -- Modify. Route to `StartLearningScreen` or `AppShell` based on persisted profile id.
- `src/k53_guru_app/test/data/learner_profile_store_test.dart` -- New. Verifies read/write round-trip and graceful handling of absent/corrupt data.
- `src/k53_guru_app/test/presentation/start_learning_screen_test.dart` -- New. Covers all 4 matrix rows.
- Review fix (verification-gap): `readProfileId()` only checked null/empty -- a malformed-but-non-empty persisted string (partial disk write, etc.) was returned as-is, contradicting the matrix's own "corrupt data -> treated as no profile id" intent. Added a UUID v4 format check.
- Review fix (blind-hunter, self-flagged by the implementer): the `Start learning` button had no guard against rapid double-tap -- confirmed harmless today (no backend call happens yet) but a real, reachable, untested gap. Added a local generating-state flag disabling the button after the first tap (deliberately not the shared provider's loading state, which would have swapped the whole screen out on every tap, not just double-taps).
- Review fix (verification-gap): added a router test exercising `K53GuruApp` directly across all four `learnerProfileProvider` states (loading, error, no-id, has-id), and an isolated unit test for `generateAndPersistProfileId()` independent of the widget layer -- both previously only exercised indirectly or not at all.

## Tasks & Acceptance

**Execution:**
- [x] `pubspec.yaml` -- add `uuid`/`shared_preferences`.
- [x] `learner_profile_store.dart` -- create local persistence wrapper.
- [x] `learner_profile_provider.dart` -- create the Riverpod provider.
- [x] `start_learning_screen.dart` -- create the first-run gate screen.
- [x] `main.dart` -- route based on persisted profile id.
- [x] Tests -- cover storage round-trip and all 4 matrix rows.

**Acceptance Criteria:**
- Given a fresh install, when I tap `Start learning`, then a UUID is generated silently client-side and persisted -- no account, password, or PII.
- Given the profile exists, when the app is relaunched, then the UUID is available app-wide as the opaque profile key with no repeat first-run friction.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
