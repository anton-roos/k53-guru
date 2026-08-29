---
stepsCompleted: ["step-01-validate-prerequisites", "step-02-design-epics", "step-03-create-stories", "step-04-final-validation"]
inputDocuments:
  - ../specs/spec-k53-learners-app/SPEC.md
  - ../specs/spec-k53-learners-app/test-structure.md
  - planning-artifacts/architecture/architecture-k53-guru-2026-08-29/ARCHITECTURE-SPINE.md
  - planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/DESIGN.md
  - planning-artifacts/ux-designs/ux-k53-guru-2026-08-29/EXPERIENCE.md
decisions:
  - "No standalone PRD exists; SPEC-k53-learners-app is the requirements source (capabilities = FRs)."
  - "Offline: online-first per finalized Architecture spine; offline deferred as a flagged story (UX offline-first noted, not built in v1)."
  - "Transfer: support BOTH QR and manual UUID paste (satisfies SPEC CAP-9 + UX)."
  - "UX review files (review-rubric.md, review-accessibility.md) treated as supporting, not primary inputs."
---

# k53-guru - Epic Breakdown

## Overview

This document provides the complete epic and story breakdown for k53-guru, decomposing the requirements from the SPEC (requirements source, in place of a PRD), the UX design contract (DESIGN.md + EXPERIENCE.md), and the Architecture Spine into implementable stories.

**Autonomous decisions applied at extraction** (user delegated; see frontmatter `decisions`):

- The **SPEC** (`spec-k53-learners-app`) is the requirements source; its capabilities `CAP-1..9` are the functional requirements.
- **Online-first** is authoritative (Architecture spine AD-4/AD-6 put composition and grading server-side); the UX "offline-first practice" guarantee is **deferred**, captured as a flagged story rather than built in v1.
- Profile transfer/restore supports **both QR and manual UUID paste**.

## Requirements Inventory

### Functional Requirements

FR1: An administrator can author multiple-choice tests through a Blazor Admin Panel that mimic the computerised K53 learner's test format; authored questions, options, and correct answers are persisted and retrievable by the API. *(CAP-1)*

FR2: An administrator can import existing tests via CSV or JSON; a well-formed file is ingested into a stored test with questions, options, and correct answers intact and delivered identically to a hand-authored test. *(CAP-2)*

FR3: The system ships with a road-sign catalog pre-loaded from the official legislation (SARTSM), each sign carrying its legislative code, and any sign can be referenced by a test question via its `sign_ref`. *(CAP-3)*

FR4: The .NET API serves tests to the client and randomises question selection and ordering per attempt (intra-section only), so two attempts of the same underlying test produce differently ordered and/or selected question sets. *(CAP-4)*

FR5: A learner can take a sitting scoped to a single code (Code 1/2/3) or a valid combination (Code 1 + Code 2, or Code 1 + Code 3); a combination shares the Rules and Signs sections once and adds a separate Vehicle-Controls module per selected code. *(CAP-5)*

FR6: The test-taking flow simulates the real CLLT in structure, scoring, and per-code outcome; a completed attempt returns per-section scores and, for a combination, an independent pass/fail per code (partial passes possible) mirroring official CLLT scoring rules. *(CAP-6)*

FR7: The import path validates every row against the road-sign catalog and the question schema, refusing malformed content — a row with an unresolved `sign_ref` or missing required field is rejected with the offending row identified, and no broken question is ever served. *(CAP-7)*

FR8: A learner can choose Practice mode (reveals correctness + explanation as they go, permits retry, untimed) or Test mode (withholds all feedback, times the sitting, returns only a final per-section scored result) for the same underlying content. *(CAP-8)*

FR9: A learner establishes and restores identity via a self-custodied UUID — on launch generate a new profile or link an existing one (by QR scan or manual paste) — with no account, password, or PII; pasting/scanning that UUID into a fresh install restores the same profile and its data. *(CAP-9)*

### NonFunctional Requirements

NFR1: **Fidelity** to the official computerised K53 learner's test — format, question mix, scoring, and pass/fail rules — is the primary quality bar every design decision resolves against. *(SPEC Constraints)*

NFR2: Road signs are stored with their official legislative codes, not ad-hoc labels; questions and study material reference the same authoritative source. *(SPEC Constraints)*

NFR3: Randomisation is **intra-section only**: order is shuffled within each section while section order stays fixed (Rules of the Road → Road Signs → Vehicle Controls, with code-specific blocks kept in order). No attempt serves a fixed, memorisable sequence. *(SPEC Constraints)*

NFR4: The test-taking UI presents progress **by section** ("Section 2 of 4"), not a single flat question counter, so long combined papers stay legible. *(SPEC Constraints)*

NFR5: An attempt's question set is **snapshotted at start**; administrator content edits apply only to subsequent attempts and never mutate a live, in-progress attempt. *(SPEC Constraints; spine AD-5)*

NFR6: Per-code question counts, pass marks, and time limits are **configurable data, not hardcoded logic**; provisional defaults live in `test-structure.md` and must be confirmed against a live DLTC/CLLT terminal before production. *(SPEC Constraints; spine AD-11)*

