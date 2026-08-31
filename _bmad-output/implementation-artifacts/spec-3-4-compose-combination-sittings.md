---
title: 'Compose valid combination sittings'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'ea8f1bc12f8e6eb36e56eb2111d26d62999a4e59'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `StartAttemptCommand` (Story 3.3) explicitly rejects any `Test` whose `Codes` is more than one bit -- combination sittings (Code1+2, Code1+3) have no way to compose at all.

**Approach:** Extend the same `StartAttemptCommand` to accept combination `Test`s: Rules and Signs are drawn and answered once (shared, using either constituent code's `TestConfig` since Story 3.2 seeds numerically identical values for every code), while VehicleControls gets one independent module per constituent code, each governed by that code's own `SectionRule`, in fixed code order (Code1 first, then the second code). `AttemptQuestion` gains a `Code` field so a future grader (Story 3.5) can tell which code's VehicleControls module a question belongs to, and can apply the one shared Rules/Signs result to every code in the attempt.

## Boundaries & Constraints

**Always:**
- `StartAttemptCommand`'s combination check changes from "reject anything but exactly one bit" to: accept `Code1`, `Code2`, `Code3` (unchanged, single-code path from Story 3.3), `Code1|Code2`, `Code1|Code3`; reject `Code2|Code3`, `Code1|Code2|Code3`, and `None` -- the same 5-valid-value allowlist Story 3.1's discovery query already uses (mirror that exact check rather than reinventing it).
- New `AttemptQuestion.Code` (`LicenceCode`, non-nullable): for Rules/Signs questions, set to the full `Attempt.Code` value (the whole combination, or the single code) -- these sections' result applies identically to every code in the attempt. For VehicleControls questions, set to the ONE specific constituent code that module belongs to.
- Composition for a combination `Test`: (1) Rules/Signs pool and `SectionRule` lookup work exactly as Story 3.3 already does (drawn once, `AttemptQuestion.Code = attempt.Code`); (2) for each constituent code in fixed order (`Code1` first when present, then `Code2`/`Code3`), independently look up that code's `TestConfig`+`VehicleControls` `SectionRule`, filter the Test's VehicleControls pool to questions whose `Question.Codes` includes that code (`HasFlag`), and compose that code's module exactly like Story 3.3's per-section logic (reject on insufficient pool, naming both the section AND the code); `DisplayOrder` continues monotonically across Rules, Signs, then each code's VehicleControls block in order.
- A `Test`'s VehicleControls pool question can legitimately carry more than one code's flag (shared-content edge case) -- filtering by `HasFlag` rather than exact equality means such a question is eligible for both codes' modules independently; nothing prevents the same underlying `Question` being drawn into both codes' modules if the admin's curation allows it (this mirrors the existing two-axis content model exactly, not a new concept).
- Single-code `Test`s (Story 3.3's original path) are entirely unaffected: `AttemptQuestion.Code` is simply set to the single code for every question (Rules/Signs/VehicleControls alike), which is a strict generalization, not a behavior change to what Story 3.3 already ships.
- New migration adding `AttemptQuestion.Code` as a required column -- backfill is a non-issue since no `Attempt` rows exist in any real environment yet (this is a brand-new feature with no production data).

**Ask First:**
- None (autonomous execution per explicit instruction).

**Never:**
- No grading/scoring logic (Story 3.5) -- this story only composes; the `Code` field is added for that story to consume, not used by anything yet.
- No changes to `GetAttemptQuery`'s shape beyond what naturally follows from `AttemptQuestionDto` gaining a `Code` field to expose the new entity property.
- No changes to Story 3.1's discovery filter or Story 3.2's `TestConfig` seeding -- both already correctly support everything this story needs.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Start, valid Code1+2 combination | Published Test, `Codes=Code1\|Code2`, sufficient pools | Attempt with Rules+Signs once (`Code=Code1\|Code2`), then Code1's VehicleControls module, then Code2's, in that order | N/A |
| Start, valid Code1+3 combination | Same shape, `Codes=Code1\|Code3` | Same structure with Code1 then Code3's modules | N/A |
| Start, Code2+3 combination | `Test.Codes=Code2\|Code3` | Rejected; nothing persisted | `Result.Failure`, clear message |
| Start, all-three combination | `Test.Codes=Code1\|Code2\|Code3` | Rejected; nothing persisted | `Result.Failure`, clear message |
| Start, combination with insufficient pool in one code's VehicleControls | Code1's module has too few questions, Code2's is fine | Rejected; nothing persisted; message names both the section and the short code | `Result.Failure` |
| Start, single-code Test (regression) | `Codes=Code1` alone | Composes exactly as Story 3.3, `AttemptQuestion.Code` set to `Code1` throughout | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Entities/AttemptQuestion.cs` -- Modify. Add `Code` (`LicenceCode`) property.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/AttemptQuestionConfiguration.cs` -- Modify. Add enum-as-string conversion for `Code`, matching the existing `Section` property's shape.
- `src/K53Guru/src/Application/Features/Attempts/Commands/Start/StartAttemptCommand.cs` -- Modify. Replace `IsSingleCode` with the 5-value allowlist check (mirroring `GetAvailableSittingsQuery`'s filter); branch composition into "shared Rules/Signs once" + "per-constituent-code VehicleControls loop"; set `AttemptQuestion.Code` throughout.
- `src/K53Guru/src/Application/Features/Attempts/DTOs/AttemptDto.cs` -- Modify. Add `Code` to `AttemptQuestionDto`.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/*_AddAttemptQuestionCode.cs` -- New.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Attempts/StartAttemptCommandHandlerTests.cs` -- Modify. Add tests covering all 6 matrix rows (combination cases new; single-code regression row confirms no behavior change).
- Review fixes (3-layer review, all independently converged findings): `.Single()` on a `TestConfig`'s VehicleControls `SectionRule` replaced with `.SingleOrDefault()` + graceful failure (no uniqueness constraint prevented a duplicate from throwing unhandled); `AttemptQuestion.Code` gained a doc comment clarifying the `HasFlag`-based access pattern future grading code must use (Rules/Signs carry the full combination, VehicleControls carries one bit -- a naive `==`/`GroupBy` would silently miss shared questions); the single-code regression test now asserts `DisplayOrder`/`Section` ordering itself rather than relying on a separate untouched test; one combination test now seeds genuinely different VehicleControls counts per constituent code (previously both codes always got the same count, so a bug reusing the primary code's config for the second code would have been invisible) and adds a DB round-trip check on `Code` values.

## Tasks & Acceptance

**Execution:**
- [x] `AttemptQuestion.cs`/`AttemptQuestionConfiguration.cs` -- add `Code`.
- [x] `StartAttemptCommand.cs` -- generalize the code-validity check and composition loop for combinations.
- [x] `AttemptDto.cs` -- expose `Code` on `AttemptQuestionDto`.
- [x] `Migrators.PostgreSQL` migration -- generate and apply.
- [x] `StartAttemptCommandHandlerTests.cs` -- add/extend tests for all 6 matrix rows.

**Acceptance Criteria:**
- Given a learner selects Code1+2 or Code1+3, when the attempt is composed, then Rules and Signs appear once and a separate VehicleControls module is included per selected code, in fixed order.
- Given an unsupported combination (Code2+3 or all-three), when it is requested, then the API rejects it as an invalid sitting.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter StartAttemptCommandHandlerTests` -- expected: all matrix-row tests pass.
