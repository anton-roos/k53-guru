---
title: 'Seed the road-sign catalog with legislative codes'
type: 'feature'
created: '2026-08-29'
status: 'done'
baseline_commit: 'b9d921da84e2129dd1f2fb2841969a08494d147d'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The system has no road-sign catalog. Every downstream feature — question authoring, import validation, test composition — needs signs referenced by their official legislative code, but no `RoadSign` entity, EF configuration, or seed data exists yet.

**Approach:** Add a `RoadSign` domain entity keyed by a unique `legislation_code`, its EF configuration and DbSet, a PostgreSQL migration, and an idempotent seed step in the existing `ApplicationDbContextInitializer`. Seed a representative starter set (~20 signs covering regulatory, warning, and guidance categories) so the Admin Panel and import pipeline have real data to resolve `sign_ref` against.

## Boundaries & Constraints

**Always:**
- `legislation_code` must be a unique index; seeding must fail fast (throw) on a duplicate rather than silently skipping.
- Derive from `BaseAuditableEntity`; ignore `DomainEvents` in the EF config.
- DB access via `IApplicationDbContextFactory` per-op contexts.
- Migration via the `Migrators.PostgreSQL` project; never `EnsureCreated`.

**Ask First:**
- If the real SARTSM source data format is available and differs from the assumed shape, pause and ask before adjusting the entity schema.

**Never:**
- Do not create the Question/AnswerOption entities (Story 1.3).
- Do not build an Admin UI page for signs (Story 1.2).
- Do not hardcode sign data inside application code — seed data lives in the initializer only.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First-run seed | Empty `RoadSigns` table | ~20 representative signs inserted, each with a unique `legislation_code`, description, and `image_asset_key` | N/A |
| Idempotent restart | `RoadSigns` already populated | Seeding is skipped; no duplicates created | N/A |
| Duplicate in source | Two seed entries share the same `legislation_code` | Seeding fails with a clear exception before any data is committed | Exception thrown by unique-index violation on SaveChanges |
| Migration applied | Fresh PostgreSQL database | `RoadSigns` table exists with the correct schema including the unique index on `legislation_code` | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Entities/RoadSign.cs` -- New entity. Model: `BaseAuditableEntity` + `LegislationCode`, `Description`, `ImageAssetKey`.
- `src/K53Guru/src/Domain/_Imports.cs` -- Already imports `K53Guru.Domain.Entities`; no change needed.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/RoadSignConfiguration.cs` -- New EF config. Mirror `ContactConfiguration.cs` pattern: required + max-length on strings, unique index on `LegislationCode`, ignore `DomainEvents`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` -- Add `DbSet<RoadSign> RoadSigns`. Existing configs auto-apply via `ApplyConfigurationsFromAssembly`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs` -- Add `SeedRoadSignsAsync()` with `AnyAsync()` guard, called from `SeedDataAsync()`. ~20 representative signs (R1, R2, W1, W2, GS1 etc.).
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- New migration via `dotnet ef migrations add AddRoadSign`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/RoadSignSeedingTests.cs` -- New. Covers matrix rows "First-run seed" and "Idempotent restart" via SQLite in-memory DB against the real `SeedRoadSignsAsync`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/RoadSignConfigurationTests.cs` -- New. Covers matrix row "Duplicate in source" (`SaveChangesAsync` throws `DbUpdateException`, nothing committed) plus a positive-path sanity test.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/AddRoadSignMigrationTests.cs` -- New. Covers matrix row "Migration applied" by inspecting the real `AddRoadSign.Up()` DDL operations (no live Postgres in this environment).
- `src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj` -- Retargeted `net9.0` -> `net10.0` (was mismatched with `net10.0` project refs, so the project silently failed to build/was never in the solution); added `Migrators.PostgreSQL` project reference; excluded pre-existing, already-broken `Services/GeolocationServiceTests.cs` (references a `GeolocationService` type that no longer exists -- unrelated dead code, left as-is otherwise).
- `src/K53Guru/K53Guru.slnx` -- Added `Infrastructure.UnitTests` to the solution (was previously excluded, which is how its build failure went unnoticed).

## Tasks & Acceptance

**Execution:**
- [x] `src/K53Guru/src/Domain/Entities/RoadSign.cs` -- Create entity deriving `BaseAuditableEntity` with `string LegislationCode`, `string Description`, `string? ImageAssetKey`.
- [x] `src/K53Guru/src/Infrastructure/Persistence/Configurations/RoadSignConfiguration.cs` -- Create EF config: `LegislationCode` required, max 20, unique index; `Description` required, max 500; `ImageAssetKey` max 500; ignore `DomainEvents`.
- [x] `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` -- Add `public DbSet<RoadSign> RoadSigns { get; set; }`.
- [x] `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs` -- Add `SeedRoadSignsAsync()` with `AnyAsync()` guard and ~20 representative signs, called from `SeedDataAsync()`.
- [x] `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- Generate migration: `dotnet ef migrations add AddRoadSign -p src/Migrators/Migrators.PostgreSQL -s src/Server.UI`.

