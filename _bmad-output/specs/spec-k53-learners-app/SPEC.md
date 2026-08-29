---
id: SPEC-k53-learners-app
companions: [test-structure.md]
sources: []
---

> **Canonical contract.** This SPEC and the files in `companions:` are the complete, preservation-validated contract for what to build, test, and validate. Source documents listed in frontmatter are for traceability — consult them only if you need narrative rationale or prose color this contract intentionally omits.

# K53 Learner's License Practice App

## Why

A vision to realize: give South African learner-driver candidates a practice tool that lets them rehearse the K53 learner's license theory test under conditions that mirror the official computerised exam as closely as possible, across all three licence codes — Code 1 (motorcycle), Code 2 (light motor vehicle), and Code 3 (heavy motor vehicle). Candidates today prepare against static booklets or apps that diverge from the real test's format, question mix, and scoring; a faithful simulation across single-code and combined-code tests raises the odds a learner walks into the testing centre already familiar with the experience. The system is two-sided: an admin-authored content backend and a mobile client that turns that content into randomised, exam-like attempts.

## Capabilities

- **CAP-1**
  - **intent:** An administrator can author multiple-choice tests through an Admin Panel that mimic the computerised K53 learner's test format.
  - **success:** An admin can create and publish a test whose questions, options, and correct answers are then retrievable by the API for delivery to the client.

- **CAP-2**
  - **intent:** An administrator can import existing tests into the system via CSV or JSON rather than hand-entering every question.
  - **success:** A well-formed CSV or JSON file is ingested into a stored test with its questions, answer options, and correct answers intact and delivered identically to a hand-authored test.

- **CAP-3**
  - **intent:** The system ships with a road-sign catalog pre-loaded from the official legislation, each sign carrying its respective legislative code.
  - **success:** Every official road sign is present in the catalog, each linked to its legislation code, and any sign can be referenced by a test question.

- **CAP-4**
  - **intent:** The .NET API serves tests to the client and randomises question selection and ordering per attempt.
  - **success:** Two attempts of the same underlying test produce differently ordered and/or differently selected question sets.

- **CAP-5**
  - **intent:** A learner using the Flutter app can take a test scoped to a single code, or a valid combination sitting (Code 1 + Code 2, or Code 1 + Code 3), mirroring the real CLLT combination booking.
  - **success:** The learner selects a single code or a valid combination and receives one sitting; a combination shares the Rules and Signs sections once and adds a separate vehicle-controls module per selected code.

- **CAP-6**
  - **intent:** The test-taking flow simulates the real computerised (CLLT) K53 test as closely as possible in structure, scoring, and per-code outcome.
  - **success:** A completed attempt returns per-section scores and, for a combination, an independent pass/fail per code (partial passes possible) that mirrors the official CLLT scoring rules.

- **CAP-7**
  - **intent:** The import path validates every row against the road-sign catalog and the question schema, refusing malformed content rather than storing it.
  - **success:** An import containing a row with an unresolved `sign_ref` or a missing required field is rejected, with the offending row identified, and no broken question is ever served to a learner.

- **CAP-8**
  - **intent:** A learner can choose Practice mode (learning-oriented) or Test mode (a faithful, cold simulation of the real exam) for the same underlying content.
  - **success:** Practice mode reveals correctness and explanation as the learner goes and permits retry, while Test mode withholds all feedback, times the sitting, and returns only a final per-section scored result.

- **CAP-9**
  - **intent:** A learner establishes and restores their identity via a self-custodied UUID — on launch, generate a new profile or link an existing one by pasting its UUID — with no account, password, or personal data.
  - **success:** Generating a profile issues a UUID; pasting that UUID into a freshly installed app restores the same profile and its data, and no PII is ever collected.

## Constraints

