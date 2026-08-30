# Epic 2 Context: Admin Content Management

<!-- Compiled from planning artifacts. Edit freely. Regenerate with compile-epic-context if planning docs change. -->

## Goal

A K53 domain expert, working from the Blazor Admin Panel, can author multiple-choice questions and tests that reference catalog signs, bulk-import a question bank via CSV/JSON with strict reject-on-error validation, and publish/unpublish content — producing the exact set of approved, published content the learner API is allowed to serve. This is the content-production side of the system: nothing reaches a learner that hasn't passed through authoring/import validation and an explicit publish action here. It builds directly on Epic 1's sign catalog and content model, and its output is the input Epic 3's exam engine composes sittings from.

## Stories

- Story 2.1: Author and edit a question
- Story 2.2: Organise questions into a test
- Story 2.3: Publish and unpublish a test
- Story 2.4: Import a question bank via CSV/JSON with reject-on-error validation
- Story 2.5: Restrict content management to authenticated admins

## Requirements & Constraints

- Authored questions, options, and correct answers must be persisted and retrievable by the API exactly as authored — no divergence between what an admin builds and what a learner is served.
- CSV/JSON import of a well-formed file must produce content delivered identically to a hand-authored question; there is no second-class "imported" content path.
- Every question — whether hand-authored or imported — must validate against the question schema (required fields, exactly one correct option) and, when it references a sign, resolve `sign_ref` to exactly one catalog sign. A row/question with a missing required field or an unresolved/ambiguous `sign_ref` is rejected outright; the offending row/field is identified, and nothing is partially persisted.
- Road signs and questions must reference the same authoritative legislative-code source established by the catalog — no ad-hoc labels.
- Editing an existing question's content must only affect future attempts; a live, in-progress learner attempt is never mutated (attempts are immutable snapshots — see Epic 3).
- Unpublishing a test removes it from future delivery immediately but must not disturb attempts already snapshotted from it.
- A draft/unpublished test must never be returned by the learner API.
- Content-management actions (author, import, publish) are restricted to authenticated admins; the working default is "any authenticated admin may perform any of these actions" — a finer author/publish/import role split is an open question, deferred past v1.
- Content copyright/legality of reproducing official K53 question-bank material is unresolved; the working assumption is original, expert-authored content. This gates what content can actually be populated, not the stories themselves.

## Technical Decisions

- Follows existing Clean Architecture + CQRS conventions: all business logic (authoring validation, import validation, publish state changes) lives in `Application/Features/Questions` (and related) as MediatR commands/queries — Blazor pages contain no business logic. DB access uses per-operation `IApplicationDbContextFactory` contexts, never a shared injected `DbContext`.
- The Admin Panel and the learner-facing API must never compute things differently: the API (built in Epic 3) delegates to these same Application-layer handlers, so authoring/publish logic built here is the single source of truth.
- `sign_ref` resolution is exact-one (`SingleOrDefault`, never `First`) at both author time and import time — an unresolved or ambiguous reference is a hard rejection, not a warning.
- Import validation is all-or-nothing per row: schema and catalog checks run before any persistence, and the existing import-command / `IExcelService` pattern in the codebase is the pattern to reuse rather than building a new import mechanism.
- Content carries the two-axis model from Epic 1 (applicable code(s) in `{Code1, Code2, Code3}`, `shared` vs `code-specific` class, `language_code` defaulting to `en`) — authoring and import must populate and validate these axes, not just stem/options.
- Authorization uses the existing ASP.NET Identity + roles system already in the solution (distinct from the learner API's anonymous UUID model in Epic 4) — no new auth mechanism is introduced for Admin.
- Writes go through MediatR commands; validation uses FluentValidation pipeline behaviours; reads use Ardalis.Specification — consistent with the rest of the backend.

## UX & Interaction Patterns

The Admin surface is Blazor, desktop-first, and utilitarian — function over polish. It inherits the shared design tokens (from the Flutter-oriented DESIGN.md) via a MudBlazor component library rather than reimplementing them. Its core screens/jobs are: author a question, bulk-import CSV/JSON with reject-on-error feedback, manage the sign catalog (built in Epic 1), and publish/unpublish content. No further Admin-specific interaction detail exists in the design artifacts beyond this — screen-level layout is left to implementation.

## Cross-Story Dependencies

- Depends on Epic 1: questions authored or imported here reference catalog signs via `sign_ref` and use the two-axis content model (code applicability, shared/code-specific class) defined there.
- Feeds Epic 3: only content published via Story 2.3 is eligible for the learner API to compose sittings from; unpublished/draft content must stay invisible to that API.
- Within the epic: Story 2.1 (author) and Story 2.4 (import) are two paths into the same stored-question model and must produce indistinguishable results; Story 2.2 (organise into a test) consumes questions from either path; Story 2.3 (publish/unpublish) gates a test built in 2.2; Story 2.5 (auth restriction) cross-cuts all authoring/import/publish actions in 2.1–2.4.
