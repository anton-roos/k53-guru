---
title: 'Publish and unpublish a test'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: '9ea6706e6c44902080686eac4cdf96adf1398003'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Test.Status` (Story 2.2) can only ever be `Draft` — nothing transitions it to `Published`, so no test can ever become eligible for future learner-facing delivery.

**Approach:** Add `PublishTestCommand`/`UnpublishTestCommand` (two small, single-purpose commands — no bidirectional toggle command, no precedent for one in this codebase) that flip `Test.Status` with a handler-level state guard (mirroring `AddEditTestCommandHandler`'s existing "not found" `Result.FailureAsync` idiom, not FluentValidation, since this is a business-state check rather than a field-format rule). A new icon-button action in `Tests.razor`'s existing grid — no dialog, immediate action — triggers each.

## Boundaries & Constraints

**Always:**
- `PublishTestCommand`/`UnpublishTestCommand` each take only `Id`; both implement `ICacheInvalidatorRequest<Result>` mirroring `AddEditTestCommand`'s `TestCacheKey.GetAllCacheKey`/`Tags`.
- State guards live in the handler as `Result.FailureAsync` checks (not a `FluentValidation` rule) — mirrors how `AddEditTestCommandHandler` already handles its "not found" case; this codebase reserves `AbstractValidator` for field-format rules, not business-state guards.
- `PublishTestCommand` rejects (via `Result.FailureAsync`, no partial state change) a `Test` that is already `Published`; `UnpublishTestCommand` rejects one that is already `Draft`. Both reject a nonexistent `Id` the same way `AddEditTestCommandHandler`'s edit branch does.
- UI: a single icon button per row in `Tests.razor`'s existing Actions column (mirror `Users.razor`'s click-to-toggle pattern combined with `DeleteConfirmation.razor`'s `Mediator.Send` + `Result.Match` + `Snackbar` wiring) — no confirmation dialog, no new page. Gated by a new `Permissions.Tests.Publish` right, distinct from `Edit`.
- A `Test` is publishable/unpublishable exactly as persisted — `AddEditTestCommand` (Story 2.2) already guarantees a non-empty `Codes`/`Sections`/question-membership at creation time, so no separate "completeness" check is added here; every existing `Test` row already satisfies whatever "complete" means by construction.

**Ask First:**
- None.

**Never:**
- No learner-facing API or query filtering by `Status` — that API doesn't exist yet (Epic 3). This story only flips the data-level flag; the AC's "eligible for API delivery" / "never returned to the learner API" clauses describe an invariant Epic 3's future queries must respect, not something buildable now.
- No `Attempt`/snapshot handling — "in-progress attempts unaffected" is already satisfied by the architecture's immutable-snapshot design (Epic 3, not built); nothing to add here.
- No changes to `Test`/`TestQuestion` schema, `AddEditTestCommand`, or `TestQuestionGrouping` (Story 2.2, done) — no new migration.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Publish, valid | `Test.Status == Draft` | `Status` becomes `Published` | N/A |
| Publish, already published | `Test.Status == Published` | Rejected; status unchanged | `Result.Failure`, clear "already published" message |
| Publish, not found | Nonexistent `Id` | Rejected | `Result.Failure`, "not found" message |
| Unpublish, valid | `Test.Status == Published` | `Status` becomes `Draft` | N/A |
| Unpublish, already draft | `Test.Status == Draft` | Rejected; status unchanged | `Result.Failure`, clear "already draft" message |
| Unpublish, not found | Nonexistent `Id` | Rejected | `Result.Failure`, "not found" message |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Application/Features/Tests/Commands/Publish/PublishTestCommand.cs` -- New. `{ Id }` + handler: load by id (`Result.FailureAsync` if missing), reject if already `Published` (`Result.FailureAsync`), else set `Status = TestStatus.Published` and `SaveChangesAsync`.
- `src/K53Guru/src/Application/Features/Tests/Commands/Unpublish/UnpublishTestCommand.cs` -- New. Mirror image of the above for the `Published -> Draft` direction.
- `src/K53Guru/src/Application/Features/Tests/Security/TestsPermissions.cs` -- Modify (Story 2.2 file). Add `Publish` const + `TestsAccessRights.Publish`.
- `src/K53Guru/src/Server.UI/Pages/Tests/Tests.razor` -- Modify (Story 2.2 file). Add a Publish/Unpublish `MudIconButton` to the existing Actions `TemplateColumn`, icon/tooltip driven by `context.Item.Status`, gated by `_accessRights.Publish`; on click, `Mediator.Send` the appropriate command, `Result.Match` -> `Snackbar` + `_testsGrid.ReloadServerData()`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/PublishUnpublishTestCommandHandlerTests.cs` -- New. Covers all 6 matrix rows via SQLite in-memory, mirroring `AddEditTestCommandHandlerTests.cs`'s harness.
- `src/K53Guru/.gitignore` -- Modify (unplanned, discovered mid-implementation). Anchored the pre-existing `publish/` Click-Once rule to `/publish/` -- unanchored, it silently matched the new `Commands/Publish/` source directory, which the implementer had to `git add -f` around.
- Review fix: `Tests.razor`'s Publish/Unpublish buttons gained a `_actionInProgress`-driven `Disabled` guard (mirroring the file's own `Refresh` button convention) to prevent a rapid double-click firing the command twice.

## Tasks & Acceptance

**Execution:**
- [x] `PublishTestCommand.cs` (+handler) -- create publish command with the already-published guard.
- [x] `UnpublishTestCommand.cs` (+handler) -- create unpublish command with the already-draft guard.
- [x] `TestsPermissions.cs` -- add `Publish` permission/access right.
- [x] `Tests.razor` -- add the publish/unpublish action button.
- [x] `PublishUnpublishTestCommandHandlerTests.cs` -- add tests covering all 6 matrix rows.

**Acceptance Criteria:**
- Given a draft test, when I publish it, then its status becomes `Published`.
- Given a published test, when I unpublish it, then its status becomes `Draft`.
- Given a test already in the target state, when I try to publish/unpublish it again, then the action is rejected with a clear message and the status is unchanged.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter PublishUnpublishTestCommandHandlerTests` -- expected: all 6 matrix-row tests pass.

