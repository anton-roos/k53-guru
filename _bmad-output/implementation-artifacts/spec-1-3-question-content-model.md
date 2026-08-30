---
title: 'Define the two-axis question content model'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: '95fce4719f22a7360cedc54d363e7845a433acac'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No `Question`/`AnswerOption` entities exist yet. Every downstream feature — authoring (Epic 2), import validation, test composition (Epic 3) — needs a persisted content model tagged by applicable licence code(s), section, and language, with answer options and a resolved sign reference.

**Approach:** Add `Question`/`AnswerOption` domain entities, `LicenceCode`/`SectionType` enums, EF configuration, a PostgreSQL migration, and a `SaveChanges` interceptor (mirroring the existing `AuditableEntityInterceptor` pattern) that enforces the model's two save-time invariants: `sign_ref` resolves to exactly one catalog sign, and each question has exactly one correct `AnswerOption`.

## Boundaries & Constraints

**Always:**
- `Question.Codes` is a `[Flags] LicenceCode` enum (`Code1=1, Code2=2, Code3=4`) storing one-or-more applicable codes; `Question.Section` is a plain `SectionType` enum (`Rules, Signs, VehicleControls`) — matches the `SectionType`/`LicenceCode` names the architecture spine already reserves for the client domain layer. Store both via `HasConversion<string>()`, matching the existing `DocumentType` convention.
- `Question.SignRef` is a nullable string (the `legislation_code`), never an FK — matches the architecture spine's explicit statement that `sign_ref` is "the official legislation-code string".
- `AnswerOption` is its own entity (own table, FK `QuestionId`, an `int Order` for the "ordered set"), per the architecture spine's ER diagram (`Question ||--|{ AnswerOption`) — never a JSON-serialized child list like `Product.Pictures`.
- Both entities derive from `BaseAuditableEntity`; ignore `DomainEvents` in EF config.
- DB access via `IApplicationDbContextFactory` per-op contexts; migration via `Migrators.PostgreSQL`; never `EnsureCreated`.
- Add a new `QuestionValidationInterceptor : SaveChangesInterceptor`, registered alongside the existing two in `DependencyInjection.cs`, enforcing: (a) a non-null `SignRef` resolves via `RoadSigns.Where(r => r.LegislationCode == signRef)` to exactly one row — zero throws "unresolved", more than one throws "ambiguous"; (b) each `Question`'s `AnswerOptions` has exactly one `IsCorrect == true`. Throw `FluentValidation.ValidationException` (the type this codebase's existing `ValidationExceptionHandler` already formats) with a clear message identifying which rule failed.

**Ask First:**
- Epic 2 (authoring UI) may prefer a FluentValidation command validator with `MustAsync` for these same two rules, giving better inline form UX than a `SaveChanges`-time exception. That's an additive change, not a conflict with this story — don't second-guess it now, but if the interceptor approach turns out to be awkward for that authoring flow, pause and confirm before removing/relocating this story's invariant instead of duplicating it silently.