**Acceptance Criteria:**
- Given a fresh database, when the application starts, then the `RoadSigns` table is created and contains ~20 seeded signs each with a unique `legislation_code`.
- Given an already-seeded database, when the application restarts, then no duplicate signs are created.
- Given a duplicate `legislation_code` in the seed data, when seeding runs, then an exception is thrown before any data is committed.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors
- `dotnet ef migrations list -p src/Migrators/Migrators.PostgreSQL -s src/Server.UI` -- expected: `AddRoadSign` migration appears in the list

## Suggested Review Order

**Schema definition**

- Entry point: the shape of the new entity everything else maps and migrates.
  [`RoadSign.cs:5`](../../src/K53Guru/src/Domain/Entities/RoadSign.cs#L5)

- Unique index on `LegislationCode` is what makes the "duplicate in source" acceptance criterion enforceable.
  [`RoadSignConfiguration.cs:11`](../../src/K53Guru/src/Infrastructure/Persistence/Configurations/RoadSignConfiguration.cs#L11)

- `DbSet` registration wires the entity into the context so `ApplyConfigurationsFromAssembly` picks up the config above.
  [`ApplicationDbContext.cs:31`](../../src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs#L31)

- Generated DDL: creates `road_signs` and its unique index against PostgreSQL.
  [`20260829113154_AddRoadSign.cs:47`](../../src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/20260829113154_AddRoadSign.cs#L47)

**Seeding logic**

- `AnyAsync()` idempotency guard, then the ~20-sign starter set inserted in one `SaveChangesAsync`.
  [`ApplicationDbContextInitializer.cs:323`](../../src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs#L323)

- Call site wiring the new seed step into the existing `SeedDataAsync` sequence.
  [`ApplicationDbContextInitializer.cs:320`](../../src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs#L320)

**Test coverage (peripherals)**

- First-run and idempotent-restart matrix rows, exercised against the real private seed method via reflection.
  [`RoadSignSeedingTests.cs:49`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/RoadSignSeedingTests.cs#L49)

- Duplicate-code matrix row: asserts `SaveChangesAsync` throws and commits nothing.
  [`RoadSignConfigurationTests.cs:21`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/RoadSignConfigurationTests.cs#L21)

- Migration-applied matrix row: asserts the shipped `Up()` DDL without needing a live PostgreSQL server.
  [`AddRoadSignMigrationTests.cs:35`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/AddRoadSignMigrationTests.cs#L35)

- Project was previously unbuildable and excluded from the solution; retargeted to `net10.0` and a pre-existing broken test file excluded so it can build.
  [`Infrastructure.UnitTests.csproj:4`](../../src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj#L4)

- Adds the now-buildable test project to the solution so `dotnet build`/`test` stop silently skipping it.
  [`K53Guru.slnx:25`](../../src/K53Guru/K53Guru.slnx#L25)

- Doc fix: corrects a stale claim about which projects still target `net9.0`.
  [`project.md:68`](../../src/K53Guru/openspec/project.md#L68)
