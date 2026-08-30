---
title: 'Import a question bank via CSV/JSON with reject-on-error validation'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: 'fe0f585cd0e89a39c9ccf6889f1a9e855548a64d'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** Every `Question` must currently be hand-entered one at a time via Story 2.1's dialog — there is no way to stand up a bank quickly from an existing file.

**Approach:** Add `ImportQuestionsCommand` (CSV or JSON, detected by file extension) that parses the whole file into row DTOs, validates **every** row through the *existing* `AddEditQuestionCommandValidator` (injected as `IValidator<AddEditQuestionCommand>` — FluentValidation's DI registration makes this the exact same validator Story 2.1 built, zero duplicated rules) before persisting anything, and only if every row passes does it build and save all `Question`+`AnswerOption` entities in one `SaveChangesAsync`. A `CreateQuestionsImportTemplateCommand` returns a downloadable CSV or JSON template.

## Boundaries & Constraints

**Always:**
- CSV is genuinely new parsing territory — `IExcelService` (the codebase's existing import mechanism) is ClosedXML/.xlsx-only and cannot read `.csv`. Add `CsvHelper` (industry-standard, MIT-licensed) as a new package reference to `Application` rather than a hand-rolled comma-splitter, which would silently corrupt any field containing a comma (e.g. a `Stem` like `"Yield, then proceed"`).
- CSV's flat row shape caps answer options at 4 via fixed columns (`Option1Text`/`Option1Correct` .. `Option4Text`/`Option4Correct`, blank for unused slots); `Codes` is a `;`-delimited list within one cell (e.g. `Code1;Code2` — semicolon, not comma, to avoid delimiter collision). JSON has no such cap: `answerOptions` is a plain array, `codes` a plain string array.
- Validation is two-phase and file-wide, matching "no partial content is stored": **phase 1** parses and validates every row (collecting every failure, not just the first), building an `AddEditQuestionCommand`-shaped object per row and running it through `IValidator<AddEditQuestionCommand>`; if **any** row fails, return `Result.Failure` with one `"Row {position}: {message}"` string per failure and touch the `DbContext` not at all. **Phase 2** (only reached if phase 1 found zero failures) builds every `Question`+`AnswerOption` entity and calls `SaveChangesAsync` exactly once.
- A per-row parse exception (malformed CSV row, invalid JSON element) is caught and folded into the same failure list as `"Row {position}: {parse error}"` — parsing continues to the next row rather than aborting the whole read on the first bad one, so every offending row is identified in one pass.
- Reuses the exact entity-building logic shape from `AddEditQuestionCommandHandler`'s create branch (`Order` always derived from array position, never from imported data) — mirrored inline in the new handler, not extracted into a shared method (matches this codebase's established preference for small, explicit duplication over premature abstraction, e.g. `PublishTestCommand`/`UnpublishTestCommand`).
- Reusing `AddEditQuestionCommandValidator` per row means its `MustAsync` `SignRef` rule issues one DB round-trip per row — this is the exact N+1 pattern already flagged in `spec-1-3-question-content-model.md`'s deferred-work entry as relevant to this story. Consciously accepted for v1's expected import volumes (tens to low hundreds of rows, "stand up a bank quickly") rather than batch-optimized now; not silently repeated.
- `CreateQuestionsImportTemplateCommand` takes a `Format` string (`"csv"`/`"json"`); returns `Result<byte[]>`.
- UI: `Questions.razor` gains an import button (`MudFileUpload`, `Accept=".csv,.json"`, mirroring `Products.razor`'s `OnFileImport`) and a template-download button (mirroring the existing `BlazorDownloadFileService` pattern used for exports elsewhere, since no page in this codebase currently wires up template download despite the command shape already existing for other features). Errors surface as one `Snackbar` per failure string, matching every existing import feature's convention — no new dialog/results-table component.

**Ask First:**
- None.

**Never:**
- No changes to `AddEditQuestionCommandValidator`, `AddEditQuestionCommand`, or `QuestionValidationInterceptor` (Story 2.1/1.3, done) — this story only calls the existing validator, never modifies it.
- No `.xlsx`/`IExcelService` support for this feature — the AC asks for CSV/JSON specifically, not Excel.
- No changes to `Test`/`TestQuestion` (Story 2.2/2.3, done).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Import, valid CSV | Well-formed CSV, 2 rows | Both rows persisted as `Question`+`AnswerOption`s | N/A |
| Import, valid JSON | Well-formed JSON, 2 rows | Both rows persisted | N/A |
| Import, missing required field | CSV/JSON with one row's `Stem` empty | Rejected; nothing persisted | Failure list identifies the row by position and reason |
| Import, unresolved sign_ref | One row's `SignRef` matches no `RoadSign` | Rejected; nothing persisted | Failure list identifies the row and "unresolved" reason |
| Import, wrong correct-answer count | One row has 0 or 2+ correct options | Rejected; nothing persisted | Failure list identifies the row and reason |
| Request CSV template | N/A | Non-empty CSV `byte[]` with the documented headers | N/A |
| Request JSON template | N/A | Non-empty, valid JSON `byte[]` matching the documented shape | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Application/Application.csproj` -- Add `CsvHelper` package reference.
- `src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs` -- New. `{ FileName, Data (byte[]) }` + handler: detects CSV vs JSON by `FileName` extension, parses to row DTOs (catching per-row parse exceptions into the failure list), phase-1 validates every row via injected `IValidator<AddEditQuestionCommand>`, phase-2 builds+saves only if zero failures.
- `src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommandValidator.cs` -- New. Mirror `ImportPicklistSetsCommandValidator.cs`: `Data` not null/empty.
- `src/K53Guru/src/Application/Features/Questions/Commands/Import/CreateQuestionsImportTemplateCommand.cs` -- New. `{ Format }` + handler returning a CSV or JSON template `byte[]` for the row shape above.
- `src/K53Guru/src/Application/Features/Questions/Commands/Import/QuestionImportRow.cs` -- New. Shared row DTO (`Stem`, `Codes` as `List<string>`, `Section`, `LanguageCode`, `SignRef`, `List<(string Text, bool IsCorrect)> AnswerOptions`) that both the CSV and JSON parsers produce, so phase-1/phase-2 logic is format-agnostic past the parse step.
- `src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor` -- Modify (Story 2.1 file). Add an import `MudFileUpload` (`Accept=".csv,.json"`) mirroring `Products.razor`'s `OnFileImport`, and a template-download button (`Mediator.Send(new CreateQuestionsImportTemplateCommand{...})` -> `BlazorDownloadFileService.DownloadFileAsync`). Gated by the existing `Permissions.Questions.Create` right (import is a bulk-create action).
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Questions/ImportQuestionsCommandHandlerTests.cs` -- New. Covers all 7 matrix rows via SQLite in-memory, mirroring `AddEditQuestionCommandHandlerTests.cs`'s harness.
- Review fix: `Questions.razor`'s `OnFileImport`/`OnDownloadTemplate` gained `try/finally` around their bodies so `_uploading`/`_downloadingTemplate` reset even if the send/download throws (mirroring `PicklistSets.razor`'s `OnImportData`).
- Review fix: `ImportQuestionsCommand.cs`'s handler gained an empty-parse guard (header-only CSV / JSON `[]` now rejected as "No rows found to import." instead of silently "succeeding" with zero rows), a try/catch around the CSV header read (previously only per-row reads were caught), and an `Enum.IsDefined` check on `Section` (previously an out-of-range numeric string like `"99"` parsed successfully into an undefined enum value).
- Review fix: `ImportQuestionsCommandValidator.cs` gained a `FileName` `NotEmpty()` rule (previously only `Data` was validated; a null/empty `FileName` could NRE inside the handler instead of failing cleanly).
- Review fix: `CreateQuestionsImportTemplateCommand.cs`'s `BuildCsvTemplate()` was rewritten so the example data row is keyed by the same `QuestionImportCsvColumns` constants that drive the header row and written by iterating `AllHeaders()`, closing a drift risk where the header list and hand-written positional data row could silently diverge (caught by the verification-gap review layer).
- Review fix: `ImportQuestionsCommandHandlerTests.cs` gained a template round-trip test (generates the CSV template, feeds it back through the real import handler, asserts success) and an unsupported-file-extension test.

## Tasks & Acceptance

**Execution:**
- [x] Add `CsvHelper` package reference to `Application.csproj`.
- [x] `QuestionImportRow.cs` -- create the shared, format-agnostic row DTO.
- [x] `ImportQuestionsCommand.cs` (+handler) -- create the two-phase import command with CSV and JSON parsing.
- [x] `ImportQuestionsCommandValidator.cs` -- create the shallow command-level validator.
- [x] `CreateQuestionsImportTemplateCommand.cs` (+handler) -- create the template-download command for both formats.
- [x] `Questions.razor` -- add the import upload control and template-download button.
- [x] `ImportQuestionsCommandHandlerTests.cs` -- add tests covering all 7 matrix rows.

**Acceptance Criteria:**
- Given a well-formed CSV or JSON file, when I import it, then every row is ingested with options and correct answers intact, indistinguishable from a hand-authored question.
- Given a file with an invalid row, when I import it, then the import is rejected, the offending row is identified by position and reason, and nothing is persisted.
- Given I request a template, then a downloadable CSV or JSON template matching the documented row shape is provided.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter ImportQuestionsCommandHandlerTests` -- expected: all 7 matrix-row tests pass.

**Manual checks (if no CLI):**
- Sign in as an Admin, navigate to `/system/questions`, download the CSV template, fill in two valid rows and one invalid row, import it, confirm the whole import is rejected with a per-row error message, fix the invalid row, re-import, confirm both questions now appear in the grid.

## Suggested Review Order

**Commands (the core of this story)**

- Entry point: extension-based format dispatch, empty-parse guard, two-phase validate-then-save.
  [`ImportQuestionsCommand.cs:63`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L63)

- Review fix: empty file (header-only CSV / JSON `[]`) now rejected instead of silently "succeeding" with zero rows.
  [`ImportQuestionsCommand.cs:84`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L84)

- Row-to-command mapping, reusing `AddEditQuestionCommandValidator` per row via DI.
  [`ImportQuestionsCommand.cs:147`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L147)

- Review fix: `Enum.IsDefined` guard against an out-of-range numeric `Section` value silently parsing.
  [`ImportQuestionsCommand.cs:156`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L156)

- CSV parsing: manual `Read`/`ReadHeader`, now try/catch-wrapped (review fix), then per-row parse-exception isolation.
  [`ImportQuestionsCommand.cs:199`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L199)

- Review fix: header-read failure now folds into the failure list instead of an unhandled exception.
  [`ImportQuestionsCommand.cs:224`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L224)

- JSON parsing counterpart: array-of-elements, per-element try/catch.
  [`ImportQuestionsCommand.cs:310`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L310)

- Review fix (verification-gap finding): template's example data row now keyed by the same header constants that drive the header row, closing the header/data drift risk.
  [`CreateQuestionsImportTemplateCommand.cs:32`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/CreateQuestionsImportTemplateCommand.cs#L32)

- Review fix: `FileName` now validated `NotEmpty()` alongside `Data`.
  [`ImportQuestionsCommandValidator.cs:12`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommandValidator.cs#L12)

**Admin Panel UI**

- Import upload + template-download menu, gated by `Permissions.Questions.Create`.
  [`Questions.razor:50`](../../src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor#L50)

- Review fix: `try/finally` around `OnFileImport`/`OnDownloadTemplate` so the busy flags always reset.
  [`Questions.razor:190`](../../src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor#L190), [`Questions.razor:218`](../../src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor#L218)

**Test coverage (peripherals)**

- All 7 matrix rows plus 2 review-fix regression tests (template round-trip, unsupported extension) against a SQLite in-memory context, invoking the real handler directly.
  [`ImportQuestionsCommandHandlerTests.cs:41`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Questions/ImportQuestionsCommandHandlerTests.cs#L41)

- Review fix: template round-trip test -- the exact regression guard for the verification-gap finding above.
  [`ImportQuestionsCommandHandlerTests.cs:360`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Questions/ImportQuestionsCommandHandlerTests.cs#L360)
