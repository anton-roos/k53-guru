---
title: 'Start a single-code attempt with randomised, snapshotted questions'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'd06486f53a97f85c6394f05ce4c5a6090289e9fb'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No `Attempt` concept exists anywhere -- a learner has no way to actually sit a published, single-code `Test`. Composing one must be randomised (no fixed, memorisable sequence), correctly structured (right question count per section, per `TestConfig`), and frozen at start time so later content edits never disturb an in-progress or resumed attempt.

**Approach:** Add `LearnerProfile` (a minimal, anonymous, client-supplied-UUID identity -- no accounts, no PII, matching the architecture's anonymous learner model), `Attempt`, `AttemptQuestion`, and `AttemptAnswerOption` (an immutable, owned copy of the composed questions -- never a live FK back to `Question`). `POST /api/v1/attempts` composes a new attempt by randomly sampling and ordering questions from the chosen `Test`'s own curated `TestQuestions` pool (per confirmed direction: Epic 2's admin curation is the real content pool, not the raw global `Question` bank), grouped by section, using each section's `TestConfig`/`SectionRule.QuestionCount`. `GET /api/v1/attempts/{id}` re-reads the same frozen snapshot, proving resume returns identical order by construction (it never re-randomises, only re-reads persisted `DisplayOrder`).

## Boundaries & Constraints

**Always:**
- New `Domain.Entities.LearnerProfile` -- `Guid Id` (client-supplied, `ValueGeneratedNever()`; no server-side "register" step -- a client generates its own UUID locally and first uses it here), implements `IAuditableEntity` directly rather than inheriting `BaseAuditableEntity` (which is hard-wired to `int` via `BaseEntity : IEntity<int>`) -- this is the solution's first Guid-keyed entity, scoped narrowly to the one entity that genuinely needs a client-held identifier with no server round-trip; `Attempt`/`AttemptQuestion`/`AttemptAnswerOption` stay ordinary `BaseAuditableEntity` (int, server-assigned), since nothing requires the client to pre-know their ids.
- New `Domain.Entities.Attempt : BaseAuditableEntity` -- `LearnerProfileId` (Guid), `TestId` (int, the source `Test` -- traceability only, content is frozen below), `Code` (`LicenceCode`, single bit, copied from `Test.Codes` at start time), `StartedAt` (UTC), `AttemptQuestions` (`List<AttemptQuestion>`).
- New `Domain.Entities.AttemptQuestion : BaseAuditableEntity` -- `AttemptId`, `QuestionId` (traceability only), `Section` (`SectionType`), `DisplayOrder` (int, globally sequential across the whole attempt: section order fixed Rules -> Signs -> VehicleControls, shuffled only within each section's block -- a single monotonic field is simpler for both storage and client iteration than a compound per-section counter, and still lets the client group by `Section` for "Section N of M" display), `Stem`, `SignRef` -- an owned COPY of the source `Question`'s content at snapshot time, never re-read live.
- New `Domain.Entities.AttemptAnswerOption : BaseAuditableEntity` -- `AttemptQuestionId`, `Text`, `IsCorrect`, `Order` -- an owned copy of the source `AnswerOption`s, in their original (admin-authored) order; no shuffling of options within a question, only of questions within a section (matches the AC's own scope).
- `StartAttemptCommand { LearnerProfileId (Guid), TestId (int) }` handler: (1) load `Test` by id, reject if missing or `Status != Published` or `Codes` is not exactly one of `Code1`/`Code2`/`Code3` (a combination is out of scope -- Story 3.4's concern); (2) load the matching `TestConfig`+`SectionRule`s for that `Code`; (3) load `Test.TestQuestions` (`Include(tq => tq.Question).ThenInclude(q => q.AnswerOptions)`), grouped by `Question.Section`; (4) for each section in fixed order, reject the whole command (per confirmed direction) if that section's pool count is less than its `SectionRule.QuestionCount`; else randomly shuffle and take exactly that count; (5) upsert the `LearnerProfile` (find by id, create if it doesn't exist yet -- this command IS the profile's first write, no separate "create profile" endpoint); (6) build and save the `Attempt`+`AttemptQuestion`+`AttemptAnswerOption` graph with sequential `DisplayOrder`.
- `GetAttemptQuery { AttemptId (int), LearnerProfileId (Guid) }` -- loads the `Attempt` only if both the id AND the owning `LearnerProfileId` match (a mismatch returns the same "not found" failure as a nonexistent id, never leaking another learner's attempt's existence), `Include`s `AttemptQuestions`/`AttemptAnswerOptions` ordered by `DisplayOrder`.
- Both commands/queries return the same `AttemptDto` shape (`Id`, `Code`, `StartedAt`, and ordered `AttemptQuestionDto`s each with `Section`/`DisplayOrder`/`Stem`/`SignRef`/ordered `AttemptAnswerOptionDto`s of `Text`/`Order`) -- `IsCorrect` is never serialized into the response DTO at all (the server retains it for future grading, Story 3.5, but nothing in this story's scope requires or should expose it -- a safe default ahead of Story 3.6's Practice/Test confidentiality split, not a decision this story reopens later).
- New `AttemptsController`, `[Route("api/v1/[controller]")]`, mirrors `SittingsController`'s shape (thin, no business logic, `[EnableRateLimiting("learner-api")]` reused as-is for now): `POST /api/v1/attempts` (body: `{ learnerProfileId, testId }`) -> `StartAttemptCommand`; `GET /api/v1/attempts/{id}?learnerProfileId=...` -> `GetAttemptQuery`.
- New `Migrators.PostgreSQL` migration for all four new entities, matching this codebase's established Postgres-as-source-of-truth pattern.

**Ask First:**
- None.

**Never:**
- No combination-sitting composition (Test.Codes with more than one bit) -- Story 3.4.
- No grading/scoring, no `CodeResult`/`SectionResult` (Story 3.5) -- this story only composes and freezes questions.
- No Practice/Test mode distinction, no answer submission, no timing enforcement (Story 3.6).
- No per-UUID rate-limit partitioning on the new endpoints, despite a `LearnerProfileId` now existing -- the existing IP-based `"learner-api"` policy (Story 3.1) is reused unchanged; switching to UUID-based partitioning (which the architecture notes anticipate once a UUID exists) is deferred rather than bundled into an already-large story.
- No shuffling of `AnswerOption` order within a question -- only question selection/order within a section is randomised.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Start, valid single-code Test | Published `Test`, `Codes=Code1`, enough questions per section | `Attempt` created with 3 sections' worth of `AttemptQuestion`s in fixed section order, `DisplayOrder` 1..N | N/A |
| Start, two attempts of the same Test | Same `Test` started twice | The two attempts' question selection and/or intra-section order differ | N/A |
| Start, Test not found | Nonexistent `TestId` | Rejected; nothing persisted | `Result.Failure`, "not found" |
| Start, Test not published | `Test.Status == Draft` | Rejected; nothing persisted | `Result.Failure`, clear message |
| Start, Test is a combination | `Test.Codes = Code1\|Code2` | Rejected; nothing persisted | `Result.Failure`, clear message |
| Start, insufficient pool in a section | Test's Rules pool has fewer questions than `SectionRule.QuestionCount` | Rejected; nothing persisted | `Result.Failure`, names the under-provisioned section |
| Start, new learner | `LearnerProfileId` not seen before | A `LearnerProfile` row is created alongside the `Attempt` | N/A |
| Resume, content edited since start | Attempt started, then the source `Question`'s `Stem` is edited | `GetAttemptQuery` returns the original (pre-edit) snapshotted `Stem`, unchanged | N/A |
| Resume, called twice | Same attempt fetched twice | Both calls return identical `DisplayOrder`s and content | N/A |
| Resume, wrong learner | Correct `AttemptId`, different `LearnerProfileId` | Rejected, same as not-found | `Result.Failure`, "not found" |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Entities/LearnerProfile.cs` -- New.
- `src/K53Guru/src/Domain/Entities/Attempt.cs` -- New.
- `src/K53Guru/src/Domain/Entities/AttemptQuestion.cs` -- New.
- `src/K53Guru/src/Domain/Entities/AttemptAnswerOption.cs` -- New.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/LearnerProfileConfiguration.cs` -- New. `builder.Property(x => x.Id).ValueGeneratedNever();` plus cascade `HasMany(x => x.Attempts)`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/AttemptConfiguration.cs` / `AttemptQuestionConfiguration.cs` / `AttemptAnswerOptionConfiguration.cs` -- New. Mirror `TestConfiguration.cs`'s cascade/enum-as-string shape.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs` -- New. Composition + validation + LearnerProfile upsert, per Boundaries above.
- `src/K53Guru/src/Application/Features/Attempts/Queries/GetById/GetAttemptQuery.cs` -- New.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs` (+`AttemptQuestionDto`/`AttemptAnswerOptionDto`) -- New. Shared by both the command's response and the query.
- `src/K53Guru/src/Server.UI/Controllers/Api/V1/AttemptsController.cs` -- New. Mirrors `SittingsController.cs`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` / `IApplicationDbContext.cs` -- Modify. Add the four new `DbSet`s to both.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/*_AddAttempts.cs` -- New.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/StartAttemptCommandHandlerTests.cs` -- New. Covers the start-side matrix rows via SQLite in-memory, seeding a `Test`+`TestQuestions`+`TestConfig` fixture.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/GetAttemptQueryHandlerTests.cs` -- New. Covers the resume-side matrix rows.
- Review fix (edge-case-hunter + blind-hunter, independently converged): two concurrent `StartAttemptCommand` calls for the same brand-new `LearnerProfileId` (client double-submit, retried timed-out request) both saw "not found" and both tried to insert a `LearnerProfile`, so the loser failed outright on a unique-constraint violation instead of succeeding. `StartAttemptCommand.cs` now catches that specific `DbUpdateException` case, detaches the speculatively-added `LearnerProfile`, and retries `SaveChangesAsync` once against the now-existing row.
- Review fix (edge-case-hunter): a `TestConfig` missing one of its three `SectionRule`s (not reachable today through any admin flow, but unguarded) would have silently composed an attempt with fewer than 3 sections. `StartAttemptCommand.cs` now checks all three `SectionType`s are represented before selecting any questions, failing with a message naming the missing section(s).
- Review fix (verification-gap + edge-case-hunter): added a test for the exact-count pool boundary (zero surplus), a test proving randomness holds for repeat starts by the *same* learner (not just different learners -- closes a blind spot where a regression seeding the shuffle off `LearnerProfileId` would have passed undetected), a strengthened insufficient-pool test that starves only one non-Rules section to prove the failure message names the *actual* short section rather than always "Rules", and a test asserting the wrong-learner and nonexistent-id failure messages are byte-for-byte identical (the spec's own "never leaking existence" guarantee, previously only type-checked, not content-checked).

## Tasks & Acceptance

**Execution:**
- [x] `LearnerProfile.cs`/`Attempt.cs`/`AttemptQuestion.cs`/`AttemptAnswerOption.cs` -- create the four new entities.
- [x] EF configurations for all four -- create, wire cascades.
- [x] `ApplicationDbContext.cs`/`IApplicationDbContext.cs` -- add the four new `DbSet`s.
- [x] `AttemptDto.cs` (+nested DTOs) -- create the shared response shape.
- [x] `StartAttemptCommand.cs` (+handler) -- create composition/validation/upsert logic.
- [x] `GetAttemptQuery.cs` (+handler) -- create the resume read.
- [x] `AttemptsController.cs` -- create the two thin endpoints.
- [x] `Migrators.PostgreSQL` migration -- generated (`20260831114057_AddAttempts`); not applied to a live database - no MSSQL/PostgreSQL instance is reachable in this sandbox (mirrors prior stories' verification notes).
- [x] `StartAttemptCommandHandlerTests.cs` / `GetAttemptQueryHandlerTests.cs` -- add tests covering all matrix rows.

**Acceptance Criteria:**
- Given a published single-code test and its configuration, when the client starts an attempt, then the server selects questions per section per the configured counts and assigns an immutable per-section `display_order`, shuffled intra-section while section order stays fixed (Rules -> Signs -> Controls).
- Given two attempts of the same test, when they are started, then they produce differently ordered and/or selected question sets.
- Given an in-progress attempt, when the underlying content is later edited, then the attempt's snapshot is unchanged, and a resume returns the identical `display_order`.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter "StartAttemptCommandHandlerTests|GetAttemptQueryHandlerTests"` -- expected: all matrix-row tests pass.

**Manual checks (if no CLI):**
- `curl -X POST https://localhost:7256/api/v1/attempts -d '{"learnerProfileId":"<new guid>","testId":<a published single-code test id>}'`, confirm a 200 with an ordered question list; repeat with a fresh guid, confirm a different order/selection; edit the source question's stem via the Admin Panel, then `GET /api/v1/attempts/{id}?learnerProfileId=...` and confirm the original stem is still returned.

**Known environment gap:** the new Postgres migration (`20260831114057_AddAttempts`) was generated but not applied to a live database -- no MSSQL/PostgreSQL instance is reachable in this sandbox, matching every prior story's verification notes. Reviewed by hand against the entity/EF configuration design and confirmed structurally correct (cascades, `uuid` primary key with no value-generation strategy for `learner_profiles`, no unrelated schema drift).

## Suggested Review Order

**Composition core (the heart of this story)**

- Entry point: load/validate Test, load TestConfig, compose sections in fixed order.
  [`StartAttemptCommand.cs:33`](../../src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs#L33)

- Review fix: guard against a `TestConfig` missing a `SectionRule`.
  [`StartAttemptCommand.cs:75`](../../src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs#L75)

- Review fix: concurrent first-time `LearnerProfile` creation, detach-and-retry-once.
  [`StartAttemptCommand.cs:145`](../../src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs#L145)

- Resume: ownership check, ordered `Include`, proves identical order by construction.
  [`GetAttemptQuery.cs:34`](../../src/K53Guru/src/Application/Features/Attempts/Queries/GetById/GetAttemptQuery.cs#L34)

- The four new entities, and the first Guid-keyed entity's design (`LearnerProfile`, implementing `IAuditableEntity` directly rather than inheriting the int-keyed `BaseAuditableEntity`).
  [`LearnerProfile.cs`](../../src/K53Guru/src/Domain/Entities/LearnerProfile.cs), [`Attempt.cs`](../../src/K53Guru/src/Domain/Entities/Attempt.cs), [`AttemptQuestion.cs`](../../src/K53Guru/src/Domain/Entities/AttemptQuestion.cs), [`AttemptAnswerOption.cs`](../../src/K53Guru/src/Domain/Entities/AttemptAnswerOption.cs)

- The shared response DTO, deliberately never carrying `IsCorrect`.
  [`AttemptDto.cs`](../../src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs)

**API surface**

- The two thin endpoints, mirroring `SittingsController`'s shape.
  [`AttemptsController.cs`](../../src/K53Guru/src/Server.UI/Controllers/Api/V1/AttemptsController.cs)

**Test coverage**

- All 10 matrix rows plus the review-fix regression tests, against a real SQLite in-memory context.
  [`StartAttemptCommandHandlerTests.cs:37`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/StartAttemptCommandHandlerTests.cs#L37), [`GetAttemptQueryHandlerTests.cs:35`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/GetAttemptQueryHandlerTests.cs#L35)