NFR7: Learner identity is anonymous and self-custodied: **no authentication, no accounts, no PII**. The UUID is the sole key; losing it loses access. *(SPEC Constraints; spine AD-10)*

NFR8: Content covers all three codes; supported sittings are a single code or a valid combination (Code 1+2 or Code 1+3). Code 2+3 and all-three sittings are not offered. *(SPEC Constraints)*

NFR9: The learner API is a public internet surface — HTTPS only, `ProblemDetails` errors, and per-UUID rate limiting to deter enumeration. *(spine AD-10, AD-14)*

NFR10: API responses use explicit **versioned DTOs** (`/api/v1`); EF entities and domain events are never serialized to the client; a breaking change bumps the version segment. *(spine AD-12)*

NFR11: In Test mode the API delivers questions **without correct keys or explanations**; grading is server-side and returns only per-section/per-code results (answer confidentiality). *(spine AD-6)*

### Additional Requirements

*(From the Architecture Spine — technical constraints that shape epics/stories.)*

- **Brownfield backend**: build on the existing **.NET 10 Clean Architecture + CQRS (MediatR)** solution (`src/K53Guru`). No new backend starter template. Ratify existing conventions (per-op `IApplicationDbContextFactory`, Ardalis.Specification, FusionCache pipeline, FluentValidation, MudBlazor Admin). *(spine AD-1)*
- **New API project**: expose a learner-facing content API as ASP.NET Core controllers under `/api/v1`, delegating to the **same** Application-layer MediatR handlers as the Admin Panel — zero business logic in controllers. *(spine AD-3)*
- **New Flutter client** (greenfield): layered `presentation → state (Riverpod 3.x) → data (repository + typed API client + DTO models) → domain`. Widgets never call HTTP directly. *(spine AD-2, Flutter paradigm)*
- **Server-side fidelity authority**: composition (randomisation, section assembly, combination handling) and scoring (per-section cut-offs, per-code independent grading, partial pass) computed in Domain/Application. Client never grades, re-computes, re-validates, or caches a grade. *(spine AD-4)*
- **Immutable attempt snapshot**: an attempt freezes its full question set + a per-section `display_order` at start; resume returns the identical order; edits never touch a live attempt. *(spine AD-5)*
- **Sign catalog as prerequisite seed data**: `legislation_code` is a unique key; `sign_ref` resolves exactly-once (`SingleOrDefault`); ambiguous/unresolved refs rejected at author/import time. **This is the foundational first epic.** *(spine AD-7)*
- **Import validation boundary**: schema + catalog validation, all-or-nothing per row, offending row identified. *(spine AD-8)*
- **Two-axis content model**: each question carries applicable code(s) `{Code1,Code2,Code3}` and a `shared` (Rules, Signs) vs `code-specific` (Vehicle Controls) class, plus a `language_code` (default `en`; v1 English only). *(spine AD-9)*
- **Two authorization domains**: Admin Panel author/import/publish uses existing ASP.NET Identity + roles; the learner API serves only **published** content anonymously and accepts UUID-keyed submissions. *(spine AD-13)*
- **Server-authoritative timing**: elapsed time computed server-side from `attempt.started_at`; a client `submitted_at` is diagnostic only. *(spine AD-11)*
- **Operational envelope**: containerized via existing `docker-compose`; **PostgreSQL** production DB; schema changes ship as version-controlled EF Core migrations through the `Migrators.PostgreSQL` project (no `EnsureCreated` in prod); env config via `IOptions<T>`; sign assets served from MinIO; Flutter distributed via app stores. *(spine AD-14)*
- **Deferred (flagged, not built in v1)**: package upgrade `10.0.0-rc.2` → stable `10.0.x`; offline test-taking; multi-language content; exact admin role split; confirmation of official CLLT counts/time limits. *(spine Deferred)*

### UX Design Requirements

*(From DESIGN.md + EXPERIENCE.md — first-class inputs. Each is specific enough to seed a story.)*

UX-DR1: **Design token system** — implement the light + dark palettes, typography scale (min body 17px; question stem 20px/700), 4px spacing scale, radii, and component tokens as a shared Flutter theme; Admin inherits equivalent tokens via MudBlazor. *(DESIGN frontmatter)*

UX-DR2: **Bottom-nav IA** — three learner destinations (Practice / Test / Profile). Practice home shows four persistent tiles (Randomized Training, Rules of the Road, Road Signs, Vehicle Controls), each with a green mastery bar and independent resume. *(EXPERIENCE IA)*

UX-DR3: **Code selection & change flow** — code chosen once at profile creation, silently filters all content; changing code prompts a confirmation offering **Recalibrate** (map progress) vs **Start fresh** (reset). *(EXPERIENCE IA)*

UX-DR4: **Question card component** — stem + options; tap to *select* (not submit); bottom-anchored **Confirm** disabled until a selection is made; single-column vertical scroll; no gesture-only paths. *(EXPERIENCE Component Patterns / Interaction Primitives)*