**Manual checks (if no CLI):**
- Sign in as an Admin, navigate to `/system/tests`, publish a draft test, confirm its Status column updates to `Published`, then unpublish it back to `Draft`.

## Suggested Review Order

**Commands (the core of this story)**

- Entry point: load, guard against already-published, flip, save.
  [`PublishTestCommand.cs:14`](../../src/K53Guru/src/Application/Features/Tests/Commands/Publish/PublishTestCommand.cs#L14)

- The state guard itself -- a `Result.FailureAsync` check, not a validator, per this codebase's field-rule-vs-business-rule split.
  [`PublishTestCommand.cs:30`](../../src/K53Guru/src/Application/Features/Tests/Commands/Publish/PublishTestCommand.cs#L30)

- Mirror image for the `Published -> Draft` direction.
  [`UnpublishTestCommand.cs:6`](../../src/K53Guru/src/Application/Features/Tests/Commands/Unpublish/UnpublishTestCommand.cs#L6)

- New permission distinct from `Edit`.
  [`TestsPermissions.cs:21`](../../src/K53Guru/src/Application/Features/Tests/Security/TestsPermissions.cs#L21)

**Admin Panel UI**

- Status-driven icon button dispatching each command; no dialog.
  [`Tests.razor:185`](../../src/K53Guru/src/Server.UI/Pages/Tests/Tests.razor#L185)

- Review fix: `_actionInProgress` guard preventing a rapid double-click from firing the command twice.
  [`Tests.razor:113`](../../src/K53Guru/src/Server.UI/Pages/Tests/Tests.razor#L113)

**Repo mechanics (unplanned, discovered mid-implementation)**

- The pre-existing `publish/` Click-Once gitignore rule was silently matching the new `Commands/Publish/` source directory -- anchored to repo root.
  [`.gitignore:175`](../../src/K53Guru/.gitignore#L175)

**Test coverage (peripherals)**

- All 6 matrix rows against a SQLite in-memory context, invoking the real handlers directly.
  [`PublishUnpublishTestCommandHandlerTests.cs:31`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/PublishUnpublishTestCommandHandlerTests.cs#L31)
