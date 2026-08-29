---
status: review
updated: 2026-08-29
reviewer: UX documentation rubric reviewer
targets:
  - ./DESIGN.md
  - ./EXPERIENCE.md
context:
  - ../../../specs/spec-k53-learners-app/SPEC.md
---

# UX Rubric Review — K53 Guru (DESIGN.md + EXPERIENCE.md)

**Overall verdict: CONCERN** — the two spines are internally clean, lean, and well-tokenized, but one capability-level SPEC contradiction (UUID restore) and two smaller drifts must be reconciled before build.

| # | Item | Verdict |
|---|------|---------|
| 1 | Coverage | PASS |
| 2 | Token integrity | PASS |
| 3 | Consistency | PASS |
| 4 | Testability | CONCERN |
| 5 | SPEC alignment | FAIL |
| 6 | Lean | PASS |

Counts: **PASS 4 · CONCERN 1 · FAIL 1**

---

## 1. COVERAGE — PASS

- **EXPERIENCE.md** carries all eight required sections: Foundation, Information Architecture, Voice and Tone, Component Patterns, State Patterns, Interaction Primitives, Accessibility Floor, Key Flows — plus value-adding invented sections (Persistence & Sync, Identity & Profile, Progression & Rewards, Admin surface).
- **DESIGN.md** carries all eight canonical sections: Brand & Style, Colors, Typography, Layout & Spacing, Elevation & Depth, Shapes, Components, Do's and Don'ts.
- Nothing required is missing from either spine.

## 2. TOKEN INTEGRITY — PASS

Every `{path.to.token}` reference in EXPERIENCE.md resolves to a token defined in DESIGN.md frontmatter:

- `{colors.light.primary}` (intro note) → `colors.light.primary` ✓
- `{components.button-primary}` (Component Patterns → Question card) → `components.button-primary` ✓
- `{components.button-disabled}` (Component Patterns → Question card) → `components.button-disabled` ✓

No dangling or misspelled token references found.

## 3. CONSISTENCY — PASS

No contradictions between the two spines or within either:

- **Feedback rules** agree — DESIGN Do's/Don'ts ("Don't make wrong answers loud/animated/haptic") matches EXPERIENCE Component Patterns/State Patterns (wrong = red highlight, no haptic, no punishing animation).
- **Mode rules** agree — timer is Test-mode-only in both DESIGN Components and EXPERIENCE Component/State Patterns; warmth explicitly drains at Test entry in both.
- **Theme rules** agree — "dark mode is a profile setting (not a system toggle), same preference drives the future web surface" in both DESIGN Colors and EXPERIENCE Identity & Profile.
- **Precedence** statements are compatible ("both spines win" / "this file and EXPERIENCE.md win").

## 4. TESTABILITY — CONCERN

Most states/flows are concrete and buildable: four option-card states with exact tokens, disabled-Confirm-until-selected, Test-only timer, per-section + per-code result screen, mastery bar = "% of that section's bank answered correctly."

Gaps that a build/test team could not resolve unaided:

- **"You'd pass" readiness meter** (Progression & Rewards; Key Flows climax "crosses into green") has **no defined threshold** — what mastery/score crosses it into green is unspecified, so it can't be asserted against.
- **In-test progress indicator** is not specified. SPEC mandates progress "by section (e.g. 'Section 2 of 4'), not a single flat question counter"; EXPERIENCE describes the Question card and Result screen but never the running section-progress element.
- **"Recalibrate vs Start fresh"** on code change (IA) names the two branches but leaves "recalibrate existing progress to the new code" undefined as a mapping rule — testable only once that transform is specified.

## 5. SPEC ALIGNMENT — FAIL

Strong alignment on most capabilities — CLLT fidelity (Test = cold, no feedback, timer, honest result), per-code combination grading (Result screen "per-code breakdown"; independent pass/fail per code), Practice/Test split (CAP-8), and a robust accessibility floor (TTS opt-in, ≥17px, ≥48px, no colour-only). But there is a capability-level contradiction:

- **UUID restore contradicts CAP-9 (FAIL driver).** SPEC CAP-9 success is explicit: "pasting that UUID into a freshly installed app restores the same profile." EXPERIENCE → Identity & Profile states "Transfer/linking is by **QR code only** (QR encodes the UUID) — **no manual paste**." Removing the paste-to-restore path negates CAP-9's stated success criterion. QR may be *added*, but manual UUID paste-in must remain for spec fidelity.
- **Test-mode back-navigation vs "section blocking" (drift).** EXPERIENCE State Patterns allows "back-navigation … (revisit/change answers, like real CLLT)." SPEC lists "section blocking" among the faithful-simulation rules. If back-nav is intra-section only this is fine; if it permits crossing completed sections it violates the fidelity bar. Needs an explicit boundary.
- **Section-progress presentation absent (drift).** SPEC constraint requiring "Section 2 of 4"-style progress is not reflected in EXPERIENCE (see item 4).
- Minor: EXPERIENCE resolves SPEC open questions (English-only v1; offline-first practice) by fiat — acceptable and flagged "(invented)", but offline test-taking scope remains a SPEC open question worth confirming.

## 6. LEAN — PASS

No decorative filler. Both spines are dense and load-bearing; frontmatter carries the tokens, prose carries only rationale prose can't encode. The Key Flows narrative (Thabo) is vivid but each beat pins a concrete behavioural contract (silent UUID, resume-after-battery-death, gentle wrong-answer, warmth-drain at Test) rather than mood-setting.

---

## Required fixes before build

1. **Restore CAP-9 paste path** — allow linking an existing profile by pasting its UUID; keep QR as an additional convenience, not the sole method.
2. **Bound Test-mode back-navigation** — state explicitly that revisit/change is intra-section (or reconcile with "section blocking").
3. **Specify in-test section-progress** ("Section X of Y") and define the "you'd pass" readiness-meter threshold and the "Recalibrate" mapping rule.