UX-DR5: **Practice answer feedback** — correct → green pulse + celebratory Lottie + haptic + tone rising a semitone per streak increment; wrong → gentle red on chosen option, green on correct, no haptic, no punishing animation, plus explanation. Correct/wrong always carry a check/cross icon + text label, never colour alone. *(EXPERIENCE Component Patterns)*

UX-DR6: **Streak / XP system** — streak chip drives the rising tone; XP accrues per correct answer, more per Test-mode answer than Practice. *(EXPERIENCE Component Patterns)*

UX-DR7: **Test-mode "cold" experience** — no feedback of any kind during the sitting, visible countdown timer pill (Test only), section progress indicator ("Section 2 of 4"); warmth visibly drains at "Begin Test". *(EXPERIENCE State Patterns)*

UX-DR8: **Result screen** — per-section pass/fail, overall pass/fail, and per-code breakdown for combinations. *(EXPERIENCE Component Patterns)*

UX-DR9: **Resume & continuous-session persistence** — every practice state and current position persist; reopening restores the exact question after minimise, lock, or battery death. *(EXPERIENCE State/Persistence — online-first scope; cross-device cloud sync keyed to UUID)*

UX-DR10: **Identity & profile UX** — first run generates a UUID silently; Profile shows the UUID (copyable) with the save-your-progress note; transfer/link via **QR code and manual paste**. *(EXPERIENCE Identity; reconciled with SPEC CAP-9)*

UX-DR11: **Settings** — theme (light default; **dark mode is a profile setting**, not a system toggle) and **TTS opt-in**. *(EXPERIENCE Identity/Settings)*

UX-DR12: **Progression screen** — per-section mastery bars, % of the bank mastered, a "you'd pass" readiness meter, and streaks / best runs. *(EXPERIENCE Progression)*

UX-DR13: **Accessibility floor** — body text never below 17px; tap targets ≥ 48px; Flutter `Semantics` labels on every option + live-region announcements (screen-reader support, *separate* from TTS); dynamic type to 200% (`MediaQuery.textScaler`) with reflow; respect `prefers-reduced-motion` (static success cue); AA contrast (dark-mode primary button uses dark ink); no colour-only cues; no gesture-only paths. *(EXPERIENCE Accessibility Floor + accessibility review)*

UX-DR14: **TTS reader** — opt-in (Settings) reader that reads questions and options aloud for reading difficulties. *(EXPERIENCE Accessibility)*

UX-DR15: **Admin panel surface** — Blazor desktop-first, utilitarian, MudBlazor tokens; author questions, bulk-import CSV/JSON with reject-on-error validation, manage the sign catalog, publish/unpublish. *(EXPERIENCE Admin surface)*

### FR Coverage Map

- **FR1** (author tests): Epic 2 — Admin Content Management
- **FR2** (CSV/JSON import): Epic 2 — Admin Content Management
- **FR3** (road-sign catalog): Epic 1 — Road-Sign Catalog & Content Model
- **FR4** (API serves + randomises): Epic 3 — Exam Engine API
- **FR5** (single/combination sittings): Epic 3 — Exam Engine API
- **FR6** (faithful scoring & per-code outcome): Epic 3 — Exam Engine API (engine) + Epic 6 — Test-Mode Simulation (result presentation)
- **FR7** (import validation): Epic 2 — Admin Content Management
- **FR8** (Practice vs Test mode): Epic 3 — Exam Engine API (backend modes) + Epic 5 — Practice Experience + Epic 6 — Test-Mode Simulation
- **FR9** (anonymous UUID identity): Epic 4 — Learner App Foundation & Identity

Every FR maps to at least one epic; no FR is unassigned.

## Epic List

### Epic 1: Road-Sign Catalog & Content Model

Establish the authoritative, seeded road-sign catalog (official legislative codes, unique `legislation_code` key, exact-one `sign_ref` resolution) and the two-axis question/answer content model that every test question references — and let an administrator browse and manage the catalog. This is the foundational prerequisite the architecture spine calls out: all downstream content and delivery depend on it.
**FRs covered:** FR3 (+ foundation for FR1, FR2, FR7)
**Supports:** NFR2 · AD-7, AD-9

### Epic 2: Admin Content Management

A K53 domain expert, from the Blazor Admin Panel, can author multiple-choice questions and tests that reference catalog signs, bulk-import a question bank via CSV/JSON with reject-on-error validation (schema + catalog, all-or-nothing per row, offending row identified), and publish/unpublish — producing the published content the learner API serves. Uses the existing ASP.NET Identity admin auth.
**FRs covered:** FR1, FR2, FR7
**Supports:** NFR2 · AD-1, AD-3, AD-8, AD-13

### Epic 3: Exam Engine API

The .NET content API composes randomised, correctly-structured sittings (single code and valid combinations — Rules/Signs shared once, a Vehicle-Controls module per code), snapshots each attempt immutably at start, and grades server-side with per-section cut-offs and independent per-code pass/fail (partial passes) — all exposed over a versioned `/api/v1` surface with Practice and Test modes and answer confidentiality. This is the fidelity heart of the system.
**FRs covered:** FR4, FR5, FR6 (engine), FR8 (backend modes)
**Supports:** NFR1, NFR3, NFR4, NFR5, NFR6, NFR9, NFR10, NFR11 · AD-3, AD-4, AD-5, AD-6, AD-11, AD-12, AD-14

