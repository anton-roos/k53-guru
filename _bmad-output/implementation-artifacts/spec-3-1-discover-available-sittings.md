---
title: 'Discover available sittings via the versioned API'
type: 'feature'
created: '2026-08-31'
status: 'done'
baseline_commit: 'cead7c0904180ef6c3471ac12770211737df7a3f'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-3-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** No HTTP API exists in this solution at all — `Server.UI` is Blazor-Server-only. A learner client (Epic 4's Flutter app) has no way to discover which licence-code sittings (single code or valid Code1+2/Code1+3 combination) are currently published and servable.

**Approach:** Stand up the solution's first real API surface: `GET /api/v1/sittings`, backed by a new anonymous, cacheable MediatR query that reads Epic 2's `Test` entity (a `Test` is already a curated, code-scoped sitting definition — publishing one, per Epic 2 Story 2.3, is exactly what makes it "servable"), filtered to `Status == Published` and to the 5 valid `Codes` values (single Code1/2/3, or Code1|Code2, or Code1|Code3 — never Code2|Code3 or all three). Establishes the reusable API-hosting mechanics (controller routing, ProblemDetails, rate limiting, enum-as-string JSON) every later Epic 3 story builds on.

## Boundaries & Constraints

**Always:**
- New controller `Server.UI/Controllers/Api/V1/SittingsController.cs`, `[ApiController]`, `[Route("api/v1/[controller]")]` (literal version segment in the route — no `Asp.Versioning` package; this is a single-version surface today). One action: `GET /api/v1/sittings` -> `List<AvailableSittingDto>`. The controller contains no business logic -- it only calls `Mediator.Send(new GetAvailableSittingsQuery())` and returns the result.
- `app.MapControllers()` must be added to `ConfigureServer` -- `services.AddControllers()` is already called but its routes are never mapped today, so no controller (this one or any future one) is currently reachable.
- New query `GetAvailableSittingsQuery : ICacheableRequest<List<AvailableSittingDto>>` (bare list, not `Result<T>` -- mirrors `TestsWithPaginationQuery`'s convention that read queries return data directly, `Result<T>` is reserved for commands; not paginated, since the realistic result set -- a handful of published sittings -- doesn't warrant it). Cache-tagged with the existing `TestCacheKey.Tags` so `PublishTestCommand`/`UnpublishTestCommand` (Story 2.3, already `ICacheInvalidatorRequest`) automatically invalidate/refresh this query's cache with no new wiring.
- Filter: `Test.Status == TestStatus.Published` AND `Test.Codes` is exactly one of `Code1`, `Code2`, `Code3`, `Code1|Code2`, `Code1|Code3` -- any other combination (`Code2|Code3`, all three, or `None`) is excluded even if published. This filter does not exist anywhere today (`AddEditTestCommandValidator` only rejects `None`/unknown bits, so an admin can already create/publish an invalid-combination `Test`); it lives only in this new query, not as a change to the existing validator (out of scope -- an already-published invalid-combination Test simply never appears here).
- `AvailableSittingDto` (new, `Application/Features/Tests/DTOs/`): `Id` (int), `Codes` (`LicenceCode`), `Name` (string) -- mirrors `TestDto`'s established shape (plain class, `[Description]` per property, nested `Mapping : Profile` with `CreateMap<Test, AvailableSittingDto>(MemberList.None)`). No section/question detail -- that belongs to the "start attempt" endpoint (Story 3.3), not discovery.
- JSON: add `JsonStringEnumConverter` to `AddControllers().AddJsonOptions(...)` so `Codes` serializes as a string (e.g. `"Code1, Code2"`) -- matches the epic's "enums serialize as strings" decision, and nothing configures this today since no controller has ever existed.
- ProblemDetails is already fully wired (`GlobalExceptionHandler` + `AddProblemDetails()` + `UseExceptionHandler()`) -- an unhandled exception in the new handler already produces a `ProblemDetails` 500 response with zero new code. This query has no business-failure path to translate (a parameterless read that returns an empty list when nothing qualifies is still success), so no new exception-to-status mapping is needed for this story.
- Anonymous access: add no `[Authorize]`/`[RequestAuthorize]` to the query or controller. Confirmed no global auth filter exists -- `AuthorizationBehaviour` (Story 2.5) only activates for MediatR requests explicitly decorated with `[RequestAuthorize]`, and ASP.NET Core has no ambient `[Authorize]` applied to controllers by default here.
- Rate limiting: add ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` (already part of the shared framework, no new package) -- one named fixed-window policy (`"learner-api"`, partitioned by client IP, sensible conservative default e.g. 60 requests/minute) registered via `AddRateLimiter` and applied to the controller via `[EnableRateLimiting("learner-api")]`. `app.UseRateLimiter()` added to the pipeline before `app.MapControllers()`. Per-UUID partitioning (mentioned in the epic's architecture notes) doesn't apply here -- no learner identity exists yet at a pre-attempt discovery endpoint; IP-based partitioning is the correct interim scope, revisited once Story 3.3+ introduces a learner UUID.
- HTTPS is already unconditionally enforced (`UseHttpsRedirection`) -- no new work.

**Ask First:**
- None.

**Never:**
- No changes to `AddEditTestCommandValidator`, `Test`, `TestQuestion`, or any Epic 2 file -- this story only reads already-published data through a new query.
- No `TestConfig`/`SectionRule` (Story 3.2, not built) -- this story doesn't need per-code counts/pass-marks/time-limits, only which sittings exist and are published.
- No API versioning package, no CORS configuration (the consumer is a native Flutter client, not a browser-origin caller), no per-UUID rate limiting (no UUID exists at this endpoint yet).
- No pagination on the response.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Published, valid single-code Test exists | One `Test`, `Status=Published`, `Codes=Code1` | Returned in the list | N/A |
| Published, valid combination Test exists | One `Test`, `Status=Published`, `Codes=Code1\|Code2` | Returned in the list | N/A |
| Published, invalid combination Test exists | One `Test`, `Status=Published`, `Codes=Code2\|Code3` | Excluded from the list | N/A |
| Draft Test exists | One `Test`, `Status=Draft`, `Codes=Code1` | Excluded from the list | N/A |
| No qualifying Test exists | No published, valid-combo `Test` rows | Empty list, HTTP 200 | N/A |
| Anonymous call, no credentials | No auth header sent | Request succeeds (not 401/403) | N/A |
| Rate limit exceeded | Same client IP exceeds the configured threshold | Request rejected | HTTP 429 |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Server.UI/Controllers/Api/V1/SittingsController.cs` -- New. `[ApiController] [Route("api/v1/[controller]")] [EnableRateLimiting("learner-api")]`. `GetAvailableSittings()` -> `Mediator.Send(new GetAvailableSittingsQuery())`, no logic beyond that.
- `src/K53Guru/src/Application/Features/Tests/Queries/AvailableSittings/GetAvailableSittingsQuery.cs` -- New. `ICacheableRequest<List<AvailableSittingDto>>`, `CacheKey => TestCacheKey.GetAllCacheKey` (or a dedicated key under the same tag set), `Tags => TestCacheKey.Tags`. Handler: `IApplicationDbContextFactory`-scoped read, `Where(t => t.Status == TestStatus.Published && (t.Codes == LicenceCode.Code1 || t.Codes == LicenceCode.Code2 || t.Codes == LicenceCode.Code3 || t.Codes == (LicenceCode.Code1 | LicenceCode.Code2) || t.Codes == (LicenceCode.Code1 | LicenceCode.Code3)))`, `ProjectTo<AvailableSittingDto>`.
- `src/K53Guru/src/Application/Features/Tests/DTOs/AvailableSittingDto.cs` -- New. `Id`/`Codes`/`Name` + nested `Mapping : Profile` (mirrors `TestDto.cs`'s exact shape).
- `src/K53Guru/src/Server.UI/DependencyInjection.cs:82` -- Modify. `services.AddControllers()` -> add `.AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()))`; add `services.AddRateLimiter(options => options.AddFixedWindowLimiter("learner-api", opt => { opt.PermitLimit = 60; opt.Window = TimeSpan.FromMinutes(1); }))` nearby.
- `src/K53Guru/src/Server.UI/DependencyInjection.cs:~155-195` -- Modify `ConfigureServer`. Add `app.UseRateLimiter();` before controller mapping, and `app.MapControllers();` (e.g. alongside the existing `app.MapHub<ServerHub>(...)`/`MapRazorComponents` calls).
- `src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/GetAvailableSittingsQueryHandlerTests.cs` -- New. Covers all 5 data-shape matrix rows (published/valid, published/invalid-combo, draft, empty) via SQLite in-memory, mirroring `TestsWithPaginationQuery`-adjacent handler test conventions. The anonymous-access and rate-limit rows are integration/manual-check concerns (no controller-level test harness exists yet in this repo), covered under Verification below instead.
- Review fix (blind-hunter + edge-case-hunter, both confirmed via decompilation of the actual installed framework assembly): `DependencyInjection.cs`'s rate limiter used `AddFixedWindowLimiter("learner-api", ...)`, which partitions by the constant policy-name string, not the caller -- every anonymous learner shared ONE global 60-req/min budget rather than 60/min each. Replaced with `AddPolicy("learner-api", httpContext => RateLimitPartition.GetFixedWindowLimiter(partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ...))` for genuine per-IP partitioning.
- Review fix (blind-hunter, confirmed via reflection against the installed framework assembly): rejections defaulted to HTTP 503, not the spec-mandated 429 -- `RateLimiterOptions.RejectionStatusCode` was never overridden. Added `options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;`.
- Review fix (verification-gap): `GetAvailableSittingsQueryHandlerTests.cs`'s assertions only ever checked `Name`/`Codes`, never `Id` -- a mapping regression dropping `Id` from the `CreateMap` would have passed every test. Added `Assert.Equal(test.Id, sitting.Id)` to the single-code and combination tests.
- Review fix (verification-gap): zero test in the repo touched `SittingsController` -- 100% of coverage bypassed it via the handler directly. New `SittingsControllerTests.cs` constructs the controller with a mocked `ISender` and asserts it sends exactly one `GetAvailableSittingsQuery` and returns the mediator's result unchanged. Required adding a `ProjectReference` to `Server.UI.csproj` from `Infrastructure.UnitTests.csproj` (the first time a test project has needed to reference the controller-hosting project).
- Review fix (verification-gap): no test verified `GetAvailableSittingsQuery`'s caching + `PublishTestCommand`'s tag-based invalidation actually work together end-to-end (unit tests all bypass the MediatR pipeline). New `Application.IntegrationTests/Tests/Queries/AvailableSittingsCachingTests.cs` mirrors Story 2.5's `AuthorizationTests.cs` pattern: seeds a Draft Test, confirms it's absent from the query result, publishes it, confirms it now appears -- proving the cache was actually invalidated, not just serving stale/empty data. Cannot be verified as passing in this sandbox due to the pre-existing, already-logged MSSQL migration-drift issue (same accepted limitation as Story 2.5).

## Tasks & Acceptance

**Execution:**
- [x] `AvailableSittingDto.cs` -- create the DTO + AutoMapper profile.
- [x] `GetAvailableSittingsQuery.cs` (+handler) -- create the cacheable, filtered discovery query.
- [x] `DependencyInjection.cs` -- add `AddJsonOptions` (enum-as-string), `AddRateLimiter` (learner-api policy), `UseRateLimiter`, `MapControllers`.
- [x] `SittingsController.cs` -- create the first API controller, one thin GET action.
- [x] `GetAvailableSittingsQueryHandlerTests.cs` -- add tests covering the data-shape matrix rows.

**Acceptance Criteria:**
- Given published content exists, when the client calls `GET /api/v1/sittings`, then it receives only published, servable options (single codes and valid Code1+2/Code1+3 combinations) as `AvailableSittingDto`s -- never EF entities, never draft content.
- Given the API surface, when any response is returned, then it is served over HTTPS and delegates entirely to the Application-layer query (no business logic in the controller).
- Given anonymous access, when the endpoint is called without credentials, then the request succeeds, and requests beyond the configured per-IP threshold receive HTTP 429.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj --filter GetAvailableSittingsQueryHandlerTests` -- expected: all matrix-row tests pass.

**Manual checks (if no CLI):**
- Run the app, `curl -k https://localhost:7256/api/v1/sittings` with no auth header -- confirm a 200 with a JSON array (enums as strings, e.g. `"codes": "Code1"`); publish/unpublish a Test via the Admin Panel and re-call to confirm the list updates; fire >60 requests within a minute and confirm a 429 appears.

**Known environment gap:** `Application.IntegrationTests` cannot execute in this sandbox -- all 29 tests (28 pre-existing + the new `AvailableSittingsCachingTests.cs`) fail identically at `OneTimeSetUp` with an EF Core `PendingModelChangesWarning` against the MSSQL provider. Confirmed pre-existing at this story's own `baseline_commit`, logged in `deferred-work.md`. The new caching test is believed correct by code review and mirrors Story 2.5's established `AuthorizationTests.cs` pattern, but is not independently verified as passing pending that migration-drift fix.

## Suggested Review Order

**API surface (the core of this story)**

- The solution's first controller -- thin, one action, no logic.
  [`SittingsController.cs:17`](../../src/K53Guru/src/Server.UI/Controllers/Api/V1/SittingsController.cs#L17)

- The discovery query: cache-tagged, filtered to the 5 valid `Codes` values.
  [`GetAvailableSittingsQuery.cs:14`](../../src/K53Guru/src/Application/Features/Tests/Queries/AvailableSittings/GetAvailableSittingsQuery.cs#L14), filter at [`GetAvailableSittingsQuery.cs:38`](../../src/K53Guru/src/Application/Features/Tests/Queries/AvailableSittings/GetAvailableSittingsQuery.cs#L38)

- The response DTO, mirroring `TestDto`'s established shape.
  [`AvailableSittingDto.cs`](../../src/K53Guru/src/Application/Features/Tests/DTOs/AvailableSittingDto.cs)

**Pipeline wiring**

- Review fix: genuine per-IP rate-limit partitioning (previously a single global bucket).
  [`DependencyInjection.cs:98`](../../src/K53Guru/src/Server.UI/DependencyInjection.cs#L98)

- Review fix: 429 rejection status (previously defaulted to 503).
  [`DependencyInjection.cs:97`](../../src/K53Guru/src/Server.UI/DependencyInjection.cs#L97)

- `UseRateLimiter()` before `MapControllers()` -- the first routes ever mapped in this solution.
  [`DependencyInjection.cs:223`](../../src/K53Guru/src/Server.UI/DependencyInjection.cs#L223)

**Test coverage**

- All 5 data-shape matrix rows plus the `Id`-assertion review fix, against a SQLite in-memory context.
  [`GetAvailableSittingsQueryHandlerTests.cs:34`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/GetAvailableSittingsQueryHandlerTests.cs#L34), fix at lines [101](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/GetAvailableSittingsQueryHandlerTests.cs#L101)/[138](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/GetAvailableSittingsQueryHandlerTests.cs#L138)

- Review fix (verification-gap finding): the first controller-level test in the repo.
  [`SittingsControllerTests.cs:21`](../../src/K53Guru/tests/Infrastructure.UnitTests/Features/Tests/SittingsControllerTests.cs#L21)

- Review fix (verification-gap finding): end-to-end cache-invalidation proof through the real DI-wired pipeline.
  [`AvailableSittingsCachingTests.cs:28`](../../src/K53Guru/tests/Application.IntegrationTests/Tests/Queries/AvailableSittingsCachingTests.cs#L28)
