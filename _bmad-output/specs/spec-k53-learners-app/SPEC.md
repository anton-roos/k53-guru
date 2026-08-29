---
id: SPEC-k53-learners-app
companions: []
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
  - **intent:** A learner using the Flutter app can take a test scoped to a single code (Code 1, 2, or 3) or any combination of codes, including all three at once.
  - **success:** The learner selects one or more codes and receives a test composed only of questions matching the selected code(s).

- **CAP-6**
  - **intent:** The test-taking flow simulates the real computerised K53 test as closely as possible in structure, scoring, and pass/fail outcome.
  - **success:** A completed attempt returns a per-section score and a pass/fail result that mirrors the official K53 scoring rules for the selected code(s).

## Constraints

- The system spans a .NET backend (Admin Panel + API) and a Flutter mobile client; the app pulls tests from the .NET API rather than bundling its own content.
- Fidelity to the official computerised K53 learner's test — format, question mix, scoring, and pass/fail rules — is the primary quality bar every design decision resolves against.
- Road signs must be stored with their official legislative codes, not ad-hoc labels, so questions and study material reference the same authoritative source.
- Question selection and ordering are randomised per attempt; no attempt may serve a fixed, memorisable sequence.
- Content must cover all three licence codes: Code 1 (motorcycle), Code 2 (light motor vehicle), and Code 3 (heavy motor vehicle), and support tests scoped to any one code or any combination.

## Non-goals

- The K53 practical / yard / on-road driving test is out of scope; this system covers the theory learner's license test only.
- This is a practice and simulation tool, not an official or government-certified examination; passing here confers no legal credential.

## Success signal

A learner opens the Flutter app, picks any single code or combination of Code 1/2/3, and completes a randomised test that looks, scores, and passes/fails like the real computerised K53 exam — while an administrator, from the Admin Panel, can stand up that test content by authoring it directly or importing a CSV/JSON bank, against a road-sign catalog already loaded with official legislative codes.

## Assumptions

- Assumed a mobile-first learner experience (native Flutter app) with the .NET API as the sole content source, since the brief names Flutter pulling tests from the .NET API.
- Assumed the Admin Panel is part of the .NET backend rather than a separate product.

## Open Questions

- What is the exact K53 section composition and pass threshold per code — the question counts, pass marks, and time limits for Rules of the Road, Road Signs/Signals/Markings, and Vehicle Controls? The official per-code values must be confirmed before CAP-6 can be built faithfully.
- What is the licensing/copyright status of the official K53 question-bank content — may the questions be reproduced and distributed in this app?
- What is the Admin Panel's authentication and authorisation model — who may create, publish, and import tests?
- Is offline test-taking in scope for the Flutter app, or is it online-only against the API?
- Is language support English-only, or must the app cover multiple South African official languages (e.g. Afrikaans)?
