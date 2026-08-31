---
title: 'Deliver Practice and Test modes with answer confidentiality and server timing'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: '433ad1c9c77cb598a34a1b30c0da3717a995e320'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every attempt built so far (3.3-3.5) behaves like Test mode -- confidential until final submission, no inline feedback -- but Practice mode's "answer, see correctness + explanation immediately, retry, no time limit" doesn't exist, and nothing distinguishes the two modes at all. There's also no `Explanation` field anywhere in the content model (Epic 1/2 never added one), so Practice mode's own AC ("returns correctness and explanation") has no data to return.

**Approach:** Add `Attempt.Mode` (`Practice`/`Test`, chosen at start). Add `Question.Explanation` (nullable, optional -- a small, contained Epic 1/2 touch: schema + `AddEditQuestionCommand` + a textarea in the existing question editor, since without an authoring path the field would be permanently empty and the feature decorative) and snapshot it onto `AttemptQuestion` just like `Stem`/`SignRef` already are. New `POST /api/v1/attempts/{id}/check-answer` (Practice-mode-only) gives immediate per-question correctness + explanation and can be called repeatedly per question (retry). `SubmitAttemptCommand` gains server-computed time-limit enforcement, but only for Test mode -- Practice mode never has a deadline.

## Boundaries & Constraints

