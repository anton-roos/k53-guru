---
title: 'Restrict content management to authenticated admins'
type: 'feature'
created: '2026-08-30'
status: 'done'
baseline_commit: '4629f34a5015fc13ae4fbf7cff4779af63184aa9'
review_loop_iteration: 0
context:
  - '{project-root}/_bmad-output/implementation-artifacts/epic-2-context.md'
---

<frozen-after-approval reason="human-owned intent — do not modify unless human renegotiates">

## Intent

**Problem:** `AddEditQuestionCommand`, `ImportQuestionsCommand`, `AddEditTestCommand`, `PublishTestCommand`, and `UnpublishTestCommand` have zero authorization at the command/handler level — only their Blazor pages check permissions to hide/show buttons. Any code holding an `ISender`/`IMediator` (another page, a future API endpoint, a test) can call these directly with no server-side gate, which contradicts Epic 2's requirement that content-management actions be "restricted to authenticated admins."

**Approach:** Finish the codebase's already-scaffolded-but-unused `RequestAuthorizeAttribute` + MediatR pipeline pattern: add one new `AuthorizationBehaviour<TRequest, TResponse>` pipeline behavior that checks `[RequestAuthorize(Roles = Roles.Admin)]` on the five commands above against the current user's roles (`IUserContextAccessor`), and a matching `ForbiddenAccessException`/`ForbiddenAccessExceptionHandler` pair (mirroring the existing `ValidationException`/`ValidationExceptionHandler` conversion pattern) that turns a failed check into a clean `Result.Failure`. No new auth mechanism, no new role, no granular author/publish/import split — matches the epic's explicit "any authenticated admin may perform any of these actions" default.

## Boundaries & Constraints

