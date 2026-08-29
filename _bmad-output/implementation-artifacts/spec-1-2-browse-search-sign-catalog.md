---
title: 'Browse and search the sign catalog in the Admin Panel'
type: 'feature'
created: '2026-08-29'
status: 'done'
baseline_commit: '72be5ba619a6d9d196239637254d5036c7be7459'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-1-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** The seeded `RoadSign` catalog (Story 1.1) is only reachable via direct SQL — a K53 domain expert has no way to browse or search it when authoring a question and looking up the correct `legislation_code`.

**Approach:** Add a read-only, paged, searchable Blazor Admin Panel page for `RoadSign`, backed by a new cached CQRS query/detail-query pair, following the existing Products/PicklistSets/Contacts pattern (MudDataGrid + Ardalis Specification + FusionCache) exactly — no new architectural pattern introduced.

## Boundaries & Constraints

**Always:**
- Mirror the existing CQRS+FusionCache pattern exactly: a `RoadSignsWithPaginationQuery` (`ICacheableRequest<PaginatedData<RoadSignDto>>`) and `GetRoadSignByIdQuery` (`ICacheableRequest<Result<RoadSignDto>>`), each with a handler using `IApplicationDbContextFactory` (never inject `DbContext`).
- Search matches `LegislationCode` OR `Description` via `.Contains(filter.Keyword)`, same style as `ProductAdvancedSpecification`/`PicklistSetAdvancedSpecification`.
- Add `RoadSignCacheKey` (`GetAllCacheKey`, `GetByIdCacheKey`, `GetPaginationCacheKey`, `Tags`, `Refresh()`) mirroring `PicklistSetCacheKey`.
- Gate the page with `[Authorize(Policy = Permissions.RoadSigns.View)]`; add a minimal `RoadSignsPermissions` (`View`) mirroring `PicklistSetsPermissions`.
- Register the page in `MenuService.cs`'s existing admin "MANAGEMENT" > System section.

