---
title: 'Seed the road-sign catalog with legislative codes'
type: 'feature'
created: '2026-08-29'
status: 'in-progress'
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

## Tasks & Acceptance

**Execution:**
- [ ] `src/K53Guru/src/Domain/Entities/RoadSign.cs` -- Create entity deriving `BaseAuditableEntity` with `string LegislationCode`, `string Description`, `string? ImageAssetKey`.
- [ ] `src/K53Guru/src/Infrastructure/Persistence/Configurations/RoadSignConfiguration.cs` -- Create EF config: `LegislationCode` required, max 20, unique index; `Description` required, max 500; `ImageAssetKey` max 500; ignore `DomainEvents`.
- [ ] `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` -- Add `public DbSet<RoadSign> RoadSigns { get; set; }`.
- [ ] `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs` -- Add `SeedRoadSignsAsync()` with `AnyAsync()` guard and ~20 representative signs, called from `SeedDataAsync()`.
- [ ] `src/K53Guru/src/Migrators/Migrators.PostgreSQL/` -- Generate migration: `dotnet ef migrations add AddRoadSign -p src/Migrators/Migrators.PostgreSQL -s src/Server.UI`.

**Acceptance Criteria:**
- Given a fresh database, when the application starts, then the `RoadSigns` table is created and contains ~20 seeded signs each with a unique `legislation_code`.
- Given an already-seeded database, when the application restarts, then no duplicate signs are created.
- Given a duplicate `legislation_code` in the seed data, when seeding runs, then an exception is thrown before any data is committed.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors
- `dotnet ef migrations list -p src/Migrators/Migrators.PostgreSQL -s src/Server.UI` -- expected: `AddRoadSign` migration appears in the list