- The system spans a .NET backend (Admin Panel + API) and a Flutter mobile client; the app pulls tests from the .NET API rather than bundling its own content.
- Fidelity to the official computerised K53 learner's test — format, question mix, scoring, and pass/fail rules — is the primary quality bar every design decision resolves against.
- Road signs must be stored with their official legislative codes, not ad-hoc labels, so questions and study material reference the same authoritative source.
- Question selection and ordering are randomised per attempt; no attempt may serve a fixed, memorisable sequence. Randomisation is intra-section only: order is shuffled within each section while the section order stays fixed (Rules of the Road → Road Signs → Vehicle Controls, with code-specific blocks kept in order).
- The test-taking UI presents progress by section (e.g. "Section 2 of 4"), not a single flat question counter, so long combined papers stay legible.
- The faithful-simulation rules (timing, no hints/feedback until the end, section blocking, per-section scoring, intra-section randomisation) are the definition of Test mode. Practice mode may relax any of them to teach (immediate feedback, hints, retry, untimed); fidelity is fenced inside Test mode, and the surrounding app is designed for a nervous first-time human.
- Content must cover all three licence codes: Code 1 (motorcycle), Code 2 (light motor vehicle), and Code 3 (heavy motor vehicle). Supported sittings are a single code, or a valid combination — Code 1 + Code 2, or Code 1 + Code 3. Code 2 + Code 3 is not offered (a Code 3 learner's already covers light and heavy), and there is no all-three sitting.
- Questions reference road signs by their legislation code (`sign_ref`) resolved against the pre-loaded catalog, not by embedded images; a `sign_ref` that does not resolve is rejected at import time.
- An attempt's question set is snapshotted at start; administrator content edits apply only to subsequent attempts and never mutate a live, in-progress attempt.
- The road-sign catalog (sourced from the SARTSM / legislation) is the foundational, prerequisite deliverable that question content references.
- Question content carries two independent axes: the code(s) it applies to, and whether it is *shared* (rules of the road, road signs — common to all codes) or *code-specific* (e.g. vehicle controls). Both axes drive test composition.
- A combination sitting (Code 1 + Code 2, or Code 1 + Code 3) is one booking, not two separate tests: the shared Rules and Signs sections are answered once, and a separate vehicle-controls module is presented per selected code.
- Within a single code, the three sections (Rules, Signs, Controls) are independent cut-offs: failing any one section's minimum fails that code. In a combination, each code is graded independently — a learner may pass one code and fail the other, keeps the passed code, and re-tests only the failed code (partial pass).
- Per-code question counts, pass marks, and time limits are configurable data, not hardcoded logic; the current best-known defaults (official ranges, per the CLLT description) live in `test-structure.md` and should be confirmed against a live DLTC/CLLT terminal before production.
- Learner identity is an anonymous, self-custodied UUID: no authentication, no accounts, no PII. The UUID is the sole key to a profile — pasting it into a fresh install restores that profile, and losing it loses access, so keeping it safe is the learner's responsibility.

## Non-goals

- The K53 practical / yard / on-road driving test is out of scope; this system covers the theory learner's license test only.
- This is a practice and simulation tool, not an official or government-certified examination; passing here confers no legal credential.
- The app does not replicate the real test's biometric / eNaTIS identity verification (fingerprint login against the national database); learner identity is instead an anonymous self-custodied UUID (CAP-9), with no accounts or PII.

## Success signal

A learner opens the Flutter app, picks a single code or a valid combination (Code 1 + 2 or Code 1 + 3), and completes a randomised sitting that looks, scores, and passes/fails like the real computerised CLLT exam — including an independent per-code result with partial passes — while an administrator, from the Admin Panel, can stand up that test content by authoring it directly or importing a CSV/JSON bank, against a road-sign catalog already loaded with official legislative codes.

## Assumptions

- Assumed a mobile-first learner experience (native Flutter app) with the .NET API as the sole content source, since the brief names Flutter pulling tests from the .NET API.
- Assumed the Admin Panel is part of the .NET backend rather than a separate product.
- Assumed the Admin Panel's primary author is a K53 domain expert (e.g. an examiner/instructor) writing original content, since the legal constraint rules out reproducing the official question bank; the authoring UI and CSV/JSON import both serve that expert.

## Open Questions

- The current best-known K53 section composition and pass thresholds per code are recorded in `test-structure.md` (high confidence on the three-section shape, medium on exact counts, low on time limits). They still need confirmation against a primary official source (DLTC / gazette) before the simulation can be called faithful.
- What is the licensing/copyright status of the official K53 question-bank content — may the questions be reproduced and distributed in this app?
- What is the Admin Panel's authentication and authorisation model — who may create, publish, and import tests?
- Is offline test-taking in scope for the Flutter app, or is it online-only against the API?
- Is language support English-only, or must the app cover multiple South African official languages (e.g. Afrikaans)?
