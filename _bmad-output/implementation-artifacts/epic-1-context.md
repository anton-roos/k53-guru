# Epic 1 Context: Road-Sign Catalog & Content Model

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

Establish the authoritative, seeded road-sign catalog (with official legislative codes as unique keys) and the two-axis question/answer content model that all test content references. This epic is the foundational prerequisite for every downstream epic: all questions reference signs from this catalog, and all test composition depends on the content model structure.

## Stories

- Story 1.1: Seed the road-sign catalog with legislative codes
- Story 1.2: Browse and search the sign catalog in the Admin Panel
- Story 1.3: Define the two-axis question content model

## Requirements & Constraints

**Functional & Non-Functional:**
- Road signs are stored with official legislative codes, not ad-hoc labels.
- Every question references an authoritative sign via `sign_ref` resolved against the pre-loaded catalog.
- Import validation rejects any row with an unresolved sign reference; no broken question ever reaches a learner.

**Content Structure:**
- Questions carry two independent axes: applicable code(s) from `{Code1, Code2, Code3}` and a class of `shared` (Rules of the Road, Road Signs) or `code-specific` (Vehicle Controls).
- A `language_code` field defaults to `en`; v1 is English-only (multi-language is deferred).
- Section order is fixed: **Rules → Signs → Vehicle Controls**. Randomisation is intra-section only.
- Three independent cut-offs per code: each section must pass individually (per test-structure.md).

**Idempotency & Authority:**
- `legislation_code` is a unique key; seeding fails fast on duplicates (preventing ambiguous catalog).
- `sign_ref` resolution is exact-one (`SingleOrDefault`), never ambiguous or unresolved.
- The catalog is seeded once via Infrastructure initializer; idempotent on restart.

## Technical Decisions

**Architecture & Patterns:**
- Brownfield .NET 10 Clean Architecture + CQRS/MediatR; ratify existing conventions from `project.md`.
- Use `IApplicationDbContextFactory` for per-operation contexts (never inject `DbContext` directly).
- DI pattern: Static `DependencyInjection` classes on each layer; entity seeding in `ApplicationDbContextInitializer`.
- EF Core entity configuration via `IEntityTypeConfiguration<T>`; ignore `DomainEvents` collection.

**Data Model:**
- `RoadSign` entity: `legislation_code` (unique constraint), description, image asset key.
- `Question` entity: stem, applicable codes (enum set), shared/code-specific class, language code, and a list of `AnswerOption` records (each with an `is_correct` flag, exactly one per question).
- Reference: resolve `sign_ref` via `DbSet<RoadSign>.SingleOrDefault()` at author/import time; reject if null or ambiguous.

**Persistence & Migration:**
- Schema changes via EF Core migrations in `Migrators.PostgreSQL`; never `EnsureCreated` in production.
- All queries/commands use the factory pattern; inject `IApplicationDbContextFactory _dbContextFactory` and create per-operation contexts with `await using var db = await _dbContextFactory.CreateAsync(cancellationToken)`.

**Caching & Invalidation:**
- Catalog queries (browse, search) use FusionCache at MediatR pipeline level with tag-based invalidation.
- Create a `SignCacheKey` class with `GetAllCacheKey`, per-code search keys, and a `Refresh()` method delegating to `FusionCacheFactory.RemoveByTags(Tags)`.

## Cross-Story Dependencies

- **Story 1.1 → 1.2**: The seeded catalog must exist before it can be browsed.
- **Story 1.1 & 1.3 → Epic 2** (Admin Content Management): The admin import validator depends on sign resolution; questions reference the content model.
- **All three stories → Epic 3+**: Every downstream epic (Exam Engine API, Learner App, Experience UX) depends on a stable, authoritative sign catalog and the content model structure.