### Epic 4: Learner App Foundation & Identity

A learner opens the Flutter app, receives a silently-generated anonymous UUID profile (restorable by QR scan or manual paste, no PII), and moves through a themed, accessible three-tab shell (Practice / Test / Profile) — picking their licence code once at creation (with a Recalibrate-vs-Start-fresh change flow) and controlling settings (theme, TTS opt-in), all backed by a typed API repository. Establishes the design-token theme and the accessibility floor the later screens inherit.
**FRs covered:** FR9
**Supports:** AD-2, AD-10 · UX-DR1, UX-DR2, UX-DR3, UX-DR10, UX-DR11, UX-DR13, UX-DR14

### Epic 5: Practice Experience

A learner practises per section from four home tiles (Randomized Training, Rules, Signs, Vehicle Controls), answering tap-to-select → Confirm question cards with warm, non-punishing feedback (green pulse + Lottie + haptic + rising tone on correct; gentle red + explanation on wrong, always icon+label), earning streaks and XP, resuming exactly where they left off, and tracking readiness on a progression screen. The warm, encouraging half of the app.
**FRs covered:** FR8 (Practice)
**Supports:** NFR7 · UX-DR4, UX-DR5, UX-DR6, UX-DR9, UX-DR12

### Epic 6: Test-Mode Simulation

A learner runs a faithful cold CLLT simulation: warmth visibly drains at “Begin Test”, a countdown timer and “Section 2 of 4” progress replace all feedback, nothing is revealed during the sitting, and the end delivers an honest result screen — per-section pass/fail, overall pass/fail, and an independent per-code breakdown for combinations. The rehearsal that mirrors the real testing centre.
**FRs covered:** FR6 (result presentation), FR8 (Test)
**Supports:** NFR1, NFR3, NFR4, NFR11 · UX-DR7, UX-DR8

---

**Dependency flow (forward-only):** Epic 1 → Epic 2 → Epic 3 provide the content and API; Epic 4 stands up the client against that API; Epics 5 and 6 build the two learner experiences on the Epic 4 foundation. Each epic delivers complete functionality for its domain and does not require a later epic to function.

## Epic 1: Road-Sign Catalog & Content Model

Establish the authoritative, seeded road-sign catalog and the two-axis question/answer content model that all test content references — the foundational prerequisite for every downstream epic.

### Story 1.1: Seed the road-sign catalog with legislative codes

As a platform maintainer,
I want the road-sign catalog pre-loaded from the official legislation with each sign carrying its legislative code,
So that every question can reference an authoritative sign by a stable code.

**Acceptance Criteria:**

**Given** a fresh database initialised by the Infrastructure seeder
**When** the application starts for the first time
**Then** a `RoadSign` entity exists for every official sign in the source data, each with its `legislation_code`, description, and image asset key
**And** `legislation_code` is enforced as a unique key so each code appears exactly once.

**Given** the seed source contains a duplicate `legislation_code`
**When** seeding runs
**Then** seeding fails fast with a clear error rather than loading an ambiguous catalog.

**Given** the catalog is already seeded
**When** the application restarts
**Then** seeding is idempotent and does not create duplicate signs.

### Story 1.2: Browse and search the sign catalog in the Admin Panel

As a K53 domain expert,
I want to browse and search the road-sign catalog in the Admin Panel,
So that I can find the correct legislative code when authoring a question.

**Acceptance Criteria:**

**Given** I am on the sign-catalog page in the Blazor Admin Panel
**When** the page loads
**Then** I see a paged, searchable list of signs showing each sign's image, `legislation_code`, and description.

**Given** I type a code or keyword into the search box
**When** the results update
**Then** only matching signs are shown, resolved against `legislation_code` and description.

**Given** I open a single sign
**When** its detail is displayed
**Then** I see its full `legislation_code`, description, and image.

### Story 1.3: Define the two-axis question content model

As a platform maintainer,
I want a question/answer content model tagged by licence code(s), shared-vs-code-specific class, and language,
So that test composition can select the right questions for any sitting.

**Acceptance Criteria:**

**Given** the content model
**When** a `Question` is created
**Then** it carries one or more applicable codes from `{Code1, Code2, Code3}`, a class of `shared` (Rules, Signs) or `code-specific` (Vehicle Controls), a `language_code` defaulting to `en`, a stem, and an ordered set of `AnswerOption`s with exactly one marked correct.

**Given** a `Question` references a road sign
**When** it is saved
**Then** the `sign_ref` must resolve to exactly one catalog sign (`SingleOrDefault`); an unresolved or ambiguous `sign_ref` is rejected with a clear error.

**Given** the model is persisted
**When** the migration is applied to PostgreSQL via the `Migrators.PostgreSQL` project
**Then** the schema is created without `EnsureCreated` and the entities round-trip correctly.

