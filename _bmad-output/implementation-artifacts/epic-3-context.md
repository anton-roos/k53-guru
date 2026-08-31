# Epic 3 Context: Exam Engine API

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Deliver a versioned `/api/v1` learner-facing content API that is the fidelity heart of the system: it discovers published sittings, composes randomised and correctly-structured question sets (single code or valid combinations), snapshots each attempt immutably at start, and grades submissions server-side with faithful CLLT per-section and per-code rules (including partial passes). It also delivers Practice and Test modes with answer confidentiality and server-authoritative timing. This engine is consumed by the Flutter client (Epic 4) and underlies both the Practice (Epic 5) and Test-Mode (Epic 6) experiences; it depends on the sign/content model (Epic 1) and published content (Epic 2) already existing.

## Stories

- Story 3.1: Discover available sittings via the versioned API
- Story 3.2: Configure per-code test parameters as data
- Story 3.3: Start a single-code attempt with randomised, snapshotted questions
- Story 3.4: Compose valid combination sittings
- Story 3.5: Submit and grade an attempt server-side
- Story 3.6: Deliver Practice and Test modes with answer confidentiality and server timing

## Requirements & Constraints

- The API serves only published content anonymously (read) and never draft/unpublished tests.
- Question selection/ordering is randomised **per attempt** and **intra-section only**; section order is always fixed (Rules of the Road → Road Signs → Vehicle Controls); two attempts of the same test must not produce a fixed, memorisable sequence.
- Supported sittings are a single code (1/2/3) or a valid combination (Code 1+2 or Code 1+3) — Code 2+3 and all-three are invalid and must be rejected. A combination shares Rules/Signs once and adds one Vehicle-Controls module per selected code, in fixed order.
- Grading is faithful to the real CLLT: per-section scores against configured cut-offs, failing any one section fails that code; for combinations each code is graded independently with partial passes possible; the result reports pass/fail per code plus overall.
- Per-code question counts, pass marks, time limits, and section definitions are configuration data (seeded from `test-structure.md`, provisional pending confirmation against a live DLTC/CLLT terminal), never hardcoded thresholds.
- An attempt's question set is snapshotted at start; later admin content edits never mutate an in-progress/live attempt; a resume must return the identical order.
- Practice mode: server returns correctness + explanation inline, permits retry, no time limit — but correctness is still server-owned (client never derives/caches it).
- Test mode: correct keys and explanations are withheld entirely; only a final per-section/per-code result is returned at submission; elapsed time is computed server-side from `attempt.started_at`; a client `submitted_at` is diagnostic only and a late submission is rejected.
- The client-facing progress model is **by section** ("Section 2 of 4"), not a flat question counter — API responses need to carry enough section-position metadata to support this.
- This is a public internet surface: HTTPS only, `ProblemDetails` for all errors, per-UUID rate limiting to deter enumeration/abuse.
- Open/unconfirmed: exact official per-code question counts/pass marks/time limits (test-structure.md is provisional); whether Test-mode back-navigation scope is whole-paper or current-section-only (affects whether the API must support revising earlier answers mid-sitting).

## Technical Decisions

- **Delegation, not duplication**: `/api/v1` controllers are thin — they call the same Application-layer MediatR handlers the Blazor Admin Panel uses. No composition or scoring logic lives in a controller.
- **Server-side fidelity authority**: all composition (randomisation, section assembly, combination handling) and all scoring (cut-offs, per-code grading, partial pass) live in Domain/Application. The client only renders, collects answers, and displays the server's result verbatim — never grades, re-computes, re-validates, or caches a pass/fail.
- **Immutable attempt snapshot**: starting an attempt copies the full question set (questions, options, correct keys, section structure) into owned `AttemptQuestion` records, each assigned an immutable per-section `display_order` at snapshot time. Responses return records in `display_order`; the client renders as-received.
- **Sign references**: questions resolve `sign_ref` against the catalog's unique `legislation_code` via exact-one (`SingleOrDefault`) resolution — this is upstream content-model behavior the engine relies on but does not re-implement.
- **Versioned DTOs only**: EF entities and domain events are never serialized; every response is an explicit DTO; enums serialize as strings; a breaking change bumps the `/api/vN` segment.
- **Two authorization domains**: Admin auth (ASP.NET Identity) is unrelated to the learner API, which is anonymous-read plus UUID-keyed writes (attempt start/submit) — no PII, no accounts.
- **Operational envelope**: containerized via existing `docker-compose`; PostgreSQL production DB via `Migrators.PostgreSQL` (no `EnsureCreated`); config via `IOptions<T>`; reads via Ardalis.Specification with FusionCache; writes via MediatR commands through per-operation `IApplicationDbContextFactory` contexts (never a shared injected `DbContext`).
- **Core entities/relationships**: `LearnerProfile` owns `Attempt`s; `Attempt` snapshots `AttemptQuestion`s (immutable copies of `Question`, itself referencing `RoadSign` via `sign_ref`); `Attempt` grades to per-code `CodeResult`s, each with per-section `SectionResult`s; `TestConfig` defines `SectionRule`s (cut-offs, counts, time limits) that composition/scoring read.
- **Conventions**: IDs are GUIDs; timestamps UTC ISO-8601; CQRS types named `*Command`/`*Query`; DTOs named `*Dto`; options `*Settings`/`*Options`; logging via Serilog; validation via FluentValidation pipeline behaviours.

## Cross-Story Dependencies

- Depends on Epic 1 (road-sign catalog + two-axis content model) and Epic 2 (authored/imported, published content) as prerequisites — the engine composes and grades against content those epics produce.
- Epic 4 (Learner App Foundation) builds its typed API repository directly against this epic's `/api/v1` DTO contract; Epic 5 (Practice) and Epic 6 (Test-Mode) both depend on the Practice/Test delivery and grading behavior defined here (Story 3.5's grading result is what Epic 6's result screen displays verbatim; Story 3.6's mode split underlies both experience epics).
- Story 3.2's configuration values (counts/pass marks/time limits) are provisional pending confirmation against a live DLTC/CLLT terminal; downstream stories should not hardcode assumptions about their final values.
