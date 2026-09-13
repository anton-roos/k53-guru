---
title: 'Author combination signs in the road-sign catalog'
type: 'feature'
created: '2026-09-13'
status: 'done'
baseline_commit: '0f4da2c359b588582cb406f80a035c8242d3d3c4'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
  - '{project-root}/_bmad-output/specs/spec-k53-learners-app/SPEC.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The catalog has no way to represent a combination sign (a primary sign + supplementary qualifier plate mounted together, e.g. No Entry + "except vehicles under 3t") — and no admin authoring exists for `RoadSign` at all today (Story 1.2 is browse/search only; every row comes from `road-signs-catalog.json`). SPEC.md's CAP-10 requires this without any client/API changes: a combination is one ordinary catalog row carrying a single pre-composited image.

**Approach:** Add full Create/Edit authoring for `RoadSign` (general-purpose, not combination-only), plus one additional field — `ComponentSignCodes` — recording which existing legislation codes a composite was assembled from, admin-facing only. A composite is authored exactly like any sign: its own synthetic code (e.g. `R3+R531`), a description, and one uploaded image; nothing downstream (`sign_ref` resolution, import, attempt snapshotting, Flutter rendering) needs to change.

## Boundaries & Constraints

**Always:**
- New `AddEditRoadSignCommand`/Handler/Validator mirror `AddEditQuestionCommand.cs`/`AddEditQuestionCommandValidator.cs` exactly: `[RequestAuthorize(Roles = Roles.Admin)]`, `ICacheInvalidatorRequest<Result<int>>` via `RoadSignCacheKey`, `IApplicationDbContextFactory` per-op context, Id>0 edit / else create branching.
- `LegislationCode` uniqueness (excluding self on edit) is validated the same way `SignRef` resolution already is in `AddEditQuestionCommandValidator` — a `MustAsync` rule, not just the DB unique index.
- An uploaded image is saved under `wwwroot/img/signs/` — the same convention every existing sign already uses (`RoadSignExtensions.ToImageUrl`) — via a new `IRoadSignImageStore` (Application interface, Infrastructure impl using `IWebHostEnvironment.WebRootPath`), never `IUploadService`/MinIO (that path writes to `Files/`/object storage, not `wwwroot/img/`, and would break every existing sign's resolution). Derive the filename from `LegislationCode` (lowercased, `+` replaced with `-`, e.g. `R3+R531` → `r3-r531.svg`).
- `ComponentSignCodes` is a plain nullable string column on `RoadSign` (comma-separated legislation codes, e.g. `"R3,R531"`) — not a normalized relationship or JSON list. It flows through `RoadSignDto`/`GetRoadSignByIdQuery` automatically (AutoMapper property-name match); it is never read by the learner API, `AttemptQuestion`, or the Flutter client.
- New `Permissions.RoadSigns.Create`/`.Edit` (+ `RoadSignsAccessRights` bools) mirror `QuestionsPermissions.cs` exactly.
- Reuse `RoadSignsWithPaginationQuery` (existing keyword search) to power the form's "component signs" picker — no new query.

**Ask First:**
- If a submitted `ComponentSignCodes` entry doesn't resolve to an existing `RoadSign.LegislationCode`, pause and confirm whether to hard-reject (matching the `SignRef` precedent) or accept it anyway (it's provenance-only, non-authoritative) before picking one.

**Never:**
- No changes to `Question`, `AttemptQuestion`, `RoadSignExtensions`, import (`Questions/Commands/Import`), or any Flutter file — a composite is an ordinary `RoadSign` row and every consumer already resolves `legislation_code` generically.
- No change to `ApplicationDbContextInitializer.SeedRoadSignsAsync` — it already upserts only rows present in `road-signs-catalog.json` and never touches other codes, so admin-authored composites are already safe from seeding.
- Do not wire up `IUploadService`/MinIO for this story.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Create a combination sign | New `LegislationCode="R3+R531"`, description, image file, `ComponentSignCodes=["R3","R531"]` | Row saved; image written to `wwwroot/img/signs/r3-r531.svg`; `ImageAssetKey="signs/r3-r531.svg"` | N/A |
| Duplicate `LegislationCode` | Code already exists in catalog | Save rejected before commit | Validation error identifying the field |
| Edit without a new image | Existing row, no file re-picked | `ImageAssetKey` unchanged; other fields update | N/A |
| Unresolved component code | `ComponentSignCodes` contains a code with no matching `RoadSign` | Per "Ask First" resolution | Per "Ask First" resolution |
| Plain sign (no components) | `ComponentSignCodes` empty/null | Saved exactly like today's seeded signs | N/A |