**Always:**
- New `Domain.Enums.AttemptMode { Practice, Test }`. `Attempt.Mode` (required) is set once at `StartAttemptCommand` time (new required parameter) and never changes.
- `Question.Explanation` (string?, nullable, no format validation beyond a sane max length e.g. 2000 -- matching `Stem`'s existing length) -- optional at authoring time, `AddEditQuestionCommand`/`AddEditQuestionCommandValidator` gain it, `QuestionFormDialog.razor` gains a single multi-line text field for it (mirrors `Stem`'s existing field shape). `AttemptQuestion.Explanation` is a snapshot copy taken at attempt-start time, exactly like `Stem`/`SignRef` already are.
- New `CheckAnswerCommand { AttemptId (int), LearnerProfileId (Guid), AttemptQuestionId (int), SelectedAttemptAnswerOptionId (int) }` handler: (1) same ownership check as `GetAttemptQuery`/`SubmitAttemptCommand` (id + `LearnerProfileId`, else `NotFoundException`); (2) reject with `Result.Failure` if `Attempt.Mode != Practice` ("check-answer is only available in Practice mode") -- Test mode's confidentiality must never be bypassable through this endpoint; (3) reject if `Attempt.SubmittedAt` is already set (no answering after the attempt is done, matching `SubmitAttemptCommand`'s own "no re-submission" rule); (4) resolve the target `AttemptQuestion`/`AttemptAnswerOption`s (404-equivalent `Result.Failure` if they don't belong to this attempt); (5) clear `IsSelected` on every option for that question, then set the newly-selected one -- explicitly re-settable, this IS what "permits retry" means, not a one-shot lock like `SubmitAttemptCommand`'s answers; (6) return `CheckAnswerResultDto { IsCorrect, CorrectAttemptAnswerOptionId, Explanation }` -- reveals the correct option and explanation immediately, by design (Practice mode's whole point), never persists a `CodeResult`/`SectionResult` (that's still only `SubmitAttemptCommand`'s job, callable separately if/when a practice session wants an overall score).
- `SubmitAttemptCommand` gains server-side time-limit enforcement for Test mode only: after the ownership/already-submitted/duplicate-answer checks, if `attempt.Mode == AttemptMode.Test`, look up any constituent code's `TestConfig.TimeLimitMinutes` (Story 3.2 seeds identical values across every code, same reasoning `StartAttemptCommand` already relies on for Rules/Signs) and reject with `Result.Failure` ("time limit exceeded") if `DateTime.UtcNow - attempt.StartedAt` exceeds it -- nothing persisted. Practice mode NEVER enforces this, regardless of `TimeLimitMinutes`.
- `SubmitAttemptCommand` gains an optional `ClientSubmittedAt` (DateTime?) parameter, stored on `Attempt` for diagnostics only -- the lateness check above always uses server `DateTime.UtcNow`, never the client-supplied value.
- `AttemptDto` gains `Mode` -- the client needs this to know which UI to render (inline-feedback Practice vs. cold-simulation Test); no other response DTO gains mode-dependent fields (the "no `IsCorrect`/no explanation" default for `AttemptDto`/`GetAttemptQuery`/`SubmitAttemptCommand`'s response stays unchanged and mode-independent -- immediate feedback is `CheckAnswerCommand`'s job alone, and only in Practice mode).
- New `AttemptsController` action: `POST /api/v1/attempts/{id}/check-answer`, mirrors the existing three actions' shape.
- New `Migrators.PostgreSQL` migration for `Attempt.Mode`/`ClientSubmittedAt`, `Question.Explanation`, `AttemptQuestion.Explanation`.

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No changes to `StartAttemptCommand`'s composition/randomisation/validation logic beyond adding the `Mode` parameter and snapshotting `Explanation` alongside `Stem`/`SignRef`.
- No new Blazor Admin page or grid column for `Explanation` beyond the one text field in the existing `QuestionFormDialog` -- no separate explanation-management feature.
- No retroactive backfill of `Explanation` for existing seeded/authored questions -- nullable, optional, populated going forward only.
- No changes to `GetAvailableSittingsQuery`/`AvailableSittingDto` (Story 3.1) -- mode selection happens at attempt-start time, not discovery time.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Check answer, Practice mode | `Attempt.Mode=Practice`, valid question/option | Returns `IsCorrect`/`CorrectAttemptAnswerOptionId`/`Explanation`; `IsSelected` updated | N/A |
| Check answer, retry | Same question checked twice with different answers | Second call's selection replaces the first; both calls return correct feedback | N/A |
| Check answer, Test mode | `Attempt.Mode=Test` | Rejected; nothing revealed | `Result.Failure`, clear message |
| Check answer, already submitted | `Attempt.SubmittedAt` set | Rejected | `Result.Failure` |
| Submit, Test mode, within time limit | Elapsed time <= `TimeLimitMinutes` | Grades normally (as Story 3.5) | N/A |
| Submit, Test mode, late | Elapsed time > `TimeLimitMinutes` | Rejected; nothing persisted | `Result.Failure`, "time limit exceeded" |
| Submit, Practice mode, arbitrarily late | Elapsed time far exceeds `TimeLimitMinutes` | Grades normally -- Practice mode has no deadline | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Enums/AttemptMode.cs` -- New.
- `src/K53Guru/src/Domain/Entities/Attempt.cs` -- Modify. Add `Mode`, `ClientSubmittedAt`.
- `src/K53Guru/src/Domain/Entities/Question.cs` -- Modify. Add `Explanation`.
- `src/K53Guru/src/Domain/Entities/AttemptQuestion.cs` -- Modify. Add `Explanation`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/AttemptConfiguration.cs`/`QuestionConfiguration.cs`/`AttemptQuestionConfiguration.cs` -- Modify. New enum-as-string/max-length column configs.
- `src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs` (+Validator) -- Modify. Add `Explanation`.
- `src/K53Guru/src/Server.UI/Pages/Questions/QuestionFormDialog.razor` -- Modify. Add the explanation text field.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs` -- Modify. Add `Mode` parameter; snapshot `Explanation`.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Submit/SubmitAttemptCommand.cs` -- Modify. Time-limit enforcement for Test mode; `ClientSubmittedAt` parameter.
- `src/K53Guru/src/Application/Features/Attempts/Commands/CheckAnswer/CheckAnswerCommand.cs` -- New.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs` -- Modify. Add `Mode`.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/CheckAnswerResultDto.cs` -- New.
- `src/K53Guru/src/Server.UI/Controllers/Api/V1/AttemptsController.cs` -- Modify. Add the check-answer action.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/*_AddPracticeMode.cs` -- New.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/CheckAnswerCommandHandlerTests.cs` -- New. Covers the check-answer matrix rows.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/StartAttemptCommandHandlerTests.cs`/`SubmitAttemptCommandHandlerTests.cs` -- Modify. Add `Mode` to existing seeding helpers; add tests for the timing matrix rows.
- Beyond the original Code Map: `src/K53Guru/src/Application/Features/Questions/DTOs/QuestionDto.cs` also gained `Explanation` -- required so an existing question's already-authored explanation isn't silently nulled out on every edit-save round-trip (`QuestionDto` -> `AddEditQuestionCommand` -> back onto the tracked `Question` entity); confirmed necessary, not scope creep.
- Review fix (blind-hunter + edge-case-hunter, independently converged, significant): `SubmitAttemptCommand`'s answer-recording loop never cleared sibling options before setting a new selection, unlike `CheckAnswerCommand` -- a Practice-mode learner who checked one answer then submitted a *different* final answer for the same question ended up with BOTH options marked selected, and grading counted it correct regardless of their actual final answer. Now clears every sibling before setting the new one, mirroring `CheckAnswerCommand`'s pattern.
- Review fix (edge-case-hunter, novel finding): `CheckAnswerCommand`'s clear-then-set loop was vulnerable to a concurrent-double-check race -- EF only emits `UPDATE`s for properties that changed from what a given `DbContext` loaded, so two near-simultaneous calls could each "clear" an already-false sibling as a no-op and each only write their own selection, leaving two options marked selected in the database. Now forces every option's `IsSelected` to be marked modified before saving, so the last writer always fully rewrites all sibling rows.
- Review fix (blind-hunter + edge-case-hunter, independently converged): the new migration's `mode` column used `defaultValue: ""`, not a valid `AttemptMode` enum name -- any pre-existing `Attempt` row from Stories 3.3-3.5 backfilled with it would crash on the next read. Changed to `defaultValue: "Test"`, matching this story's own stated reasoning that every prior attempt behaved like Test mode.
- Review fix (verification-gap): added a test proving `AttemptDto.Mode` actually round-trips through `GetAttemptQuery`'s resume path with a non-default `Mode` value (the one existing call site in that test file was never updated and silently relied on the enum's default).
- Review fix: added a regression test proving the CheckAnswer-then-Submit-a-different-answer fix works -- grading reflects only the final submitted answer, and the previously-checked wrong option reads back as unselected.

## Tasks & Acceptance

**Execution:**
- [x] `AttemptMode.cs` -- create.
- [x] `Attempt.cs`/`Question.cs`/`AttemptQuestion.cs` (+EF configs) -- add new fields.
- [x] `AddEditQuestionCommand.cs`/Validator/`QuestionFormDialog.razor` -- add `Explanation` authoring.
- [x] `StartAttemptCommand.cs` -- add `Mode` parameter, snapshot `Explanation`.
- [x] `SubmitAttemptCommand.cs` -- add time-limit enforcement + `ClientSubmittedAt`.
- [x] `CheckAnswerCommand.cs` (+handler) -- create.
- [x] `AttemptDto.cs`/`CheckAnswerResultDto.cs` -- create/modify.
- [x] `AttemptsController.cs` -- add the check-answer action.
- [x] `Migrators.PostgreSQL` migration -- generate.
- [x] Tests -- cover all matrix rows across the three test files listed above.

**Acceptance Criteria:**
- [x] Given Practice mode, when the client fetches questions and submits an answer, then the server returns correctness and explanation, permits retry, and applies no time limit.
- [x] Given Test mode, when the client fetches questions, then correct keys and explanations are withheld; only a final per-section/per-code result is returned at submission.
- [x] Given a timed Test sitting, when a submission arrives, then elapsed time is computed server-side; a late submission is rejected; any client-supplied timestamp is diagnostic only.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj` -- expected: all tests pass, no regressions.