## Epic 2: Admin Content Management

A K53 domain expert can author, import, and publish the test content the learner API serves, from the Blazor Admin Panel, behind the existing admin authentication.

### Story 2.1: Author and edit a question

As a K53 domain expert,
I want to author and edit multiple-choice questions that reference catalog signs,
So that I can build faithful K53 test content by hand.

**Acceptance Criteria:**

**Given** I am authoring a new question in the Admin Panel
**When** I enter a stem, options, mark the correct option, set the applicable code(s), section class, and (optionally) a `sign_ref`
**Then** the question is validated (required fields present, exactly one correct option, `sign_ref` resolves to one sign) and saved.

**Given** a validation rule fails
**When** I try to save
**Then** the save is rejected and the failing field is identified, with nothing partially persisted.

**Given** an existing question
**When** I edit and save it
**Then** the changes persist and apply only to future attempts (never to a live attempt).

### Story 2.2: Organise questions into a test

As a K53 domain expert,
I want to group questions into a named test scoped to code(s) and sections,
So that the API has a coherent content set to compose sittings from.

**Acceptance Criteria:**

**Given** authored questions exist
**When** I create a test and assign its code scope and sections
**Then** the test is persisted with its associated questions and is retrievable in the Admin Panel.

**Given** a test
**When** I view it
**Then** I see its questions grouped by section (Rules → Signs → Controls) with counts per section and code.

### Story 2.3: Publish and unpublish a test

As a K53 domain expert,
I want to publish or unpublish a test,
So that only content I have approved is ever served to learners.

**Acceptance Criteria:**

**Given** a complete test in draft
**When** I publish it
**Then** its status becomes `published` and it becomes eligible for API delivery.

**Given** a published test
**When** I unpublish it
**Then** it is no longer served to new attempts, while in-progress attempts (which are snapshotted) are unaffected.

**Given** an unpublished/draft test
**When** the learner API is queried
**Then** the draft is never returned.

### Story 2.4: Import a question bank via CSV/JSON with reject-on-error validation

As a K53 domain expert,
I want to bulk-import questions from a CSV or JSON file with strict validation,
So that I can stand up a bank quickly without hand-entering every question and without ever storing broken content.

**Acceptance Criteria:**

**Given** a well-formed CSV or JSON file matching the published template
**When** I import it
**Then** each row is ingested into stored questions with options and correct answers intact, delivered identically to hand-authored questions.

**Given** a file containing a row with a missing required field or an unresolved/ambiguous `sign_ref`
**When** I import it
**Then** the import is rejected, the offending row is identified by position and reason, and no partial content is stored (all-or-nothing per the documented import unit).

**Given** I need the correct format
**When** I request a template
**Then** a downloadable CSV/JSON template is provided.

### Story 2.5: Restrict content management to authenticated admins

As a platform owner,
I want authoring, import, and publish actions restricted to an authenticated admin role,
So that only authorised staff can change the content learners receive.

**Acceptance Criteria:**

**Given** the existing ASP.NET Identity admin auth
**When** an unauthenticated or unauthorised user attempts an authoring/import/publish action
**Then** the action is denied.

**Given** an authenticated admin
**When** they perform content-management actions
**Then** the actions succeed (working default: "authenticated admin may author/publish/import"; finer role split deferred).

## Epic 3: Exam Engine API

The .NET content API composes randomised, correctly-structured sittings, snapshots attempts immutably, and grades server-side with faithful CLLT rules — the fidelity heart of the system, exposed over a versioned `/api/v1` surface.

### Story 3.1: Discover available sittings via the versioned API

As a learner app,
I want to fetch the available codes and published tests over a versioned API,
So that a learner can choose a valid single-code or combination sitting.

**Acceptance Criteria:**

**Given** published content exists
**When** the client calls `GET /api/v1/…` for available sittings
**Then** it receives only published, servable options (single codes and valid combinations Code 1+2, Code 1+3) as versioned DTOs — never EF entities or draft content.

**Given** the API surface
**When** any response is returned
**Then** it is served over HTTPS, uses `ProblemDetails` for errors, and delegates to the shared Application MediatR handlers (no business logic in controllers).

**Given** anonymous access
**When** the endpoint is called without credentials
**Then** published read access is permitted and the endpoint is rate-limited to deter abuse.

### Story 3.2: Configure per-code test parameters as data

As a platform maintainer,
I want per-code question counts, pass marks, time limits, and section definitions stored as configuration data seeded from `test-structure.md`,
So that fidelity rules can be tuned without code changes and confirmed against a live terminal later.

**Acceptance Criteria:**

**Given** the configuration entities
**When** the system seeds them
**Then** each code has its section definitions, question counts, pass marks, and time limit as data (no thresholds hardcoded in logic).

**Given** a value must change
**When** an admin/maintainer updates the configuration
**Then** composition and scoring read the new value on the next attempt without a code change.

### Story 3.3: Start a single-code attempt with randomised, snapshotted questions

As a learner app,
I want to start an attempt that composes a randomised question set and freezes it,
So that each sitting is unique and cannot change under the learner.

