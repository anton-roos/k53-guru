---
title: 'Configure per-code test parameters as data'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: '8b7b4406c3d74642877748e791252c3d07a2750b'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
  - '{project-root}/_bmad-output/specs/spec-k53-learners-app/test-structure.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No fidelity parameters (question counts, pass marks, time limits) exist as data anywhere -- Story 3.3's attempt composition and Story 3.5's grading would otherwise need to hardcode them, which the architecture explicitly forbids and which blocks tuning without a code change.

**Approach:** Add two new entities, `TestConfig` (one row per `LicenceCode` -- Code1/Code2/Code3 -- holding that code's time limit) and its child `SectionRule`s (one per `SectionType` -- Rules/Signs/VehicleControls -- holding that section's question count and pass mark), seeded once from `test-structure.md`'s documented figures. `test-structure.md` itself flags these figures as **provisional** (ranges, not fixed numbers, "confirm against a live DLTC/CLLT terminal before relying on them") -- per the human's explicit direction, this story seeds one representative, clearly-flagged-as-placeholder value per parameter rather than modeling ranges: the upper end of each question-count range, the documented pass mark paired with it, and a generous placeholder time limit (60 minutes) since no time limit is specified in the source at all.

## Boundaries & Constraints

**Always:**
- New `Domain.Entities.TestConfig : BaseAuditableEntity` -- `Code` (`LicenceCode`, single-bit value: `Code1`, `Code2`, or `Code3` -- reuses the existing flags enum rather than introducing a parallel one, same way `Test.Codes` already does for combinations), `TimeLimitMinutes` (int), `SectionRules` (`List<SectionRule>`).
- New `Domain.Entities.SectionRule : BaseAuditableEntity` -- `TestConfigId` (int, FK), `Section` (`SectionType` -- reuses `Question`'s existing plain enum, not `TestSectionScope`, since a rule always names exactly one section), `QuestionCount` (int), `PassMark` (int).
- Seed exactly 3 `TestConfig` rows (one per `Code1`/`Code2`/`Code3`), each `TimeLimitMinutes = 60` (placeholder), each with exactly 3 child `SectionRule`s: `Rules` (30, 22), `Signs` (30, 23), `VehicleControls` (12, 10) -- identical across all three codes today, since `test-structure.md` documents no per-code numeric variance (only content differs by code, which the existing `Question.Codes` tagging already handles). Seeding is idempotent (`if (await _context.TestConfigs.AnyAsync()) return;`, mirroring `SeedRoadSignsAsync`'s guard) and wired into `ApplicationDbContextInitializer.SeedDataAsync` right after the existing `await SeedRoadSignsAsync();` call.
- Every seeded number carries an inline comment/XML-doc pointing at `test-structure.md` and stating it's a provisional placeholder, not a confirmed official value -- this must be traceable, not silently indistinguishable from a confirmed figure.
- EF configuration (`TestConfigConfiguration.cs`/`SectionRuleConfiguration.cs`) mirrors `TestConfiguration.cs`'s exact shape: enum properties via `.HasConversion<string>().HasMaxLength(...)`, `HasMany(...).WithOne().OnDelete(DeleteBehavior.Cascade)` from `TestConfig` to `SectionRule`, `builder.Ignore(e => e.DomainEvents)`.
- New `Migrators.PostgreSQL` migration only, matching this codebase's established Postgres-as-source-of-truth pattern for every prior new entity (Story 1.1's `RoadSign`, Story 1.3's `Question`/`AnswerOption`) -- the pre-existing MSSQL/SqLite migration gap is already extensively logged in `deferred-work.md` and not re-litigated here.
- This story only stands up the config-as-data mechanism and seeds it -- it does not build an Admin UI to edit it (nothing in Epic 3's planning artifacts calls for one; a maintainer changes a seeded row directly, satisfying the AC's actual guarantee: "no code change needed," not "a UI exists").

**Ask First:**
- None.

**Never:**
- No attempt-composition logic (Story 3.3), no grading logic (Story 3.5), no combination-sitting merge logic for how Code1+2's shared Rules/Signs rule is selected when both codes' configs are numerically identical (Story 3.4's concern).
- No Admin Blazor page, no MediatR command/query exposing this config externally -- later stories that need to read it do so directly via `IApplicationDbContextFactory`, the same way every other Application-layer read does.
- No per-code numeric differentiation logic or UI -- the schema supports it (a future maintainer could seed different numbers per code), but this story seeds identical values across all three codes, matching the source document.
- No range/min-max modeling of question counts -- fixed single values only, per the human's explicit direction.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| First run, no TestConfig rows exist | Empty `TestConfigs` table | Seeding creates exactly 3 `TestConfig` rows (Code1/Code2/Code3), each with 3 `SectionRule`s | N/A |
| Second run, TestConfig rows already exist | `TestConfigs` table non-empty | Seeding is a no-op (idempotent, no duplicate rows) | N/A |
| Seeded values are correct | After seeding | Each `TestConfig.TimeLimitMinutes == 60`; each code's `SectionRule`s are exactly `Rules(30,22)`, `Signs(30,23)`, `VehicleControls(12,10)` | N/A |
| Config value changed after seeding | A `SectionRule.PassMark` is updated directly in the DB | A subsequent read via `IApplicationDbContextFactory` returns the new value -- no code/recompilation involved | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Domain/Entities/TestConfig.cs` -- New. `BaseAuditableEntity` with `Code`/`TimeLimitMinutes`/`SectionRules`.
- `src/K53Guru/src/Domain/Entities/SectionRule.cs` -- New. `BaseAuditableEntity` with `TestConfigId`/`Section`/`QuestionCount`/`PassMark`.
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/TestConfigConfiguration.cs` -- New. Mirrors `TestConfiguration.cs`'s shape (enum-as-string, cascade delete to `SectionRule`).
- `src/K53Guru/src/Infrastructure/Persistence/Configurations/SectionRuleConfiguration.cs` -- New. Mirrors sibling configuration shape (enum-as-string on `Section`).
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContext.cs` -- Modify. Add `DbSet<TestConfig> TestConfigs` and `DbSet<SectionRule> SectionRules`.
- `src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs` -- Modify. Add `SeedTestConfigsAsync()` (idempotent, seeds the 3x3 rows with inline provisional-value comments), called from `SeedDataAsync()` right after the existing `SeedRoadSignsAsync()` call.
- `src/K53Guru/src/Migrators/Migrators.PostgreSQL/Migrations/*_AddTestConfig.cs` -- New. `dotnet ef migrations add AddTestConfig` against the Postgres provider only.
- `src/K53Guru/tests/Infrastructure.UnitTests/Persistence/TestConfigSeedingTests.cs` -- New. Covers all 4 matrix rows via SQLite in-memory (`EnsureCreated()`), mirroring `RoadSignSeedingTests.cs`'s established harness -- seeds, asserts exact values, re-runs seeding to prove idempotency, updates a value directly and re-reads to prove no hardcoding.
- Review fix (edge-case-hunter + verification-gap, both independently converged): every assertion went through EF's `Include(c => c.SectionRules)` navigation and checked only aggregate counts/values -- since all three seeded codes carry byte-for-byte identical child data, a rotation/swap-type FK corruption (e.g. Code1's rules accidentally wired to Code2's `TestConfig.Id`) would have passed every test undetected. Added explicit per-row `Assert.Equal(c.Id, r.TestConfigId)` assertions to `FirstRunSeed_...` and `SeededValues_...`.
- Beyond the original Code Map: `src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs` -- also gained the two new `DbSet`s (`TestConfigs`/`SectionRules`), required so `IApplicationDbContextFactory.CreateAsync()` (which returns the interface type) can actually reach these tables from a future Application-layer handler -- confirmed necessary and consistent with every other entity's dual interface/concrete exposure, not scope creep.

## Tasks & Acceptance

**Execution:**
- [x] `TestConfig.cs`/`SectionRule.cs` -- create the two new Domain entities.
- [x] `TestConfigConfiguration.cs`/`SectionRuleConfiguration.cs` -- create EF configurations.
- [x] `ApplicationDbContext.cs` -- add the two new `DbSet`s.
- [x] `ApplicationDbContextInitializer.cs` -- add `SeedTestConfigsAsync()`, wire it into `SeedDataAsync()`.
- [x] `Migrators.PostgreSQL` migration -- generate and apply.
- [x] `TestConfigSeedingTests.cs` -- add tests covering all 4 matrix rows.

**Acceptance Criteria:**
- Given the configuration entities, when the system seeds them, then each code has its section definitions, question counts, pass marks, and time limit as data -- no thresholds hardcoded in Domain/Application logic.
- Given a value must change, when a maintainer updates it directly in the data store, then a subsequent read reflects the new value with no code change.

## Design Notes

`test-structure.md`'s own header flags these figures as provisional ("High confidence on structure and pass marks; the exact per-sitting count varies within the stated range... Confirm the final ranges and the time limit against a live DLTC/CLLT terminal before relying on them"). This story's seeded numbers are a deliberate, human-approved placeholder choice (upper-end counts, paired pass marks, 60-minute time limit) -- not a claim that these are the confirmed official figures. A `deferred-work.md` entry tracks that these specific values need real-world confirmation before this system is relied on for production exam delivery.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter TestConfigSeedingTests` -- expected: all matrix-row tests pass.

**Manual checks (if no CLI):**
- Run the app against a real Postgres instance with migrations applied, confirm `TestConfigs`/`SectionRules` tables are populated with 3 and 9 rows respectively after first startup.

## Suggested Review Order

**Domain & persistence**

- The two new entities, and the deliberate reuse of `LicenceCode`/`SectionType`.
  [`TestConfig.cs:13`](../../src/K53Guru/src/Domain/Entities/TestConfig.cs#L13), [`SectionRule.cs:14`](../../src/K53Guru/src/Domain/Entities/SectionRule.cs#L14)

- Seeding: idempotency guard, the 3x3 seed shape, and the provisional-value comments (verified by review to accurately cite `test-structure.md`'s real figures).
  [`ApplicationDbContextInitializer.cs:369`](../../src/K53Guru/src/Infrastructure/Persistence/ApplicationDbContextInitializer.cs#L369)

- Beyond the original Code Map: `IApplicationDbContext.cs`'s two new `DbSet`s, confirmed necessary for `IApplicationDbContextFactory` consumers to reach this data.
  [`IApplicationDbContext.cs`](../../src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs)

**Test coverage**

- All 4 matrix rows against a real SQLite in-memory context, invoking the real seeding method via reflection (mirrors `RoadSignSeedingTests.cs`).
  [`TestConfigSeedingTests.cs:33`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/TestConfigSeedingTests.cs#L33)

- Review fix: per-row FK-linkage assertions closing the rotation/swap-corruption blind spot.
  [`TestConfigSeedingTests.cs:86`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/TestConfigSeedingTests.cs#L86), [`TestConfigSeedingTests.cs:135`](../../src/K53Guru/tests/Infrastructure.UnitTests/Persistence/TestConfigSeedingTests.cs#L135)