**Ask First:**
- `RoadSign.ImageAssetKey` (e.g. `"signs/r1.png"`) has no established resolution convention in this codebase (confirmed: doesn't match the `/Files` upload-store pattern used by `Product`). Default to rendering it as `/img/{ImageAssetKey}` via standard static-file serving. If a different asset convention surfaces during implementation, pause and confirm before wiring it in.

**Never:**
- No Create/Edit/Delete for `RoadSign` — browse/search/detail only, per Story 1.2's acceptance criteria.
- Do not touch `Question`/`AnswerOption` (Story 1.3) or `RoadSign`'s schema/seed data (Story 1.1, done).

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Search by code | Keyword `"R1"` | Only rows whose `LegislationCode` or `Description` contains it | N/A |
| Search by keyword | Keyword `"stop"` | Only rows whose `Description` contains it | N/A |
| No match | Keyword matches nothing | Empty grid, no error | N/A |
| Detail view | Valid `Id` | Full `LegislationCode`, `Description`, `ImageAssetKey` shown | N/A |
| Detail view, missing | Nonexistent `Id` | User-facing not-found state | `NotFoundException` |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Application/Features/RoadSigns/DTOs/RoadSignDto.cs` -- New. AutoMapper projection: `Id`, `LegislationCode`, `Description`, `ImageAssetKey`.
- `src/K53Guru/src/Application/Features/RoadSigns/Caching/RoadSignCacheKey.cs` -- New. Mirror `PicklistSetCacheKey.cs`.
- `src/K53Guru/src/Application/Features/RoadSigns/Specifications/RoadSignAdvancedSpecification.cs` -- New. Mirror `ProductAdvancedSpecification.cs`.
- `src/K53Guru/src/Application/Features/RoadSigns/Specifications/RoadSignByIdSpecification.cs` -- New. Mirror `ContactByIdSpecification`.
- `src/K53Guru/src/Application/Features/RoadSigns/Queries/Pagination/RoadSignsWithPaginationQuery.cs` -- New. Mirror `ProductsWithPaginationQuery.cs:11-49` (query+handler in one file), uses `QueryableExtensions.ProjectToPaginatedDataAsync`.
- `src/K53Guru/src/Application/Features/RoadSigns/Queries/GetById/GetRoadSignByIdQuery.cs` -- New. Mirror `GetContactByIdQuery.cs:24-53`.
- `src/K53Guru/src/Application/Features/RoadSigns/Security/RoadSignsPermissions.cs` -- New. Mirror `PicklistSetsPermissions.cs:8-46`; extend the `Permissions` partial class.
- `src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor` -- New. `@page "/system/roadsigns"`. Mirror `Products.razor` (MudDataGrid `ServerData`, search `MudTextField`) and `Products/Components/Thumbnail.razor` for the image cell.
- `src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs:114-135` -- Add a nav entry ("Road Signs", `/system/roadsigns`) inside the existing admin System `MenuItems` list.
- `src/K53Guru/tests/Application.IntegrationTests/RoadSigns/Queries/RoadSignsPaginationQueryTests.cs` -- New. Mirror `Products/Queries/ProductsPaginationQueryTests.cs` (seed via `AddAsync`, assert via `SendAsync`). Documents intended full-stack coverage; cannot execute in this sandbox (no live DB for any provider -- pre-existing, confirmed `ProductsPaginationQueryTests` fails identically here).
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/RoadSigns/RoadSignsQueryHandlerTests.cs` -- New. Covers all 5 I/O matrix rows against a SQLite in-memory `ApplicationDbContext`, invoking the real handlers directly (bypasses the MSSQL/PostgreSQL migration drift, same pattern as Story 1.1's `RoadSignSeedingTests.cs`). This is the coverage that actually runs and passes in this environment.
- `src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs` -- Added `DbSet<RoadSign> RoadSigns { get; set; }` (was missing from the interface even though the concrete `ApplicationDbContext` and EF config existed since Story 1.1; required for the new handlers to compile against the interface).
- `src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignDetailDialog.razor` -- New. Read-only detail dialog invoking `GetRoadSignByIdQuery`, opened via a per-row view icon on the grid.
- Review fix: `RoadSigns.razor`/`RoadSignDetailDialog.razor` image URL builders now return `null` (not `""`) for a missing `ImageAssetKey`, per-segment `Uri.EscapeDataString`-escape the key, and the grid/dialog render a placeholder icon instead of an empty `<img src>` (which would otherwise cause the browser to re-request the current page).
- Review fix: removed an incorrect copy-pasted ".NET Foundation" MIT license header from the 5 new `Application/Features/RoadSigns` files; left header-less to match Story 1.1's convention.

## Tasks & Acceptance

**Execution:**
- [x] `RoadSignDto.cs` -- create DTO -- projection target for both queries.
- [x] `RoadSignCacheKey.cs` -- create cache key class -- enables FusionCache pipeline caching/invalidation.
- [x] `RoadSignAdvancedSpecification.cs`, `RoadSignByIdSpecification.cs` -- create specifications -- keyword search + single-row lookup.
- [x] `RoadSignsWithPaginationQuery.cs`, `GetRoadSignByIdQuery.cs` (+handlers) -- create cacheable queries -- power the grid and detail view.
- [x] `RoadSignsPermissions.cs` -- create `View` permission -- gates the page.
- [x] `RoadSigns.razor` -- create Admin Panel page -- MudDataGrid + search box + detail view, per `Products.razor`.
- [x] `MenuService.cs` -- add nav entry -- makes the page reachable from admin nav.
- [x] `RoadSignsPaginationQueryTests.cs` -- add integration test -- covers the I/O matrix's search/no-match rows.

**Acceptance Criteria:**
- Given I am an authorized admin on `/system/roadsigns`, when the page loads, then I see a paged grid of signs showing image, `legislation_code`, and description.
- Given I lack the `RoadSigns.View` permission, when I navigate to `/system/roadsigns`, then access is denied.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Application.IntegrationTests/Application.IntegrationTests.csproj --filter RoadSigns` -- expected: new tests pass (falls back to a documented environment note if no live DB is reachable, matching the constraint already recorded for Story 1.1's local sandbox).

**Manual checks (if no CLI):**
- Sign in as an Admin, navigate to `/system/roadsigns`, confirm the grid loads seeded signs and the search box filters by code/keyword.

## Suggested Review Order

**CQRS query + caching**

- Entry point: keyword search over `LegislationCode`/`Description`, the core behavior everything else serves.
  [`RoadSignAdvancedSpecification.cs:12`](../../src/K53Guru/src/Application/Features/RoadSigns/Specifications/RoadSignAdvancedSpecification.cs#L12)

- Pagination query + handler powering the grid; uses `IApplicationDbContextFactory` and the shared `ProjectToPaginatedDataAsync` extension.
  [`RoadSignsWithPaginationQuery.cs:7`](../../src/K53Guru/src/Application/Features/RoadSigns/Queries/Pagination/RoadSignsWithPaginationQuery.cs#L7)

- Detail query + handler; throws `NotFoundException` on a missing `Id`, converted to a failed `Result` by the existing MediatR pipeline.
  [`GetRoadSignByIdQuery.cs:7`](../../src/K53Guru/src/Application/Features/RoadSigns/Queries/GetById/GetRoadSignByIdQuery.cs#L7)

- Cache-key class enabling FusionCache pipeline caching/invalidation for both queries above.
  [`RoadSignCacheKey.cs:3`](../../src/K53Guru/src/Application/Features/RoadSigns/Caching/RoadSignCacheKey.cs#L3)

- DTO projection target shared by both queries.
  [`RoadSignDto.cs:4`](../../src/K53Guru/src/Application/Features/RoadSigns/DTOs/RoadSignDto.cs#L4)

- Was missing from the interface despite existing on the concrete context since Story 1.1; required for the handlers above to compile.
  [`IApplicationDbContext.cs:21`](../../src/K53Guru/src/Application/Common/Interfaces/IApplicationDbContext.cs#L21)

**Access control**

- The `View` permission gating the page; auto-discovered and assignable to roles via the codebase's existing reflection-based permission scan.
  [`RoadSignsPermissions.cs:12`](../../src/K53Guru/src/Application/Features/RoadSigns/Security/RoadSignsPermissions.cs#L12)

**Admin Panel UI**

- Route + grid wiring; note the review-fixed `GetImageUrl` (returns `null`, not `""`, for a missing image, escaping each path segment).
  [`RoadSigns.razor:1`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor#L1)

- Placeholder-vs-image branch in the grid's image cell — the concrete fix for the empty-`src` browser re-fetch bug caught in review.
  [`RoadSigns.razor:60`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor#L60)

- Detail-view dispatch; routes `NotFoundException`/other failures to a Snackbar via `Result.Match`.
  [`RoadSigns.razor:152`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/RoadSigns.razor#L152)

- Read-only detail dialog; same image-fallback fix applied here.
  [`RoadSignDetailDialog.razor:46`](../../src/K53Guru/src/Server.UI/Pages/RoadSigns/Components/RoadSignDetailDialog.razor#L46)

- Nav entry making the page reachable from the admin System menu.
  [`MenuService.cs:130`](../../src/K53Guru/src/Server.UI/Services/Navigation/MenuService.cs#L130)

**Test coverage (peripherals)**

- Covers all 5 I/O matrix rows against a SQLite in-memory context, invoking the real handlers directly — the coverage that actually runs and passes in this sandbox.
  [`RoadSignsQueryHandlerTests.cs:44`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/RoadSigns/RoadSignsQueryHandlerTests.cs#L44)

- Full-stack equivalent kept as documentation of intended coverage; cannot run here (see `deferred-work.md`).
  [`RoadSignsPaginationQueryTests.cs:12`](../../src/K53Guru/tests/Application.IntegrationTests/RoadSigns/Queries/RoadSignsPaginationQueryTests.cs#L12)