**Acceptance Criteria:**

**Given** a published single-code test and its configuration
**When** the client starts an attempt
**Then** the server selects questions per section per the configured counts and assigns an immutable per-section `display_order`, shuffled intra-section while section order stays fixed (Rules → Signs → Controls).

**Given** two attempts of the same test
**When** they are started
**Then** they produce differently ordered and/or selected question sets (no fixed, memorisable sequence).

**Given** an in-progress attempt
**When** the underlying content is later edited
**Then** the attempt's snapshot is unchanged, and a resume returns the identical `display_order`.

### Story 3.4: Compose valid combination sittings

As a learner app,
I want a combination sitting (Code 1+2 or Code 1+3) composed as one booking,
So that shared knowledge is answered once and each code gets its own controls module.

**Acceptance Criteria:**

**Given** a learner selects Code 1+2 or Code 1+3
**When** the attempt is composed
**Then** the Rules and Signs sections appear once and a separate Vehicle-Controls module is included per selected code, in fixed order.

**Given** an unsupported combination (Code 2+3 or all-three)
**When** it is requested
**Then** the API rejects it as an invalid sitting.

### Story 3.5: Submit and grade an attempt server-side

As a learner app,
I want the server to grade a submitted attempt with faithful per-section and per-code rules,
So that the outcome mirrors the real CLLT including partial passes.

**Acceptance Criteria:**

**Given** a completed attempt's answers are submitted
**When** the server grades it
**Then** it computes per-section scores against the configured cut-offs; failing any one section fails that code.

**Given** a combination sitting
**When** it is graded
**Then** each code is graded independently, a partial pass is possible (pass one code, fail the other), and the result reports pass/fail per code plus overall.

**Given** grading occurs
**When** the result is returned
**Then** it is computed entirely server-side and returned as a versioned DTO; the client never grades, re-computes, or caches a grade.

### Story 3.6: Deliver Practice and Test modes with answer confidentiality and server timing

As a learner app,
I want distinct Practice and Test delivery for the same content,
So that Practice can teach while Test stays a faithful cold simulation.

**Acceptance Criteria:**

**Given** Practice mode
**When** the client fetches questions and submits an answer
**Then** the server returns correctness and explanation (server-owned), permits retry, and applies no time limit.

**Given** Test mode
**When** the client fetches questions
**Then** correct keys and explanations are withheld; only a final per-section/per-code result is returned at submission.

**Given** a timed Test sitting
**When** a submission arrives
**Then** elapsed time is computed server-side from `attempt.started_at`; a late submission is rejected; any client `submitted_at` is used for diagnostics only.

## Epic 4: Learner App Foundation & Identity

A learner opens the Flutter app, gets a silent anonymous UUID profile, and moves through a themed, accessible three-tab shell — the foundation the two experience epics build on.

### Story 4.1: Scaffold the Flutter app with the design-token theme and API repository

As a learner,
I want a consistent, legible app theme,
So that the app feels calm, trustworthy, and readable on any phone.

**Acceptance Criteria:**

**Given** the Flutter app scaffold
**When** it builds
**Then** it implements the layered structure (presentation → state (Riverpod) → data (repository + typed API client + DTO models) → domain), and widgets never call HTTP directly.

**Given** the design tokens from DESIGN.md
**When** the theme is applied
**Then** light and dark palettes, the typography scale (body ≥ 17px, question stem 20px/700), 4px spacing scale, radii, and component tokens are available as a shared theme.

**Given** the API contract
**When** the repository calls the backend
**Then** it targets the versioned `/api/v1` DTOs and surfaces typed models to the app.

### Story 4.2: Provide the three-tab bottom-nav shell

As a learner,
I want a simple three-destination app,
So that I always know where Practice, Test, and Profile live.

**Acceptance Criteria:**

**Given** the app is open
**When** it renders
**Then** a bottom navigation exposes exactly three destinations — Practice (home), Test, and Profile — portrait-only, single-column.

**Given** I switch tabs
**When** navigation occurs
**Then** each destination preserves its own state and tap targets are ≥ 48px.

### Story 4.3: Generate an anonymous UUID profile silently on first run

As a first-time learner,
I want a profile created for me without any signup,
So that I can start immediately with no friction and no personal data.

**Acceptance Criteria:**

**Given** a fresh install
**When** I tap `Start learning`
**Then** a UUID is generated silently client-side and an anonymous profile is created via the profile API keyed solely by that UUID — no account, password, or PII.

**Given** the profile exists
**When** any API call is made
**Then** the UUID is sent as the opaque profile key and the endpoint is rate-limited.

### Story 4.4: Restore or transfer a profile by QR or manual paste

As a returning learner,
I want to restore my profile by scanning a QR code or pasting my UUID,
So that I can continue on a fresh install or a different device.

**Acceptance Criteria:**

**Given** my Profile screen
**When** I view it
**Then** my UUID is shown and copyable, with the save-your-progress note, and a QR code encoding the UUID.

**Given** a fresh install
**When** I scan my QR code or paste my UUID
**Then** the same profile and its data are restored.

