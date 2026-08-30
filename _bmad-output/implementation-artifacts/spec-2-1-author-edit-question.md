---
title: 'Author and edit a question'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: '7d87d9c1525bb8f805af1f42267997befa894d3e'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `Question`/`AnswerOption` exist only as domain/persistence scaffolding (Story 1.3) — a K53 domain expert has no way to author or edit a question in the Admin Panel.

**Approach:** Add a paged Question list page, an `AddEditQuestionCommand` (create+update via `Id`, mirroring `AddEditProductCommand`) that reconciles the `AnswerOptions` child collection on update, and a create/edit dialog with a novel add/remove answer-option repeater (no prior UI precedent in this codebase). Field-level validation (missing stem, zero applicable codes, wrong correct-answer count, unresolved `sign_ref`) runs via FluentValidation *before* `SaveChangesAsync`, complementing — not replacing — Story 1.3's `QuestionValidationInterceptor`, which still enforces the same invariants as a persistence-layer safety net.

## Boundaries & Constraints

**Always:**
- Follow the existing CQRS pattern exactly: command+handler in one file (mirror `AddEditProductCommand.cs`), separate validator file, `IApplicationDbContextFactory` per-op contexts, `ICacheInvalidatorRequest<Result<int>>`.
- The validator adds this codebase's *first* `MustAsync` rule (resolving `SignRef` against `RoadSigns`, via `IApplicationDbContextFactory` injected into the validator's constructor) — this is new pattern territory, not a deviation from one.
- Sync validator rules also reject: empty `Stem`; `Codes == LicenceCode.None`; `AnswerOptions.Count < 2`; not exactly one `AnswerOptions[i].IsCorrect == true`. Every rule attaches to a specific property so the UI can identify the failing field (this story's own AC), even though `QuestionValidationInterceptor` would also catch codes/correct-count/sign_ref at `SaveChangesAsync` as an unattributed exception.
- On update, reconcile `AnswerOptions` explicitly in the handler (match by `Id`, add new, remove missing) — do not attempt to let AutoMapper diff the collection. `Order` is always derived from the submitted list's array position, never taken from client input, to prevent duplicate/invalid `Order` values.
- Create/edit UI is a `MudDialog` opened via `DialogService.ShowAsync`, mirroring `ProductFormDialog.razor`'s structure (`MudForm` + `Mediator.Send` + `Result.Match`).
- Gate the page/commands with new `Permissions.Questions.{View,Create,Edit}`.

**Ask First:**
- None — the interceptor-vs-validator duplication was already pre-approved as an "Ask First" item in spec-1-3-question-content-model.md ("don't second-guess it now... duplicating it, not removing/relocating, is fine").

**Never:**
- No delete command — not required by this story's AC.
- No CSV/JSON import, test composition, or publish/unpublish (Stories 2.2-2.4).
- No changes to `Question`/`AnswerOption` schema or `QuestionValidationInterceptor` (Story 1.3, done) — this story only adds an Application/Server.UI layer on top; no new migration.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Create, valid | Stem, codes, section, 2+ options with one correct, resolvable `SignRef` | Saved; `Question` + `AnswerOption`s persisted, `Id` returned | N/A |
| Create, missing stem | Empty `Stem` | Rejected before `SaveChangesAsync`; nothing persisted | `ValidationException` on `Stem` |
| Create, no codes | `Codes = LicenceCode.None` | Rejected before commit; nothing persisted | `ValidationException` on `Codes` |
| Create, zero/multiple correct | 0 or 2+ options with `IsCorrect == true` | Rejected before commit; nothing persisted | `ValidationException` on `AnswerOptions` |
| Create, unresolved sign_ref | `SignRef` matches no `RoadSign` | Rejected before commit (async rule); nothing persisted | `ValidationException` on `SignRef` |
| Edit, reconciles options | Existing question; submitted list adds one option, removes another, edits a third | Added option inserted, removed option deleted, edited option updated — no duplicates, no orphans | N/A |
| Edit, validation fails | Existing question; edit introduces zero correct answers | Rejected before commit; original row and options unchanged | `ValidationException` on `AnswerOptions` |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs` -- New. Command (`Id`, `Stem`, `Codes`, `Section`, `LanguageCode`, `SignRef`, `List<AnswerOptionModel> AnswerOptions`) + handler, mirror `AddEditProductCommand.cs`'s `Id > 0` insert/update branch. Nested `AnswerOptionModel { Id, Text, IsCorrect, Order }` + AutoMapper profile (`CreateMap<AnswerOption, AnswerOptionModel>().ReverseMap()`; `CreateMap<AddEditQuestionCommand, Question>().ForMember(d => d.AnswerOptions, opt => opt.Ignore())` -- child list is reconciled manually, never AutoMapper-diffed).
- `src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommandValidator.cs` -- New. Sync rules for `Stem`/`Codes`/`AnswerOptions` count and correct-count; `RuleFor(x => x.SignRef).MustAsync(...)` -- this codebase's first async validator, constructor-injects `IApplicationDbContextFactory`.
- `src/K53Guru/src/Application/Features/Questions/DTOs/QuestionDto.cs` -- New. Projection: `Id`, `Stem`, `Codes`, `Section`, `LanguageCode`, `SignRef`, `AnswerOptions` (list of a small `AnswerOptionDto`).
- `src/K53Guru/src/Application/Features/Questions/Caching/QuestionCacheKey.cs` -- New. Mirror `RoadSignCacheKey.cs`.
- `src/K53Guru/src/Application/Features/Questions/Specifications/QuestionAdvancedSpecification.cs` -- New. Keyword search on `Stem` only (mirror `RoadSignAdvancedSpecification.cs`).
- `src/K53Guru/src/Application/Features/Questions/Queries/Pagination/QuestionsWithPaginationQuery.cs` -- New. Mirror `RoadSignsWithPaginationQuery.cs`.
- `src/K53Guru/src/Application/Features/Questions/Queries/GetById/GetQuestionByIdQuery.cs` -- New. Mirror `GetRoadSignByIdQuery.cs`, but the handler must `.Include(q => q.AnswerOptions)` (or project `AnswerOptions` explicitly) so the edit dialog gets the full child list.
- `src/K53Guru/src/Application/Features/Questions/Security/QuestionsPermissions.cs` -- New. `View`, `Create`, `Edit` (mirror `PicklistSetsPermissions.cs`'s multi-right shape, since this story writes, unlike Story 1.2's read-only `RoadSignsPermissions`).
- `src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor` -- New. `@page "/system/questions"`, paged `MudDataGrid` (no search box needed per this story's AC) + "Add" button + per-row edit button, mirror `RoadSigns.razor`'s grid structure.
- `src/K53Guru/src/Server.UI/Pages/Questions/Components/QuestionFormDialog.razor` -- New. Mirror `ProductFormDialog.razor`'s `MudForm`+`Mediator.Send`+`Result.Match` skeleton. New UI, no precedent: an `AnswerOptions` repeater -- one row per option (`MudTextField` for `Text`, a single `MudRadioGroup<int>` bound to "which index is correct" spanning all rows so exactly-one-correct is structurally enforced in the UI, a delete-row icon button), plus an "Add option" button appending a blank row.
- `src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs` -- Add a nav entry ("Questions", `/system/questions`) inside the existing admin System `MenuItems` list.
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Questions/AddEditQuestionCommandHandlerTests.cs` -- New. Covers all 7 matrix rows via a SQLite in-memory `ApplicationDbContext` (mirror `RoadSignsQueryHandlerTests.cs`), constructing the validator + handler directly and invoking them in sequence the way the MediatR pipeline would. Review fix: 3 additional tests for blank answer-option text (`[Theory]`, 2 cases) and out-of-range `Codes` bits.
- Review fix: `AddEditQuestionCommandHandler`'s edit branch now explicitly marks the parent `Question` entry `Modified` after `ReconcileAnswerOptions` runs, so `QuestionValidationInterceptor`'s safety net still fires when an edit changes only `AnswerOptions` (previously the parent stayed `Unchanged` and the interceptor's `Entries<Question>()` filter silently skipped it).
- Review fix: `AddEditQuestionCommandValidator.cs` gained a `RuleForEach` rejecting blank `AnswerOptions[i].Text`, and the `Codes` rule was tightened from `NotEqual(LicenceCode.None)` to also reject any bit outside `Code1|Code2|Code3`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/QuestionValidationInterceptorTests.cs` -- Review fix (Story 1.3 file). Added `ChildrenOnlyEdit_ParentExplicitlyMarkedModified_StillRejectedBeforeCommit`, proving the interceptor still catches an invalid `AnswerOptions` state when only the child collection changes on an already-persisted `Question`.

## Tasks & Acceptance

**Execution:**
- [x] `AddEditQuestionCommand.cs` -- create command+handler+`AnswerOptionModel` -- create/update entry point with explicit child-collection reconciliation.
- [x] `AddEditQuestionCommandValidator.cs` -- create validator -- field-level rejection incl. this codebase's first `MustAsync`.
- [x] `QuestionDto.cs`, `QuestionCacheKey.cs`, `QuestionAdvancedSpecification.cs` -- create supporting types.
- [x] `QuestionsWithPaginationQuery.cs`, `GetQuestionByIdQuery.cs` (+handlers) -- create list/detail queries powering the grid and edit dialog.
- [x] `QuestionsPermissions.cs` -- create `View`/`Create`/`Edit` permissions.
- [x] `Questions.razor`, `QuestionFormDialog.razor` -- create Admin Panel page + the new answer-option repeater UI.
- [x] `MenuService.cs` -- add nav entry.
- [x] `AddEditQuestionCommandHandlerTests.cs` -- add tests covering all 7 matrix rows.

**Acceptance Criteria:**
- Given I am authoring a new question, when I enter a stem, options (one marked correct), codes, section, and optionally a `sign_ref`, then it validates and saves.
- Given any validation rule fails, when I try to save, then the save is rejected, the failing field is identified in the UI, and nothing is partially persisted.
- Given an existing question, when I edit and save it, then the changes persist (including added/removed/edited options) without orphaning rows.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter AddEditQuestionCommandHandlerTests` -- expected: all 7 matrix-row tests pass.

**Manual checks (if no CLI):**
- Sign in as an Admin, navigate to `/system/questions`, create a question with 3 options (one correct), edit it to add a 4th option and remove the 2nd, confirm the grid reflects the final option count.

## Suggested Review Order

**Command + validator (the core of this story)**

- Entry point: field-level rejection running before `SaveChangesAsync`, including this codebase's first `MustAsync` rule and the two review-added rules (blank option text, out-of-range `Codes`).
  [`AddEditQuestionCommandValidator.cs:10`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommandValidator.cs#L10)

- Review fix: tightened `Codes` guard now rejects any bit outside `Code1|Code2|Code3`, not just `None`.
  [`AddEditQuestionCommandValidator.cs:28`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommandValidator.cs#L28)

- Review fix: rejects blank answer-option text server-side, closing a gap only the client-side `Required` attribute previously covered.
  [`AddEditQuestionCommandValidator.cs:39`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommandValidator.cs#L39)

- Create/update handler; `Id > 0` branch mirrors `AddEditProductCommand.cs` exactly.
  [`AddEditQuestionCommand.cs:56`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs#L56)

- Explicit child-collection reconciliation (match by `Id`, add/update/remove) -- the first genuine parent+child-entity command in this codebase.
  [`AddEditQuestionCommand.cs:111`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs#L111)

- Review fix: forces the parent `Question` to `Modified` after reconciling children, so `QuestionValidationInterceptor` still fires on children-only edits -- this is the fix for the review's most significant finding.
  [`AddEditQuestionCommand.cs:88`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs#L88)

**Admin Panel UI**

- The novel answer-option repeater: a single `MudRadioGroup` spanning all rows structurally enforces "exactly one correct" in the UI itself.
  [`QuestionFormDialog.razor:57`](../../src/K53Guru/src/Server.UI/Pages/Questions/Components/QuestionFormDialog.razor#L57)

- Add/remove row handlers -- no prior precedent in this codebase for editing a variable-length child list.
  [`QuestionFormDialog.razor:144`](../../src/K53Guru/src/Server.UI/Pages/Questions/Components/QuestionFormDialog.razor#L144)

- List page entry points into the dialog (create vs. edit, the latter round-tripping `AnswerOptions` from `GetQuestionByIdQuery`).
  [`Questions.razor:136`](../../src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor#L136)

- New permissions gating the page/actions.
  [`QuestionsPermissions.cs:12`](../../src/K53Guru/src/Application/Features/Questions/Security/QuestionsPermissions.cs#L12)

- Nav entry making the page reachable from admin System menu.
  [`MenuService.cs:136`](../../src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs#L136)

**Test coverage (peripherals)**

- All 7 matrix rows plus 3 review-added cases (blank option text x2, out-of-range `Codes`), against a SQLite in-memory context.
  [`AddEditQuestionCommandHandlerTests.cs:41`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Questions/AddEditQuestionCommandHandlerTests.cs#L41)

- Review fix: proves the interceptor still rejects an invalid `AnswerOptions` state on a children-only edit, closing the gap fixed above.
  [`QuestionValidationInterceptorTests.cs:198`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/QuestionValidationInterceptorTests.cs#L198)