**Always:**
- Reuse `RequestAuthorizeAttribute` (`Application/Common/Security/RequestAuthorizeAttribute.cs`, exists, unused) exactly as-is — apply `[RequestAuthorize(Roles = Roles.Admin)]` to the five command classes named above. Do not add a `Policy`-based variant; role-only matches the epic's stated scope.
- `AuthorizationBehaviour<TRequest, TResponse>` reads `RequestAuthorizeAttribute`s off `typeof(TRequest)` via reflection; a request with none passes through untouched (`return await next()` immediately) — every other command in the app (Products, PicklistSets, RoadSigns, etc.) must be completely unaffected.
- Authorization check: `_userContextAccessor.Current` must be non-null AND its `Roles` must contain at least one role named by the attribute(s) (comma-split, matching the attribute's existing `Roles` property contract). Failing either throws `ForbiddenAccessException`.
- Register `AuthorizationBehaviour` via `config.AddOpenBehavior(typeof(AuthorizationBehaviour<,>))` in `Application/DependencyInjection.cs`, added FIRST (before `PerformanceBehaviour`/`FusionCacheBehaviour`/`CacheInvalidationBehaviour`) so a rejected request never touches cache or triggers invalidation.
- `ForbiddenAccessException`/`ForbiddenAccessExceptionHandler<TRequest,TResponse,TException>` mirror `Common/ExceptionHandlers/NotFoundException.cs` / `ValidationExceptionHandler.cs` exactly in shape (same reflection-based `Result`/`Result<T>` construction, same `IRequestExceptionHandler<,,>` MediatR auto-discovery — no explicit DI registration needed, matching how `ValidationExceptionHandler` itself needs none).
- Existing handler unit tests (`AddEditQuestionCommandHandlerTests.cs`, `ImportQuestionsCommandHandlerTests.cs`, `PublishUnpublishTestCommandHandlerTests.cs`, `AddEditTestCommandHandlerTests.cs`) call `handler.Handle(...)` directly, never through `IMediator.Send`/`ISender.Send` — a pipeline behavior never executes on that path, so these files need zero changes and must still pass unmodified.

**Ask First:**
- None.

**Never:**
- No changes to `AddEditQuestionCommandHandler`, `ImportQuestionsCommandHandler`, `AddEditTestCommandHandler`, `PublishTestCommandHandler`, `UnpublishTestCommandHandler`, or any existing handler test — this story adds a pipeline-level gate in front of them, not in-handler checks.
- No new role, policy, or claim — `Roles.Admin` (`Application/Common/Constants/Roles.cs`) already exists and is already the only role seeded with every `Permissions.Questions.*`/`Permissions.Tests.*` claim (`ApplicationDbContextInitializer.SeedRolesAsync`).
- No fix for the pre-existing, unrelated dead `GlobalExceptionBehaviour` (`Application/Pipeline/GlobalExceptionBehaviour.cs`, never registered) — out of scope, unrelated to authorization.
- No UI changes — `Questions.razor`/`Tests.razor`'s existing `_accessRights`-driven button visibility is unaffected and untouched; this story closes the server-side gap behind it, not the UI.

## I/O & Edge-Case Matrix

| Scenario | Input / State | Expected Output / Behavior | Error Handling |
|----------|--------------|---------------------------|----------------|
| Admin sends a guarded command | Current user has `Roles.Admin` | Request proceeds to the handler exactly as before | N/A |
| Non-admin sends a guarded command | Authenticated user without `Roles.Admin` (e.g. `Basic`) | Rejected before the handler runs; nothing persisted | `Result.Failure`, clear "not authorized" message |
| No user context sends a guarded command | `IUserContextAccessor.Current` is `null` | Rejected before the handler runs; nothing persisted | `Result.Failure`, clear "not authorized" message |
| Unguarded command sent by anyone | e.g. `ImportPicklistSetsCommand` (no `[RequestAuthorize]`) | Proceeds exactly as before this story | N/A |

</frozen-after-approval>

## Code Map

- `src/K53Guru/src/Application/Pipeline/AuthorizationBehaviour.cs` -- New. `IPipelineBehavior<TRequest, TResponse> where TResponse : IResult` (mirrors `ValidationExceptionHandler`'s constraint). Reflects `RequestAuthorizeAttribute`s off `typeof(TRequest)`; if none, `return await next()`; else checks `IUserContextAccessor.Current?.Roles` against the attribute(s)' comma-split `Roles`, throwing `ForbiddenAccessException` on mismatch/null.
- `src/K53Guru/src/Application/Common/ExceptionHandlers/ForbiddenAccessException.cs` -- New. Mirrors `NotFoundException.cs`'s minimal `Exception` subclass shape.
- `src/K53Guru/src/Application/Common/ExceptionHandlers/ForbiddenAccessExceptionHandler.cs` -- New. `IRequestExceptionHandler<TRequest, TResponse, ForbiddenAccessException>`, mirrors `ValidationExceptionHandler.cs`'s reflection-based `Result`/`Result<T>.Failure(...)` construction verbatim (adapted for a single message instead of an error array).
- `src/K53Guru/src/Application/DependencyInjection.cs:20-29` -- Modify. Add `config.AddOpenBehavior(typeof(AuthorizationBehaviour<,>));` as the first line inside the `AddMediatR` config block, before the existing three `AddOpenBehavior` calls.
- `src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs:7` -- Modify. Add `[RequestAuthorize(Roles = Roles.Admin)]` above the `AddEditQuestionCommand` class declaration.
- `src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs:10` -- Modify. Same attribute on `ImportQuestionsCommand`.
- `src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs:7` -- Modify. Same attribute on `AddEditTestCommand`.
- `src/K53Guru/src/Application/Features/Tests/Commands/Publish/PublishTestCommand.cs:6` -- Modify. Same attribute on `PublishTestCommand`.
- `src/K53Guru/src/Application/Features/Tests/Commands/Unpublish/UnpublishTestCommand.cs:6` -- Modify. Same attribute on `UnpublishTestCommand`.
- `src/K53Guru/tests/Infrastructure.UnitTests/Pipeline/AuthorizationBehaviourTests.cs` -- New. Covers all 4 matrix rows directly against the behavior (constructing it with a fake/stub `IUserContextAccessor`, a decorated and an undecorated dummy request type, and a no-op `next()` delegate) -- does not need a DbContext.
- Review fix: `AuthorizationBehaviour.cs` now requires `IUserContextAccessor.Current` to be non-null whenever ANY `[RequestAuthorize]` attribute is present, even one with an empty/unspecified `Roles` -- previously an empty-`Roles` attribute silently skipped the entire check (including the null-user check), identically to having no attribute at all.
- Review fix: `AuthorizationBehaviourTests.cs` gained a `GuardedCommandNoRolesSpecified` dummy type + a test for the fix above, plus `Assert.Same(expectedInstance, result)` on both "proceeds" tests (previously only `.Succeeded` was checked, which couldn't distinguish the real `next()` result from a fabricated one) and a message assertion on the no-user-context rejection test.
- Review fix: `Application.IntegrationTests/Testing.cs`'s mocked `IUserContextAccessor` now populates `Roles` (threaded through from `RunAsUserAsync`'s `roles` parameter via a new `_currentUserRoles` field, evaluated lazily per-call since the mock is a singleton) -- previously `Roles` was always `null` regardless of which roles a test's simulated user held, which would have falsely rejected every guarded command sent via the standard `RunAsAdministratorAsync()` + `SendAsync(...)` integration-test pattern.
- Review fix: new `Application.IntegrationTests/Tests/Commands/AuthorizationTests.cs` -- sends the REAL `PublishTestCommand` (not a dummy stand-in) through the actual DI-wired `IMediator` pipeline as both an Administrator (succeeds) and a non-admin (`Result.Failure`, nothing persisted) -- closes the gap where no test in the repo exercised any of the five real decorated commands end-to-end, so a misapplied attribute or role-string typo on production code would have been invisible to every other test in the diff.

## Tasks & Acceptance

**Execution:**
- [x] `AuthorizationBehaviour.cs` -- create the reflection-driven role-check pipeline behavior.
- [x] `ForbiddenAccessException.cs` -- create the exception type.
- [x] `ForbiddenAccessExceptionHandler.cs` -- create the exception-to-`Result.Failure` handler.
- [x] `DependencyInjection.cs` -- register `AuthorizationBehaviour` first in the pipeline.
- [x] Add `[RequestAuthorize(Roles = Roles.Admin)]` to `AddEditQuestionCommand`, `ImportQuestionsCommand`, `AddEditTestCommand`, `PublishTestCommand`, `UnpublishTestCommand`.
- [x] `AuthorizationBehaviourTests.cs` -- add tests covering all 4 matrix rows.

**Acceptance Criteria:**
- Given an authenticated user with the `Admin` role, when they send any of the five guarded commands via `IMediator`/`ISender`, then the command executes exactly as it did before this story.
- Given an authenticated user without the `Admin` role, or no user context at all, when they send any of the five guarded commands via `IMediator`/`ISender`, then the command is rejected with a clear message before the handler runs, and nothing is persisted.
- Given any command NOT decorated with `[RequestAuthorize]`, when it is sent, then its behavior is completely unchanged by this story.

## Verification

**Commands:**
- `dotnet build src/K53Guru/K53Guru.slnx` -- expected: build succeeds with no errors.
- `dotnet test src/K53Guru/tests/Infrastructure.UnitTests/Infrastructure.UnitTests.csproj` -- expected: full suite passes, including the new `AuthorizationBehaviourTests` and every pre-existing test unmodified and green.

**Manual checks (if no CLI):**
- Sign in as the seeded `Administrator` user, confirm authoring/importing a question, organising a test, and publishing/unpublishing all still work exactly as before via `/system/questions` and `/system/tests`.

**Known environment gap:** `Application.IntegrationTests` cannot execute in this sandbox -- all 28 tests (26 pre-existing + the 2 new `AuthorizationTests.cs`) fail identically at `OneTimeSetUp` with an EF Core `PendingModelChangesWarning` against the MSSQL provider. Confirmed via `git stash` comparison that this failure is pre-existing at the story's own `baseline_commit`, unrelated to any change here -- see the escalated `deferred-work.md` entry. `AuthorizationTests.cs` is believed correct by code review and mirrors the codebase's established integration-test conventions, but is not independently verified as passing pending that migration-drift fix.

## Suggested Review Order

**Pipeline core**

- Entry point: reflection over `[RequestAuthorize]`, pass-through when absent.
  [`AuthorizationBehaviour.cs:25`](../../src/K53Guru/src/Application/Pipeline/AuthorizationBehaviour.cs#L25)

- Review fix: unconditional null-user check whenever any `[RequestAuthorize]` attribute is present, closing the empty-`Roles` bypass.
  [`AuthorizationBehaviour.cs:44`](../../src/K53Guru/src/Application/Pipeline/AuthorizationBehaviour.cs#L44)

- Role-membership check, only when the attribute(s) actually specify `Roles`.
  [`AuthorizationBehaviour.cs:49`](../../src/K53Guru/src/Application/Pipeline/AuthorizationBehaviour.cs#L49)

- Exception-to-`Result.Failure` conversion, mirroring `ValidationExceptionHandler`.
  [`ForbiddenAccessExceptionHandler.cs`](../../src/K53Guru/src/Application/Common/ExceptionHandlers/ForbiddenAccessExceptionHandler.cs)

- Registration order -- first in the pipeline, before caching/invalidation.
  [`DependencyInjection.cs:25`](../../src/K53Guru/src/Application/DependencyInjection.cs#L25)

**Guarded commands**

- The five decorated commands: [`AddEditQuestionCommand.cs:8`](../../src/K53Guru/src/Application/Features/Questions/Commands/AddEdit/AddEditQuestionCommand.cs#L8), [`ImportQuestionsCommand.cs:11`](../../src/K53Guru/src/Application/Features/Questions/Commands/Import/ImportQuestionsCommand.cs#L11), [`AddEditTestCommand.cs:8`](../../src/K53Guru/src/Application/Features/Tests/Commands/AddEdit/AddEditTestCommand.cs#L8), [`PublishTestCommand.cs:7`](../../src/K53Guru/src/Application/Features/Tests/Commands/Publish/PublishTestCommand.cs#L7), [`UnpublishTestCommand.cs:7`](../../src/K53Guru/src/Application/Features/Tests/Commands/Unpublish/UnpublishTestCommand.cs#L7).

**Test coverage**

- All 4 matrix rows plus the review-fix regression test, directly against the production behaviour.
  [`AuthorizationBehaviourTests.cs:27`](../../src/K53Guru/tests/Infrastructure.UnitTests/Pipeline/AuthorizationBehaviourTests.cs#L27)

- Review fix: `Assert.Same` strengthening so a fabricated result can no longer masquerade as `next()`'s real output.
  [`AuthorizationBehaviourTests.cs:73`](../../src/K53Guru/tests/Infrastructure.UnitTests/Pipeline/AuthorizationBehaviourTests.cs#L73)

- Review fix (verification-gap finding): a REAL guarded command through the REAL DI-wired pipeline, as both Admin and non-admin.
  [`AuthorizationTests.cs:19`](../../src/K53Guru/tests/Application.IntegrationTests/Tests/Commands/AuthorizationTests.cs#L19)

- Review fix: the integration-test harness's mocked user context now actually carries roles.
  [`Testing.cs:80`](../../src/K53Guru/tests/Application.IntegrationTests/Testing.cs#L80)