## Code Map

- `src/K53Guru/src/Domain/Entities/RoadSign.cs` -- add `public string? ComponentSignCodes { get; set; }`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/RoadSignConfiguration.cs` -- add `HasMaxLength(200)` for the new column.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- new migration `AddRoadSignComponentCodes`.
- `src/K53Guru/src/Application/Features/RoadSigns/Security/RoadSignsPermissions.cs` -- add `Create`/`Edit` consts + access-right bools, mirroring [QuestionsPermissions.cs](src/K53Guru/src/Application/Features/Questions/Security/QuestionsPermissions.cs).
- `src/K53Guru/src/Application/Features/RoadSigns/DTOs/RoadSignDto.cs` -- add `ComponentSignCodes`.
- `src/K53Guru/src/Application/Common/Interfaces/IRoadSignImageStore.cs` -- new. `Task<string> SaveAsync(string legislationCode, string fileName, byte[] bytes, CancellationToken ct)` returning the `ImageAssetKey`.
- `src/K53Guru/src/Infrastructure/Services/RoadSignImageStore.cs` -- new impl, writes under `{IWebHostEnvironment.WebRootPath}/img/signs/`.
- `src/K53Guru/src/Infrastructure/DependencyInjection.cs` -- register `IRoadSignImageStore`.
- `src/K53Guru/src/Application/Features/RoadSigns/Commands/AddEdit/AddEditRoadSignCommand.cs` + `AddEditRoadSignCommandValidator.cs` -- new, mirroring [AddEditQuestionCommand.cs](src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs) / [AddEditQuestionCommandValidator.cs](src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommandValidator.cs).
- `src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignFormDialog.razor` -- new, mirroring [QuestionFormDialog.razor](src/K53Guru/src/Server.UI/Pages/Questions/Components/QuestionFormDialog.razor) for form/submit shape and [UserFormDialog.razor:32](src/K53Guru/src/Server.UI/Pages/Identity/Users/Components/UserFormDialog.razor#L32) for the `MudFileUpload` pattern; add a component-signs multi-picker backed by `RoadSignsWithPaginationQuery`.
- `src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor` -- add "New"/"Edit" actions (permission-guarded) mirroring [Questions.razor](src/K53Guru/src/Server.UI/Pages/Questions/Questions.razor)'s `OnCreateQuestion`/`OnEditQuestion`/`ShowQuestionFormDialog`; add a "Combination" chip column when `ComponentSignCodes` is set.
- `src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignDetailDialog.razor` -- show `ComponentSignCodes` when present.

## Tasks & Acceptance

**Execution:**
- [x] `RoadSign.cs`, `RoadSignConfiguration.cs`, migration -- add `ComponentSignCodes` column.
- [x] `RoadSignsPermissions.cs` -- add `Create`/`Edit`.
- [x] `RoadSignDto.cs` -- add `ComponentSignCodes`.
- [x] `IRoadSignImageStore.cs`, `RoadSignImageStore.cs`, DI registration -- image save to `wwwroot/img/signs/`.
- [x] `AddEditRoadSignCommand.cs`, `AddEditRoadSignCommandValidator.cs` -- create/edit + validation.
- [x] `RoadSignFormDialog.razor` -- authoring UI with upload + component-signs picker.
- [x] `RoadSigns.razor`, `RoadSignDetailDialog.razor` -- wire New/Edit actions, show composite indicator.
- [x] Unit tests -- cover the I/O matrix (mirror `QuestionValidationInterceptorTests.cs`/existing RoadSign test patterns).

**Acceptance Criteria:**
- Given an admin with `Permissions.RoadSigns.Create`, when they submit a new combination sign with an image and component codes, then the row is queryable via `GetRoadSignByIdQuery`/`RoadSignsWithPaginationQuery` and a question's `sign_ref` resolves it exactly like any single sign.
- Given an admin without `Create`/`Edit` rights, when they attempt either action, then it is denied.
- Given the migration applied to PostgreSQL, when inspected, then `RoadSigns.ComponentSignCodes` exists as a nullable column with no `EnsureCreated`.

## Design Notes

`ComponentSignCodes` is deliberately a flat delimited string, not a normalized join table or JSON list — it's admin-display provenance only (SPEC.md's CAP-10 constraint), never queried or joined against, so the simplest shape that round-trips through existing AutoMapper projections is correct; do not build a child-entity relationship for it.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter RoadSign` -- expected: new tests pass.
- `dotnet ef migrations list -p src/K53Guru/src/Migrators/Migrators.PostgreSQL -s src/K53Guru/src/Server.UI` -- expected: `AddRoadSignComponentCodes` appears in the list.