**Never:**
- No Admin authoring UI, CQRS commands/queries, or CSV/JSON import (Epic 2's stories).
- No seed data for `Question`/`AnswerOption` — only the entity/persistence layer.
- Do not touch `RoadSign`'s schema (Story 1.1/1.2, done).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Valid question, resolved sign | `SignRef` matches one seeded `RoadSign`; exactly one `AnswerOption.IsCorrect` | Saved; entity round-trips with all fields intact | N/A |
| Valid question, no sign | `SignRef` is `null` (e.g. a Rules-section question); exactly one correct option | Saved; sign-resolution check skipped | N/A |
| Unresolved sign_ref | `SignRef` does not match any `RoadSign.LegislationCode` | Save rejected before commit | `ValidationException`, clear "unresolved" message |
| Zero correct answers | All `AnswerOption.IsCorrect == false` | Save rejected before commit | `ValidationException`, clear "exactly one correct" message |
| Multiple correct answers | More than one `AnswerOption.IsCorrect == true` | Save rejected before commit | `ValidationException`, clear "exactly one correct" message |
| Multiple applicable codes | `Codes = LicenceCode.Code1 \| LicenceCode.Code2` | Saved; round-trips as the combined flag value | N/A |
| Migration applied | Fresh PostgreSQL database | `Questions`/`AnswerOptions` tables exist with correct schema and FK | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Enums/LicenceCode.cs` -- New. `[Flags] enum LicenceCode { None = 0, Code1 = 1, Code2 = 2, Code3 = 4 }`.
- `src/K53Guru/src/Domain/Enums/SectionType.cs` -- New. `enum SectionType { Rules, Signs, VehicleControls }`.
- `src/K53Guru/src/Domain/Entities/Question.cs` -- New. `BaseAuditableEntity` + `Stem`, `Codes` (`LicenceCode`), `Section` (`SectionType`), `LanguageCode` (default `"en"`), `SignRef` (`string?`), `AnswerOptions` (`List<AnswerOption>`).
- `src/K53Guru/src/Domain/Entities/AnswerOption.cs` -- New. `BaseAuditableEntity` + `QuestionId` (FK), `Text`, `IsCorrect` (bool), `Order` (int).
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/QuestionConfiguration.cs` -- New. Mirror `RoadSignConfiguration.cs`: required/max-length on `Stem`/`LanguageCode`, `HasConversion<string>()` on `Codes`/`Section` (mirror `DocumentConfiguration.cs`'s `DocumentType` conversion), `HasMany(AnswerOptions).WithOne().HasForeignKey(QuestionId)`, ignore `DomainEvents`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/AnswerOptionConfiguration.cs` -- New. Required/max-length on `Text`, ignore `DomainEvents`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` + `src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs` -- Add `DbSet<Question> Questions` and `DbSet<AnswerOption> AnswerOptions` to **both** (Story 1.2 hit a compile gap from only updating the concrete class).
- `src/K53Guru/src/Infrastructure/Persistence/Interceptors/QuestionValidationInterceptor.cs` -- New. Mirror `AuditableEntityInterceptor.cs`'s `SavingChangesAsync` override; iterate `ChangeTracker.Entries<Question>()` in `Added`/`Modified` state, enforce the two invariants above.
- `src/K53Guru/src/Infrastructure/DependencyInjection.cs:94-95` -- Register `QuestionValidationInterceptor` alongside the existing two `ISaveChangesInterceptor` registrations.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- New migration via `dotnet ef migrations add AddQuestionContentModel`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/QuestionValidationInterceptorTests.cs` -- New. Covers 6 matrix rows (all but "Migration applied") via a SQLite in-memory `ApplicationDbContext` with the interceptor registered (mirror `RoadSignSeedingTests.cs`'s SQLite setup), plus an 8th review-fix test for `Codes == LicenceCode.None`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/AddQuestionContentModelMigrationTests.cs` -- New (review fix). Covers the "Migration applied" matrix row by reflectively invoking the real `AddQuestionContentModel.Up()` and asserting its `CreateTableOperation`/FK operations, mirroring `AddRoadSignMigrationTests.cs` -- the original test used `EnsureCreated()`, which never touched the checked-in migration file.
- Review fix: `QuestionValidationInterceptor.cs` gained a third invariant -- rejects `Codes == LicenceCode.None` -- since the frozen Intent requires "one or more" applicable codes and nothing previously enforced that floor.

## Tasks & Acceptance

**Execution:**
- [x] `LicenceCode.cs`, `SectionType.cs` -- create enums -- typed axes for question tagging.
- [x] `Question.cs`, `AnswerOption.cs` -- create entities -- the content model itself.
- [x] `QuestionConfiguration.cs`, `AnswerOptionConfiguration.cs` -- create EF configs -- schema, conversions, FK relationship.
- [x] `ApplicationDbContext.cs`, `IApplicationDbContext.cs` -- add `DbSet`s for both entities.
- [x] `QuestionValidationInterceptor.cs` -- create interceptor -- enforces sign-resolution and exactly-one-correct at save time.
- [x] `DependencyInjection.cs` -- register the new interceptor.
- [x] `Migrators.PostgreSQL/` -- generate `AddQuestionContentModel` migration.
- [x] `QuestionValidationInterceptorTests.cs` -- add tests covering all 7 matrix rows.

**Acceptance Criteria:**
- Given a `Question` with a `SignRef` and answer options, when saved with everything valid, then it and its `AnswerOption`s round-trip via EF with all fields (`Codes`, `Section`, `LanguageCode`, `SignRef`) intact.
- Given the migration applied to a fresh PostgreSQL database, when inspected, then `Questions` and `AnswerOptions` tables exist with the FK relationship and no `EnsureCreated` was used.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter QuestionValidationInterceptorTests` -- expected: all matrix-row tests pass.
- `dotnet ef migrations list -p src/K53Guru/src/Migrators/Migrators.PostgreSQL -s src/K53Guru/src/Server.UI` -- expected: `AddQuestionContentModel` appears in the list.

## Suggested Review Order

**Domain model**

- Entry point: the entity shape everything else configures, migrates, and validates.
  [`Question.cs:6`](../../src/K53Guru/src/Domain/Entities/Question.cs#L6)

- Child entity in the "ordered set" relationship; own table, own `Order`, FK to `Question`.
  [`AnswerOption.cs:5`](../../src/K53Guru/src/Domain/Entities/AnswerOption.cs#L5)

- Flags enum for one-or-more applicable codes -- the axis the "multiple applicable codes" matrix row exercises.
  [`LicenceCode.cs:9`](../../src/K53Guru/src/Domain/Enums/LicenceCode.cs#L9)

- The fixed-order test section axis (Rules -> Signs -> VehicleControls).
  [`SectionType.cs:7`](../../src/K53Guru/src/Domain/Enums/SectionType.cs#L7)

**Save-time invariants (the core of this story)**

- Enforces both frozen-intent invariants plus the review-added `Codes == None` guard, in one place mirroring `AuditableEntityInterceptor`.
  [`QuestionValidationInterceptor.cs:15`](../../src/K53Guru/src/Infrastructure/Persistence/Interceptors/QuestionValidationInterceptor.cs#L15)

- Review fix: rejects a `Question` carrying no applicable licence code, closing a gap the original implementation missed.
  [`QuestionValidationInterceptor.cs:32`](../../src/K53Guru/src/Infrastructure/Persistence/Interceptors/QuestionValidationInterceptor.cs#L32)

- Registration alongside the two existing interceptors -- easy to miss, required for any of this to actually run.
  [`DependencyInjection.cs:96`](../../src/K53Guru/src/Infrastructure/DependencyInjection.cs#L96)

**Persistence & schema**

- `HasConversion<string>()` on both enum axes, mirroring the existing `DocumentType` convention; the FK relationship to `AnswerOption`.
  [`QuestionConfiguration.cs:11`](../../src/K53Guru/src/Infrastructure/Persistence/Configurations/QuestionConfiguration.cs#L11)

- Interface gap Story 1.2 hit is explicitly avoided here -- `DbSet`s added to the interface, not just the concrete context.
  [`IApplicationDbContext.cs:22`](../../src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs#L22)

- Generated DDL for both tables plus the FK -- what the review-fixed migration test now actually exercises.
  [`20260830080734_AddQuestionContentModel.cs:15`](../../src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/20260830080734_AddQuestionContentModel.cs#L15)

**Test coverage (peripherals)**

- All 8 interceptor-invariant tests (7 matrix rows + the review-added `None`-code case), against SQLite in-memory since no live DB is reachable here.
  [`QuestionValidationInterceptorTests.cs:34`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/QuestionValidationInterceptorTests.cs#L34)

- Review fix: exercises the real checked-in migration file via reflection (mirroring `AddRoadSignMigrationTests.cs`), replacing an `EnsureCreated()`-based test that never touched it.
  [`AddQuestionContentModelMigrationTests.cs:19`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/AddQuestionContentModelMigrationTests.cs#L19)