**Given** an invalid or unknown UUID
**When** I attempt to restore
**Then** I get a clear, non-technical error and no profile is corrupted.

### Story 4.5: Choose licence code once, with a change-code flow

As a learner,
I want to pick my licence code once and change it deliberately,
So that content is silently filtered to me without category-picking mid-practice.

**Acceptance Criteria:**

**Given** first-run profile creation
**When** I choose Code 1, 2, or 3
**Then** all content is silently filtered to that code and I am never asked to pick a category again.

**Given** I change my code in Profile
**When** I confirm
**Then** I am offered **Recalibrate** (map existing progress to the new code) or **Start fresh** (reset progress), and my choice is applied.

### Story 4.6: Provide theme and TTS settings

As a learner,
I want to control theme and text-to-speech,
So that the app fits my visual and reading needs.

**Acceptance Criteria:**

**Given** Settings
**When** I open it
**Then** I can set theme (light default; dark mode as a profile setting, not a system toggle) and toggle TTS opt-in.

**Given** I change a setting
**When** it is saved
**Then** it persists to my profile and takes effect immediately.

### Story 4.7: Meet the accessibility floor

As a learner with access needs,
I want the app to be fully accessible,
So that I can use it regardless of vision, reading, or motor differences.

**Acceptance Criteria:**

**Given** any screen
**When** it renders
**Then** body text is never below 17px, tap targets are ≥ 48px, and no interaction depends on colour alone or a gesture-only path.

**Given** a screen reader is active
**When** I navigate
**Then** every option has a Flutter `Semantics` label, selection/correctness/results are announced via live regions (separate from the opt-in TTS reader), and focus order is sane.

**Given** OS accessibility settings
**When** dynamic type is scaled up to 200% or reduced-motion is enabled
**Then** cards reflow/grow without clipping and celebratory motion is replaced by a static success cue; AA contrast holds in both themes (dark-mode primary button uses dark ink).

**Given** TTS is opted in
**When** a question is shown
**Then** an opt-in reader can read the question and its options aloud.

## Epic 5: Practice Experience

The warm, encouraging half of the app: per-section practice with non-punishing feedback, streaks, exact resume, and a readiness screen.

### Story 5.1: Practice home with four section tiles and mastery bars

As a learner,
I want four persistent practice tiles each showing my mastery,
So that I can pick where to practise and see my progress at a glance.

**Acceptance Criteria:**

