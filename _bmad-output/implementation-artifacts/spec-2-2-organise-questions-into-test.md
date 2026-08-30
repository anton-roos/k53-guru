---
title: 'Organise questions into a test'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: '2a619d5b7bd4c046b15245072444936a221c9dd3'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Authored `Question`s (Story 2.1) exist only individually — there is no way to group them into a named, code/section-scoped content set the API can later compose sittings from.

**Approach:** Add a `Test` entity (name, a `LicenceCode` scope, a new `TestSectionScope` flags scope, a `Draft`/`Published` status defaulting to `Draft`) and a `TestQuestion` join entity, following the exact same CQRS+EF shape as Story 2.1's `Question`/`AnswerOption`. An `AddEditTestCommand` reconciles the question-membership join on edit; a `GetTestByIdQuery` returns the associated questions flat, grouped/counted client-side for display. `Test.Status` exists now (defaulting `Draft`) so Story 2.3 needs no schema change — but this story never sets it to anything else.

## Boundaries & Constraints

**Always:**
- `Test.Codes` is `LicenceCode` (reused from Story 1.3, same `[Flags]` semantics). `Test.Sections` is a **new** `[Flags] TestSectionScope { None=0, Rules=1, Signs=2, VehicleControls=4 }` — never repurpose `Question`'s existing plain `SectionType` (it's not a flags enum and is already migrated; changing it would be a breaking schema edit outside this story).
- `Test` is distinct from the future `TestConfig`/`SectionRule` entities (Epic 3, per-code pass-mark/timing configuration) — do not conflate the two; this story's `Test` is a curated content grouping, nothing more.
- `TestQuestion` is a real join entity (own table, `TestId`+`QuestionId`, unique index on the pair, FK to both `Test` and `Question`), mirroring `AnswerOption`'s genuine-relational-child precedent from Story 1.3/2.1 — never a JSON blob.
- Follow the exact CQRS pattern from Story 2.1: command+handler in one file, separate validator, `IApplicationDbContextFactory`, `ICacheInvalidatorRequest<Result<int>>`; reconcile `TestQuestions` explicitly in the handler (diff submitted `QuestionId`s against the tracked join rows — add missing, remove extra), never AutoMapper-diffed.
- Editing an existing `Test` must never change its `Status` — this command only ever sets `Status = TestStatus.Draft` on create and leaves it untouched on update (publish/unpublish is Story 2.3's command, not built here).
- Add `GetAllQuestionsQuery` (mirror `GetAllPicklistSetsQuery.cs`) to populate the question multi-select — reuses `QuestionCacheKey.GetAllCacheKey`, defined in Story 2.1 but never consumed until now.
- Gate the page/commands with new `Permissions.Tests.{View,Create,Edit}`.

**Ask First:**
- None.

**Never:**
- No publish/unpublish action or `Status` transitions (Story 2.3).
- No CSV/JSON import (Story 2.4).
- No changes to `Question`/`AnswerOption` schema, `QuestionValidationInterceptor`, or `AddEditQuestionCommand` (Story 1.3/2.1, done).
- No server-side pre-computed per-section/per-code count DTO — the associated-questions list is returned flat; grouping/counting for display is plain LINQ in the Razor code-behind, not a new query concern.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Create, valid | Name, codes, sections, 1+ question ids | Saved as `Draft`; retrievable with its associated questions | N/A |
| Create, missing name | Empty `Name` | Rejected before `SaveChangesAsync`; nothing persisted | `ValidationException` on `Name` |
| Create, no codes | `Codes = LicenceCode.None` | Rejected before commit; nothing persisted | `ValidationException` on `Codes` |
| Create, no sections | `Sections = TestSectionScope.None` | Rejected before commit; nothing persisted | `ValidationException` on `Sections` |
| Create, zero questions | Empty question-id list | Rejected before commit; nothing persisted | `ValidationException` on question ids |
| Edit, reconciles questions | Existing test; submitted id list adds one question, removes another | Join table reflects final membership exactly — no duplicates, no orphans | N/A |
| View, grouped counts | Test with questions across Rules/Signs, Code1/Code2 | Per-section question counts and per-code counts within each section are correct | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Enums/TestStatus.cs` -- New. `enum TestStatus { Draft, Published }`.
- `src/K53Guru/src/Domain/Enums/TestSectionScope.cs` -- New. `[Flags] enum TestSectionScope { None = 0, Rules = 1, Signs = 2, VehicleControls = 4 }`.
- `src/K53Guru/src/Domain/Entities/Test.cs` -- New. `BaseAuditableEntity` + `Name`, `Codes` (`LicenceCode`), `Sections` (`TestSectionScope`), `Status` (`TestStatus`, default `Draft`), `TestQuestions` (`List<TestQuestion>`).
- `src/K53Guru/src/Domain/Entities/TestQuestion.cs` -- New. `BaseAuditableEntity` + `TestId` (FK), `QuestionId` (FK), `Question` (navigation, required for the grouped-view projection).
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/TestConfiguration.cs` -- New. Mirror `QuestionConfiguration.cs`: required/max-length `Name`, `HasConversion<string>()` on `Codes`/`Sections`/`Status`, `HasMany(TestQuestions).WithOne().HasForeignKey(TestId)`, ignore `DomainEvents`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/TestQuestionConfiguration.cs` -- New. `HasOne(t => t.Question)` required; unique index on `(TestId, QuestionId)`; ignore `DomainEvents`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` + `IApplicationDbContext.cs` -- Add `DbSet<Test> Tests` and `DbSet<TestQuestion> TestQuestions` to **both**.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- New migration via `dotnet ef migrations add AddTestContentGrouping`.
- `src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs` -- New. Command (`Id`, `Name`, `Codes`, `Sections`, `List<int> QuestionIds`) + handler mirroring `AddEditQuestionCommand.cs`'s shape; reconciles `TestQuestions` by diffing `QuestionIds` against tracked join rows; sets `Status = TestStatus.Draft` only on create, and explicitly re-marks the parent `Test` `Modified` after reconciling the join (same fix Story 2.1 needed for `Question`/`AnswerOptions`).
- `src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommandValidator.cs` -- New. Sync rules: `Name` not empty, `Codes != LicenceCode.None`, `Sections != TestSectionScope.None`, `QuestionIds` not empty.
- `src/K53Guru/src/Application/Features/Tests/DTOs/TestDto.cs` -- New. `Id`, `Name`, `Codes`, `Sections`, `Status`, `Questions` (flat `List<TestQuestionSummaryDto>` with `Id`, `Stem`, `Codes`, `Section`).
- `src/K53Guru/src/Application/Features/Tests/Caching/TestCacheKey.cs` -- New. Mirror `QuestionCacheKey.cs`.
- `src/K53Guru/src/Application/Features/Tests/Specifications/TestAdvancedSpecification.cs`, `TestByIdSpecification.cs` -- New. Mirror `QuestionAdvancedSpecification.cs`/`QuestionByIdSpecification.cs`; the by-id spec includes `TestQuestions.Question`.
- `src/K53Guru/src/Application/Features/Tests/Queries/Pagination/TestsWithPaginationQuery.cs`, `Queries/GetById/GetTestByIdQuery.cs` -- New. Mirror `QuestionsWithPaginationQuery.cs`/`GetQuestionByIdQuery.cs`.
- `src/K53Guru/src/Application/Features/Questions/Queries/GetAll/GetAllQuestionsQuery.cs` -- New. Mirror `GetAllPicklistSetsQuery.cs` exactly; populates the question multi-select.
- `src/K53Guru/src/Application/Features/Tests/Security/TestsPermissions.cs` -- New. `View`, `Create`, `Edit` (mirror `QuestionsPermissions.cs`).
- `src/K53Guru/src/Server.UI/Pages/Tests/Tests.razor` -- New. Mirror `Questions.razor`'s grid structure.
- `src/K53Guru/src/Server.UI/Pages/Tests/Components/TestFormDialog.razor` -- New. Mirror `QuestionFormDialog.razor`'s skeleton; a `MudSelect T="int" MultiSelection="true"` over `GetAllQuestionsQuery`'s results for question membership, plus a read-only panel grouping the currently-associated questions by `Section` (via a Razor-code-behind `GroupBy`) with per-code counts within each group -- satisfies the "view" AC inside the same dialog rather than a separate screen.
- `src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs` -- Add a nav entry ("Tests", `/system/tests`).
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/AddEditTestCommandHandlerTests.cs` -- New. Covers all 7 matrix rows via SQLite in-memory, mirroring `AddEditQuestionCommandHandlerTests.cs`. Review fix: 3 additional tests (nonexistent question id, out-of-range `Codes`/`Sections` bits), and the "View, grouped counts" test now calls the real `TestQuestionGrouping` helper instead of re-implementing the grouping logic itself.
- `src/K53Guru/src/Application/Features/Tests/TestQuestionGrouping.cs` -- New (review fix). Pure static helper (`GroupBySectionWithCodeCounts`) extracted from `TestFormDialog.razor`'s inline `GroupBy`/`CountByCode` logic, so the grouped/counted "view" behavior is testable independent of the Razor component. Both the dialog and the test now call this same code.
- Review fix: `AddEditTestCommandValidator.cs` gained a `MustAsync` rule (constructor now takes `IApplicationDbContextFactory`) confirming every submitted `QuestionId` references a real `Question`, mirroring Story 2.1's `SignRef` resolution pattern.

## Tasks & Acceptance

**Execution:**
- [x] `TestStatus.cs`, `TestSectionScope.cs` -- create enums.
- [x] `Test.cs`, `TestQuestion.cs` -- create entities.
- [x] `TestConfiguration.cs`, `TestQuestionConfiguration.cs` -- create EF configs incl. unique `(TestId, QuestionId)` index.
- [x] `ApplicationDbContext.cs`, `IApplicationDbContext.cs` -- add `DbSet`s for both entities.
- [x] `Migrators.PostgreSQL/` -- generate `AddTestContentGrouping` migration.
- [x] `AddEditTestCommand.cs` (+handler) -- create/update with explicit `TestQuestions` reconciliation.
- [x] `AddEditTestCommandValidator.cs` -- create validator.
- [x] `TestDto.cs`, `TestCacheKey.cs`, `TestAdvancedSpecification.cs`, `TestByIdSpecification.cs` -- create supporting types.
- [x] `TestsWithPaginationQuery.cs`, `GetTestByIdQuery.cs`, `GetAllQuestionsQuery.cs` (+handlers) -- create list/detail/lookup queries.
- [x] `TestsPermissions.cs` -- create `View`/`Create`/`Edit` permissions.
- [x] `Tests.razor`, `TestFormDialog.razor` -- create Admin Panel page + question-membership multi-select + grouped view panel.
- [x] `MenuService.cs` -- add nav entry.
- [x] `AddEditTestCommandHandlerTests.cs` -- add tests covering all 7 matrix rows.

**Acceptance Criteria:**
- Given authored questions exist, when I create a test and assign its code scope, sections, and questions, then the test is persisted with its associated questions and is retrievable in the Admin Panel.
- Given a test, when I view it, then I see its questions grouped by section (Rules -> Signs -> Controls) with counts per section and code.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter AddEditTestCommandHandlerTests` -- expected: all 7 matrix-row tests pass.
- `dotnet ef migrations list -p src/K53Guru/src/Migrators/Migrators.PostgreSQL -s src/K53Guru/src/Server.UI` -- expected: `AddTestContentGrouping` appears in the list.

**Manual checks (if no CLI):**
- Sign in as an Admin, navigate to `/system/tests`, create a test spanning two sections and two codes with several questions, reopen it, and confirm the grouped section/code counts match what was selected.

## Suggested Review Order

**Domain model**

- Entry point: the curated content-grouping shape, deliberately distinct from the future `TestConfig` scoring-config entity.
  [`Test.cs:6`](../../src/K53Guru/src/Domain/Entities/Test.cs#L6)

- Real join entity to `Question` -- the first genuine many-to-many-via-join-table relationship in this codebase.
  [`TestQuestion.cs:5`](../../src/K53Guru/src/Domain/Entities/TestQuestion.cs#L5)

- New flags enum for multi-section scope, kept separate from `Question`'s plain `SectionType` to avoid a breaking schema edit.
  [`TestSectionScope.cs:10`](../../src/K53Guru/src/Domain/Enums/TestSectionScope.cs#L10)

- Publication state that exists now (defaulting `Draft`) but is never transitioned by this story.
  [`TestStatus.cs:7`](../../src/K53Guru/src/Domain/Enums/TestStatus.cs#L7)

**Command + validator**

- Field-level rejection, including the review-added `MustAsync` existence check on `QuestionIds` and the tightened `Codes`/`Sections` bit-range guards.
  [`AddEditTestCommandValidator.cs:9`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommandValidator.cs#L9)

- Review fix: confirms every submitted `QuestionId` references a real `Question` before save, mirroring Story 2.1's `SignRef` resolution pattern.
  [`AddEditTestCommandValidator.cs:47`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommandValidator.cs#L47)

- Create/update handler.
  [`AddEditTestCommand.cs:36`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs#L36)

- Explicit `TestQuestions` join reconciliation (add/remove by diffing submitted ids).
  [`AddEditTestCommand.cs:96`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs#L96)

- Forces the parent `Test` to `Modified` after reconciling the join, same fix Story 2.1 needed for `Question`/`AnswerOptions`.
  [`AddEditTestCommand.cs:70`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs#L70)

**Grouped-view logic (review-extracted)**

- Review fix: pure helper pulled out of the Razor component specifically so the "view grouped by section/code" behavior is directly testable, not just re-implemented in a test.
  [`TestQuestionGrouping.cs:15`](../../src/K53Guru/src/Application/Features/Tests/TestQuestionGrouping.cs#L15)

- The dialog now calls that same helper rather than inlining the grouping logic.
  [`TestFormDialog.razor:69`](../../src/K53Guru/src/Server.UI/Pages/Tests/Components/TestFormDialog.razor#L69)

**Persistence & UI**

- Unique `(TestId, QuestionId)` index -- prevents duplicate membership rows at the DB level.
  [`TestQuestionConfiguration.cs:17`](../../src/K53Guru/src/Infrastructure/Persistence/Configurations/TestQuestionConfiguration.cs#L17)

- List page + question multi-select entry points.
  [`Tests.razor:136`](../../src/K53Guru/src/Server.UI/Pages/Tests/Tests.razor#L136)

- Nav entry.
  [`MenuService.cs:142`](../../src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs#L142)

**Test coverage (peripherals)**

- All 7 matrix rows plus 3 review-added cases, and the grouped-counts test now calling the real shipped helper.
  [`AddEditTestCommandHandlerTests.cs:47`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/AddEditTestCommandHandlerTests.cs#L47)

- Review fix: proves nonexistent question ids are rejected before save.
  [`AddEditTestCommandHandlerTests.cs:260`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/AddEditTestCommandHandlerTests.cs#L260)

- Review fix: the "View, grouped counts" test now asserts against `TestQuestionGrouping`'s real output.
  [`AddEditTestCommandHandlerTests.cs:331`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/AddEditTestCommandHandlerTests.cs#L331)
