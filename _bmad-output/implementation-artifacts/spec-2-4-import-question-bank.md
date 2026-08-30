---
title: 'Import a question bank via CSV/JSON with reject-on-error validation'
type: 'feature'
created: '2026-08-30'
status: 'in-review'
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

## Tasks & Acceptance

**Execution:**
- [ ] Add `CsvHelper` package reference to `Application.csproj`.
- [ ] `QuestionImportRow.cs` -- create the shared, format-agnostic row DTO.
- [ ] `ImportQuestionsCommand.cs` (+handler) -- create the two-phase import command with CSV and JSON parsing.
- [ ] `ImportQuestionsCommandValidator.cs` -- create the shallow command-level validator.
- [ ] `CreateQuestionsImportTemplateCommand.cs` (+handler) -- create the template-download command for both formats.
- [ ] `Questions.razor` -- add the import upload control and template-download button.
- [ ] `ImportQuestionsCommandHandlerTests.cs` -- add tests covering all 7 matrix rows.

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