## Suggested Review Order

**Domain model**

- The whole design in one line: a combination sign is an ordinary `RoadSign` row, nothing more.
  [`RoadSign.cs:18`](../../src/K53Guru/src/Domain/Entities/RoadSign.cs#L18)

**Command & validation (core logic + security patches)**

- Create/edit branching; writes the new image only when one was picked, never clobbers on a no-image edit.
  [`AddEditRoadSignCommand.cs:79`](../../src/K53Guru/src/Application/Features/RoadSigns/Commands/AddEdit/AddEditRoadSignCommand.cs#L79)

- Case-insensitive uniqueness (patch) -- prevents "R1"/"r1" coexisting and silently overwriting each other's image file.
  [`AddEditRoadSignCommandValidator.cs:75`](../../src/K53Guru/src/Application/Features/RoadSigns/Commands/AddEdit/AddEditRoadSignCommandValidator.cs#L75)

- Self-reference exclusion (patch) -- a composite can no longer list its own code as one of its own components.
  [`AddEditRoadSignCommandValidator.cs:88`](../../src/K53Guru/src/Application/Features/RoadSigns/Commands/AddEdit/AddEditRoadSignCommandValidator.cs#L88)

- Server-side size and extension allow-list (patches) -- close the gap left by client-only `MudFileUpload` restrictions.
  [`AddEditRoadSignCommandValidator.cs:63`](../../src/K53Guru/src/Application/Features/RoadSigns/Commands/AddEdit/AddEditRoadSignCommandValidator.cs#L63)

**Image storage (path-traversal defense-in-depth)**

- Every character that could form a path or escape the filename is stripped, not just `+`.
  [`RoadSignImageStore.cs:105`](../../src/K53Guru/src/Infrastructure/Services/RoadSignImageStore.cs#L105)

- Belt-and-suspenders: the resolved full path is re-checked against the signs folder before every write/delete.
  [`RoadSignImageStore.cs:88`](../../src/K53Guru/src/Infrastructure/Services/RoadSignImageStore.cs#L88)

- Orphan cleanup (patch) -- best-effort delete of the superseded image after a successful edit commit.
  [`RoadSignImageStore.cs:50`](../../src/K53Guru/src/Infrastructure/Services/RoadSignImageStore.cs#L50)

**Admin UI**

- Create/Edit actions gated by the new granular permissions, wired the same way Questions.razor already does it.
  [`RoadSigns.razor:196`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor#L196)

- Image picker mirrors the existing avatar-upload pattern; preview swaps between stored URL and just-picked bytes.
  [`RoadSignFormDialog.razor:46`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignFormDialog.razor#L46)

- Component-signs picker reuses the existing catalog search query -- no new query needed for this feature.
  [`RoadSignFormDialog.razor:60`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignFormDialog.razor#L60)

**Peripherals**

- New `Create`/`Edit` permission constants, mirroring `QuestionsPermissions.cs` exactly.
  [`RoadSignsPermissions.cs:15`](../../src/K53Guru/src/Application/Features/RoadSigns/Security/RoadSignsPermissions.cs#L15)

- The nullable column this whole story hinges on.
  [`RoadSignDto.cs:14`](../../src/K53Guru/src/Application/Features/RoadSigns/DTOs/RoadSignDto.cs#L14)

- Real migration script the new migration test (below) exercises via reflection.
  [`20260913134853_AddRoadSignComponentCodes.cs:11`](../../src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/20260913134853_AddRoadSignComponentCodes.cs#L11)

- I/O matrix coverage: create, duplicate, edit-without-image, unresolved/self-referencing component codes, oversized/disallowed images.
  [`AddEditRoadSignCommandHandlerTests.cs:39`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/RoadSigns/AddEditRoadSignCommandHandlerTests.cs#L39)

- Path-traversal and sanitization coverage for the image store patch.
  [`RoadSignImageStoreTests.cs:20`](../../src/K53Guru/tests/Infrastructure.UnitTests/Services/RoadSignImageStoreTests.cs#L20)

- Migration Up()/Down() coverage, mirroring `AddRoadSignMigrationTests.cs`'s established pattern.
  [`AddRoadSignComponentCodesMigrationTests.cs:17`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/AddRoadSignComponentCodesMigrationTests.cs#L17)

</frozen-after-approval>
