---
title: 'Submit and grade an attempt server-side'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'bb2f0bb4347a11aaba5e9fa9b4221b41b5abb61f'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** An `Attempt` can be composed (3.3) and even combined across codes (3.4), but a learner has no way to submit answers and get a graded result -- `AttemptQuestion`/`AttemptAnswerOption` don't even record which option the learner picked.

**Approach:** Add `POST /api/v1/attempts/{id}/submit`, backed by `SubmitAttemptCommand`, which records the learner's selected option per question, then grades entirely server-side: for each code in the attempt (one for a single-code Test, two for a combination -- reusing `AttemptQuestion.Code`'s `HasFlag`-based semantics established in Story 3.4), compute a per-section correct count against the CURRENT `TestConfig`/`SectionRule.PassMark` (read fresh at grading time, not snapshotted -- Story 3.2's own AC already establishes that scoring, like composition, reads config live) and derive that code's pass/fail (failing any one section fails that code). Persist `CodeResult`/`SectionResult` and return an aggregate, per-question-detail-free result DTO.

## Boundaries & Constraints

**Always:**
- `AttemptAnswerOption` gains `IsSelected` (bool, default false) -- the learner's recorded choice, set by `SubmitAttemptCommand`, never exposed back out through `AttemptDto` (which still never carries `IsCorrect`).
- `Attempt` gains `SubmittedAt` (DateTime?, null while in-progress) -- `SubmitAttemptCommand` rejects a second submission attempt on the same `Attempt` (`Result.Failure`, "already submitted").
- `AttemptQuestionDto`/`AttemptAnswerOptionDto` gain an `Id` field each (previously unexposed) -- the client needs these to reference which question/option it's answering when submitting.
- New `Domain.Entities.CodeResult : BaseAuditableEntity` -- `AttemptId`, `Code` (`LicenceCode`, matching `AttemptQuestion.Code`'s semantics: the single code or full combination this result covers), `Passed` (bool), `SectionResults` (`List<SectionResult>`).
- New `Domain.Entities.SectionResult : BaseAuditableEntity` -- `CodeResultId`, `Section` (`SectionType`), `CorrectCount` (int), `PassMark` (int, the threshold applied -- recorded for transparency even though not snapshotted from the config), `Passed` (bool).
- `SubmitAttemptCommand { AttemptId (int), LearnerProfileId (Guid), Answers: List<{ AttemptQuestionId (int), SelectedAttemptAnswerOptionId (int) }> }` handler: (1) load the `Attempt` with the same ownership check as `GetAttemptQuery` (id + `LearnerProfileId` match, else `NotFoundException`); (2) reject if `SubmittedAt` is already set; (3) for each submitted answer, set the matching `AttemptAnswerOption.IsSelected = true` (an `AttemptQuestionId`/`SelectedAttemptAnswerOptionId` that doesn't belong to this attempt is ignored, not an error -- a question with no matching answer in the submitted list is simply graded as incorrect, no crash either way); (4) derive the attempt's constituent codes via the same `HasFlag(Code1)`/`HasFlag(Code2)`/`HasFlag(Code3)` pattern `StartAttemptCommand` already uses (extract this into a small shared `LicenceCodeExtensions.GetConstituentCodes()` helper in `Domain`, used by both handlers, rather than duplicating the three `if` checks a second time); (5) for each constituent code, group that code's `AttemptQuestion`s (`q.Code.HasFlag(code)`) by `Section`, compute `CorrectCount` per section against the CURRENT `TestConfig`(code)/`SectionRule`(section)`.PassMark`, and derive `SectionResult.Passed`/`CodeResult.Passed` (all sections must pass); (6) persist `CodeResult`+`SectionResult`, set `Attempt.SubmittedAt = UtcNow`, save.
- Response DTO (`GradedAttemptResultDto`): `AttemptId`, `Passed` (overall -- every `CodeResult.Passed`), `CodeResults: List<CodeResultDto>` each with `Code`/`Passed`/`SectionResults: List<SectionResultDto>` (`Section`/`CorrectCount`/`PassMark`/`Passed`). No per-question correctness/explanation is exposed -- matches the same "no `IsCorrect` in the wire response" default `AttemptDto` already established, and is symmetric with Story 3.6's eventual Test-mode default (this story is mode-agnostic; the distinction is 3.6's job).
- New `AttemptsController` action: `POST /api/v1/attempts/{id}/submit`, mirrors the existing two actions' shape (thin, `[EnableRateLimiting("learner-api")]`, anonymous).
- New `Migrators.PostgreSQL` migration for `CodeResult`/`SectionResult` plus the two new columns (`AttemptAnswerOption.IsSelected`, `Attempt.SubmittedAt`).

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No timing/lateness enforcement (`Attempt.StartedAt` vs. a time limit) -- Story 3.6's concern.
- No Practice-mode inline correctness/explanation, no Test-mode confidentiality distinction -- this story's response shape is the single, mode-agnostic shape both future modes will build on.
- No re-grading/re-submission path -- a second submit on an already-submitted `Attempt` is rejected outright, not idempotently re-computed.
- No snapshotting of `SectionRule.PassMark` onto the `Attempt` at start time -- grading reads the config live, per Story 3.2's own established "scoring reads config, not hardcode" AC.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Submit, single-code, all sections pass | Correct answers meet every section's PassMark | `Passed=true`, one `CodeResult` with all `SectionResult.Passed=true` | N/A |
| Submit, single-code, one section fails | One section's correct count is below PassMark | `Passed=false`, that section's `SectionResult.Passed=false`, others true | N/A |
| Submit, combination, partial pass | Code1's sections all pass, Code2's VehicleControls fails | Overall `Passed=false`; `CodeResults` show Code1 `Passed=true`, Code2 `Passed=false` | N/A |
| Submit, attempt not found / wrong learner | Nonexistent id or mismatched `LearnerProfileId` | Rejected, identical to Story 3.3's resume behavior | `Result.Failure`, "not found" |
| Submit, already submitted | `SubmittedAt` already set | Rejected; no re-grading | `Result.Failure`, "already submitted" |
| Submit, unanswered question | An `AttemptQuestion` has no matching entry in `Answers` | Graded as incorrect for that question; no crash | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Common/LicenceCodeExtensions.cs` -- New. `GetConstituentCodes(this LicenceCode)` shared helper, extracted from `StartAttemptCommand`'s existing inline logic.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs` -- Modify. Use the new shared helper instead of its own inline `if` checks.
- `src/K53Guru/src/Domain/Entities/AttemptAnswerOption.cs` -- Modify. Add `IsSelected`.
- `src/K53Guru/src/Domain/Entities/Attempt.cs` -- Modify. Add `SubmittedAt`.
- `src/K53Guru/src/Domain/Entities/CodeResult.cs` / `SectionResult.cs` -- New.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/CodeResultConfiguration.cs` / `SectionResultConfiguration.cs` -- New. Mirror sibling cascade/enum-as-string shape.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Submit/SubmitAttemptCommand.cs` -- New.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs` -- Modify. Add `Id` to `AttemptQuestionDto`/`AttemptAnswerOptionDto`.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/GradedAttemptResultDto.cs` (+`CodeResultDto`/`SectionResultDto`) -- New.
- `src/K53Guru/src/Server.UI/Controllers/Api/V1/AttemptsController.cs` -- Modify. Add the submit action.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` / `IApplicationDbContext.cs` -- Modify. Add the two new `DbSet`s.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/*_AddResults.cs` -- New.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/SubmitAttemptCommandHandlerTests.cs` -- New. Covers all 6 matrix rows.
- Review fix (blind-hunter, significant): submitting two different answers for the same `AttemptQuestionId` used to mark both `AttemptAnswerOption`s `IsSelected=true`, and grading's `Any(IsSelected && IsCorrect)` check then counted the question correct regardless of intent -- a learner could submit both the correct and an incorrect option for every question and guarantee every section passed. `SubmitAttemptCommand.cs` now rejects the whole submission outright if any `AttemptQuestionId` appears more than once in `Answers`, before touching anything.
- Review fix (blind-hunter, significant): two concurrent submits for the same `Attempt` both previously saw `SubmittedAt == null` and both persisted a full, duplicate set of `CodeResult`/`SectionResult` rows with no error -- worse than Story 3.3's already-fixed `LearnerProfile` race, since nothing here even detected the conflict. `CodeResultConfiguration.cs` now has a unique index on `(AttemptId, Code)`; the handler's final `SaveChangesAsync` is wrapped in a try/catch converting the resulting `DbUpdateException` into the same "already submitted" failure (no retry, unlike the `LearnerProfile` race -- the loser has nothing left to do once the winner has already fully graded and persisted).
- Review fix (blind-hunter): `AttemptsController.SubmitAttempt` returned 400 for every failure, including the not-found/wrong-learner case -- contradicting the spec's own "identical to Story 3.3's resume behavior" and `GetAttempt`'s 404 for the same underlying condition. Now returns 404 when the failure message matches the shared `NotFoundException` format, 400 otherwise.
- Review fix (verification-gap): added a test proving `Passed = All(...)` isn't confusable with a broken `Any`/`First` aggregation when a combination attempt has BOTH codes passing (the only prior multi-code test had exactly one pass and one fail); added a message-symmetry test between "not found" and "wrong learner" mirroring Story 3.3's equivalent guard against leaking attempt existence.
- Follow-up: added dedicated regression tests directly exercising both significant fixes above -- one proving the duplicate-answer rejection happens before any mutation (zero `IsSelected`/`CodeResult` side effects), and one forcing the exact interleaving needed to trigger the concurrency fix's `DbUpdateException` catch specifically (via a context that injects a competing insert immediately before its own `SaveChangesAsync` call), rather than relying only on indirect/first-pass coverage.

## Tasks & Acceptance

**Execution:**
- [x] `LicenceCodeExtensions.cs` -- extract the shared constituent-codes helper; update `StartAttemptCommand.cs` to use it.
- [x] `AttemptAnswerOption.cs`/`Attempt.cs` -- add `IsSelected`/`SubmittedAt`.
- [x] `CodeResult.cs`/`SectionResult.cs` (+EF configs) -- create.
- [x] `SubmitAttemptCommand.cs` (+handler) -- create grading logic per Boundaries.
- [x] `AttemptDto.cs` -- add `Id` to question/option DTOs.
- [x] `GradedAttemptResultDto.cs` (+nested DTOs) -- create.
- [x] `AttemptsController.cs` -- add the submit action.
- [x] `ApplicationDbContext.cs`/`IApplicationDbContext.cs` -- add the two new `DbSet`s.
- [x] `Migrators.PostgreSQL` migration -- generate.
- [x] `SubmitAttemptCommandHandlerTests.cs` -- cover all 6 matrix rows.

**Acceptance Criteria:**
- [x] Given a completed attempt's answers are submitted, when the server grades it, then it computes per-section scores against the configured cut-offs; failing any one section fails that code.
- [x] Given a combination sitting, when it is graded, then each code is graded independently, a partial pass is possible, and the result reports pass/fail per code plus overall.
- [x] Given grading occurs, then it is computed entirely server-side and returned as a versioned DTO; the client never grades, re-computes, or caches a grade.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter SubmitAttemptCommandHandlerTests` -- expected: all matrix-row tests pass.
