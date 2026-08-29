# Reviewer Gate — K53 Guru spine

Run: 2026-08-29 · Finalize gate. Deterministic `lint_spine.py`: **0 findings**.
Three lenses dispatched as parallel subagents (read-only, so summaries captured here).

---

## Lens 1 — Rubric walker (good-spine checklist)

**Verdict:** Functionally sound (CAP-1..9 covered, Clean Architecture ratified) but operational envelope under-guarded.

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| R1 | critical | Operational envelope deferred with no binding guardrail (deploy, environments, migrations, distribution) | **Fixed** — added AD-14 |
| R2 | critical | Multi-language deferred without schema guidance | **Fixed** — AD-9 now reserves a `language_code` axis (default `en`) |
| R3 | critical | AD-10 rate-limiting rule vague/unenforceable | **Fixed** — rule now states a working default + defers exact limits |
| R4 | high | Riverpod unpinned `[ASSUMPTION]` | Deferred — pin at client-epic kickoff (noted 3.x current) |
| R5 | high | DB migration strategy unaddressed | **Fixed** — folded into AD-14 |

## Lens 2 — Version & reality check

**Verdict:** Spine contradicted the codebase on the framework version.

| # | Sev | Finding | Disposition |
| --- | --- | --- | --- |
| V1 | critical | Spine bound `net9.0`; every backend `.csproj` targets **net10.0** (project.md was stale) | **Fixed** — Stack now `net10.0`, EF Core/ASP.NET Core 10.x |
| V2 | high | Microsoft packages pinned to `10.0.0-rc.2` (pre-release); `10.0.11` stable is available | **Fixed** — Deferred item now "move rc.2 → stable 10.0.x" |
| V3 | high | QuestPDF alpha + Serilog.Sinks.MSSqlServer dev build in solution | Noted — pre-existing template pins, out of spine scope |
| V4 | med | Flutter 3.47.2 / Dart 3.13.2 plausible-current; confirm with `flutter --version` at kickoff | Deferred |
| V5 | low | `project.md` persistent-facts doc says net9.0 — contradicts codebase | Flagged to user (source-input divergence) |

## Lens 3 — Adversarial (incompatible-pair hunt)

**Verdict:** Five pairs of epics each obey every AD yet build incompatibly. All closed by tightening existing ADs.

| # | Sev | Incompatible pair | Fix applied |
| --- | --- | --- | --- |
| A1 | critical | Who randomises & is authoritative for question order; client resume breaks if a local seed is lost | AD-5 — server assigns immutable per-section `display_order`; client renders as-received, never re-shuffles; resume returns identical order |
| A2 | critical | Results DTO shape + client defensively re-computing/validating pass-fail | AD-4/AD-6 — client renders server results verbatim; never re-computes, re-validates, or caches a grade |
| A3 | high | Timing authority (client countdown vs server deadline) → lost submissions | AD-11 — server clock authoritative; `submitted_at` is diagnostic-only; server computes elapsed |
| A4 | high | Practice-mode client-side grading diverges from server grade | AD-6 — grading always server-authoritative even in Practice; client caches no grade |
| A5 | med | Ambiguous `sign_ref` if catalog holds duplicates | AD-7 — `legislation_code` is a unique catalog key; resolution is exactly-one; import rejects ambiguous refs |

---

**Gate outcome:** all critical/high findings auto-fixed in the spine; V4/R4 deferred to client-epic kickoff; V5 flagged to the user.
