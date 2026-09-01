---
title: 'Restore or transfer a profile by QR or manual paste'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: '679f416f4ac55454db17f7547d852b36cbc42cb1'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** A learner's profile is nothing but a locally-persisted UUID (Story 4.3) -- reinstalling the app or switching devices currently means losing it forever, with no way to view, share, or restore it.

**Approach:** The Profile tab gains real content: the UUID shown and copyable, plus a QR code encoding it. The first-run screen gains a second path alongside `Start learning`: `Restore profile`, offering both QR-scan and manual-paste entry. **Note on a genuine conflict in the source material:** `epics.md`'s own story title and Acceptance Criteria explicitly require *both* "scanning a QR code **or** pasting my UUID" -- but `EXPERIENCE.md`'s "Identity & Profile" section states "Transfer/linking is by QR code only... no manual paste." These directly contradict each other. This spec follows `epics.md`'s explicit AC (the authoritative story requirement) and builds both paths -- QR is not weakened, manual paste is added alongside it, which strictly satisfies EXPERIENCE.md's QR requirement while also meeting the story's own literal AC.

## Boundaries & Constraints

**Always:**
- Add `qr_flutter` (QR generation) and `mobile_scanner` (QR scanning, camera access) packages.
- Profile tab (`lib/presentation/profile/profile_screen.dart`, replacing Story 4.2's placeholder) shows: the UUID as selectable/copyable text with a copy-to-clipboard action, the exact save-your-progress microcopy from EXPERIENCE.md ("To save your progress, copy this UUID to import your results in another app"), and a QR code widget encoding the raw UUID string.
- `StartLearningScreen` (Story 4.3) gains a secondary, clearly-less-prominent action below the primary `Start learning` button: `Restore profile` -- opens `lib/presentation/profile/restore_profile_screen.dart`, offering a QR-scan view (via `mobile_scanner`'s camera preview) AND a manual text-entry field for the UUID, both feeding the same validate-and-restore logic.
- Restore validation is entirely client-side format checking (the same UUID v4 regex already added to `LearnerProfileStore` in Story 4.3) -- there is no backend "does this profile exist" endpoint to check against (none was built; `LearnerProfile` is only ever implicitly upserted by a future `StartAttemptCommand` call). A well-formed-but-nonexistent UUID cannot be distinguished from a real one by this app; restoring simply persists whatever well-formed UUID the learner provides as their profile id, exactly as `Start learning` persists a freshly generated one.
- "Invalid... UUID" (matrix row) means fails the format check -- shown as a clear, non-technical message (e.g. "That doesn't look like a valid code. Please check and try again.") with nothing persisted and no state change. "Unknown" UUID (well-formed but never actually used against the backend) is NOT independently detectable client-side and is treated identically to any other well-formed UUID -- restoring proceeds; this is a client-side scoping limit, not a bug, and is called out in Design Notes/deferred-work rather than silently assumed.
- Both the QR-scan and manual-paste paths funnel through one shared validate-and-persist function so their behavior (including the error case) is identical regardless of entry method.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No backend changes -- no profile-existence-check endpoint is added; restore is a purely local operation from the app's perspective.
- No settings/theme/TTS content on the Profile screen -- that's Story 4.6.
- No licence-code display/change flow on the Profile screen -- that's Story 4.5.
- No camera-permission-flow polish beyond `mobile_scanner`'s own default prompt handling -- a full "camera permission denied, here's how to fix it in Settings" UX is not required by this story's AC.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| View Profile tab | Profile exists | UUID shown, copyable; QR code rendered encoding it | N/A |
| Copy UUID | Tap copy action | UUID copied to system clipboard | N/A |
| Restore via manual paste, valid format | Fresh install, well-formed UUID pasted | That UUID becomes the persisted profile id; app proceeds to `AppShell` | N/A |
| Restore via QR scan, valid payload | Fresh install, a QR encoding a well-formed UUID is scanned | Same as manual paste -- restored, proceeds to `AppShell` | N/A |
| Restore, invalid format | Malformed string (either entry method) | Rejected; nothing persisted | Clear, non-technical error message |

</frozen-after-approval>

## Code Map

- `src/k53_guru_app/pubspec.yaml` -- Modify. Add `qr_flutter`, `mobile_scanner`.
- `src/k53_guru_app/lib/presentation/profile/profile_screen.dart` -- New (replaces the Story 4.2 placeholder registered in `app_shell.dart`).
- `src/k53_guru_app/lib/presentation/profile/restore_profile_screen.dart` -- New.
- `src/k53_guru_app/lib/presentation/onboarding/start_learning_screen.dart` -- Modify. Add the `Restore profile` secondary action.
- `src/k53_guru_app/lib/presentation/shell/app_shell.dart` -- Modify. Wire `ProfileScreen` in place of `ProfilePlaceholderScreen`.
- `src/k53_guru_app/lib/presentation/profile/profile_restore_validator.dart` (or similar) -- New. The shared validate-and-persist function used by both entry methods, decoupled from the camera widget so it's independently unit-testable.
- `src/k53_guru_app/test/presentation/profile_screen_test.dart` -- New. UUID display/copy, QR code presence.
- `src/k53_guru_app/test/presentation/profile_restore_validator_test.dart` -- New. Covers the format-validation matrix rows directly, independent of the camera/scanner widget.
- `src/k53_guru_app/test/presentation/restore_profile_screen_test.dart` -- New. Manual-paste path end-to-end (the QR-scan camera path itself is not meaningfully unit-testable without a physical device/camera -- covered by the shared validator's own tests instead).
- Review fix (verification-gap + edge-case-hunter + blind-hunter, all independently converging): `_onBarcodeDetected`'s glue code between `MobileScanner`/the validator had zero coverage despite being testable without a camera (`BarcodeCapture`/`Barcode` are plain Dart data classes) -- added tests invoking `onDetect` directly with both a valid and a garbage payload; added an end-to-end test driving the real app router through the actual "Restore profile" button (previously only tested in an isolated harness, though blind-hunter independently confirmed via a scratch test it already worked correctly); strengthened the QR test to compare genuine rendered pixel data against an independently-constructed `QrPainter` (previously only checked an unrelated `semanticsLabel` string); made lowercase persistence explicit in `generateAndPersistProfileId()` rather than relying on the `uuid` package's internal behavior.

## Tasks & Acceptance

**Execution:**
- [x] `pubspec.yaml` -- add `qr_flutter`/`mobile_scanner`.
- [x] `profile_restore_validator.dart` -- create the shared validate-and-persist logic.
- [x] `profile_screen.dart` -- create Profile tab content (UUID, copy, QR).
- [x] `restore_profile_screen.dart` -- create QR-scan + manual-paste restore UI.
- [x] `start_learning_screen.dart` -- add the `Restore profile` secondary action.
- [x] `app_shell.dart` -- wire the real `ProfileScreen`.
- [x] Tests -- cover all 5 matrix rows.

**Acceptance Criteria:**
- Given my Profile screen, when I view it, then my UUID is shown and copyable, with the save-your-progress note, and a QR code encoding the UUID.
- Given a fresh install, when I scan my QR code or paste my UUID, then the same profile id is restored and the app proceeds as that learner.
- Given an invalid UUID, when I attempt to restore, then I get a clear, non-technical error and no profile is corrupted.

## Design Notes

"Unknown" UUID detection (a well-formed UUID that was never actually used to start an attempt against the backend) is out of this story's reach -- the backend has no profile-existence-check endpoint (Story 3.3 deliberately only upserts `LearnerProfile` on first attempt-start). This is logged to `deferred-work.md`: if a genuine "unknown UUID" error is ever required by product, it needs a new backend read endpoint, not just client-side format validation.

## Verification

**Commands:**
- `flutter analyze` (run from `src/k53_guru_app/`) -- expected: no errors.
- `flutter test` (run from `src/k53_guru_app/`) -- expected: all tests pass.