**Given** the Practice home
**When** it renders
**Then** it shows four tiles — Randomized Training, Rules of the Road, Road Signs, Vehicle Controls — each with a green mastery bar (% of that section's bank answered correctly).

**Given** I tap a tile
**When** it opens
**Then** it resumes exactly where I last left off in that section, independently of the others.

### Story 5.2: Tap-to-select → Confirm question card

As a learner,
I want to select an answer and confirm it deliberately,
So that I can think without fear of accidental submits.

**Acceptance Criteria:**

**Given** a question card
**When** it renders
**Then** it shows the stem and options in a single column, with a bottom-anchored **Confirm** button disabled until an option is selected.

**Given** I tap an option
**When** it becomes selected
**Then** it shows the selected (indigo) state and Confirm becomes enabled; no gesture is required to progress.

### Story 5.3: Warm Practice answer feedback

As a learner,
I want encouraging, non-punishing feedback in Practice,
So that I learn from mistakes without anxiety.

**Acceptance Criteria:**

**Given** I confirm an answer in Practice
**When** the server returns correctness (server-owned)
**Then** a correct answer shows a green pulse + celebratory Lottie + haptic + a tone rising a semitone per streak increment.

**Given** a wrong answer
**When** feedback shows
**Then** the chosen option highlights gentle red, the correct option shows green, there is no haptic and no punishing animation, and the explanation is shown.

**Given** any feedback
**When** it renders
**Then** correct/wrong always carry a check/cross icon and text label, never colour alone.

### Story 5.4: Streak and XP rewards

As a learner,
I want streaks and XP,
So that consistent practice feels rewarding.

**Acceptance Criteria:**

**Given** consecutive correct answers
**When** my streak increments
**Then** a streak chip updates and drives the rising feedback tone.

**Given** a correct answer
**When** XP accrues
**Then** XP increases per correct answer, with more awarded per Test-mode answer than per Practice-mode answer.

### Story 5.5: Continuous-session persistence and exact resume

As a learner,
I want my practice position and progress to always survive,
So that I can do a few questions anytime and never lose my place.

**Acceptance Criteria:**

**Given** an in-progress practice session
**When** I minimise the app, lock the phone, or the battery dies
**Then** reopening restores the exact question and progress.

**Given** progress keyed to my UUID profile
**When** I sign in on a different device (by QR/paste)
**Then** my position and progress are restored via cloud sync (online-first; small-diff sync).

**Given** connectivity is temporarily unavailable
**When** I reopen
**Then** the app restores the last synced state and resumes syncing when connectivity returns (full offline-first authoring of new progress is out of v1 scope — see Story 5.7).

### Story 5.6: Progression and readiness screen

As a learner,
I want a progress screen showing mastery and readiness,
So that I can see when I'm likely to pass.

**Acceptance Criteria:**

**Given** the Profile/progress screen
**When** it renders
**Then** it shows per-section mastery bars, % of the bank mastered, a "you'd pass" readiness meter, and streaks / best runs.

**Given** my mastery crosses the readiness threshold
**When** the meter updates
**Then** the readiness meter enters its green "you'd pass" state (threshold rule to be finalised — see Open Questions).

### Story 5.7: Offline-first practice (DEFERRED — v1 out of scope)

As a data-conscious learner,
I want practice to work fully offline and sync later,
So that I can practise with no connection at all.

> **Status: DEFERRED.** The Architecture spine chose online-first (grading is server-authoritative, AD-4/AD-6). This story captures the UX "offline-first" guarantee as a tracked future item; it is **not** built in v1 and must not block Epic 5. Revisiting it requires an architecture change (local grading/answer store + sync/conflict model).

**Acceptance Criteria (future):**

**Given** no connectivity
**When** a learner practises
**Then** questions are served from a local cache and answers are graded/queued locally, syncing on reconnect — pending an architecture decision that reconciles this with server-authoritative grading.

## Epic 6: Test-Mode Simulation

The cold rehearsal that mirrors the real testing centre: no feedback, a countdown, section progress, and an honest final result.

### Story 6.1: Begin Test transition drains the warmth

As a learner,
I want the app to visibly shift into exam mode,
So that I feel the seriousness of the simulation.

**Acceptance Criteria:**

**Given** I choose a single code or valid combination and tap "Begin Test"
**When** the sitting starts
**Then** the warm styling (colour, celebration, sound) visibly drains to the cold Test theme, and no practice feedback affordances are present.

### Story 6.2: Cold test-taking flow with timer and section progress

As a learner,
I want a faithful cold sitting,
So that my rehearsal matches the real CLLT.

**Acceptance Criteria:**

**Given** a Test sitting is in progress
**When** I answer questions
**Then** no correctness feedback of any kind is shown during the sitting, a countdown timer pill is visible, and a section progress indicator shows "Section N of M" (not a flat counter).

**Given** the question card
**When** it renders in Test mode
**Then** it reuses the tap-to-select → Confirm card without any Practice feedback, and I can page back to change answers within the defined navigation scope (whole-paper vs current-section — to be confirmed against real CLLT behaviour; see Open Questions).

### Story 6.3: Submit the sitting with server-authoritative timing

As a learner,
I want the sitting to end fairly when I submit or time out,
So that the result reflects the real exam's timing rules.

**Acceptance Criteria:**

**Given** I finish before time
**When** I submit
**Then** the attempt is graded server-side and I proceed to the result.

**Given** the countdown reaches zero
**When** the client auto-submits
**Then** the server enforces the deadline from `attempt.started_at`; if the elapsed server time exceeds the limit the sitting is closed as expired, and the client informs me plainly rather than retrying.

### Story 6.4: Honest result screen with per-code breakdown

As a learner,
I want a complete, honest result,
So that I know exactly where I stand per section and per code.

**Acceptance Criteria:**

**Given** a graded sitting
**When** the result screen renders
**Then** it shows per-section pass/fail, overall pass/fail, and — for a combination — an independent per-code breakdown reflecting partial passes.

**Given** the result
**When** it is displayed
**Then** the values are the server-computed result shown verbatim (no client re-computation), with clear, non-punishing language.

## Open Questions & Deferred Items

Tracked so downstream story work doesn't lose them:

**Open questions (need a decision before the dependent story ships):**

- **Test-mode back-navigation scope** (Story 6.2) — can a learner revisit/change answers across the *whole paper* or only *within the current section*? Confirm against real CLLT behaviour; it affects the section-progress model and fidelity.
- **Readiness "you'd pass" threshold** (Story 5.6) — the mastery level that turns the readiness meter green is undefined; needs a rule.
- **Recalibrate mapping** (Story 4.5) — how existing per-section progress maps onto a new code on "Recalibrate".
- **Official CLLT figures** (Story 3.2) — exact per-code question counts, pass marks, and time limits are provisional (`test-structure.md`) pending confirmation against a live DLTC/CLLT terminal. AD-11 keeps them configurable so they can land late.
- **Admin role split** (Story 2.5) — finer author-vs-publish-vs-import roles beyond the "authenticated admin" working default.
- **Content copyright** — legality of reproducing official K53 question-bank content; working assumption is original expert-authored content (gates content population, not a story blocker).

**Deferred (explicitly out of v1 scope):**

- **Offline-first practice** (Story 5.7) — conflicts with the online-first, server-authoritative-grading architecture (AD-4/AD-6); revisiting requires an architecture change.
- **Multi-language content** — v1 is English-only; the content model reserves a `language_code` axis (Story 1.3).
- **Package upgrade** — backend `10.0.0-rc.2` → stable `10.0.x`; self-contained, do before production.
- **Companion web surface** — scanning the profile QR on a website to continue on the web (UX "future").
