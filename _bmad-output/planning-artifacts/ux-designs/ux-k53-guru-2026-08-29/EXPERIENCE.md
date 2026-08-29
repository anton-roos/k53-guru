---
status: final
updated: 2026-08-29
design_ref: ./DESIGN.md
sources:
  - ../../../specs/spec-k53-learners-app/SPEC.md
  - ../../../specs/spec-k53-learners-app/test-structure.md
---

# K53 Guru — EXPERIENCE.md

> Behaviour, IA, states, and flows contract. Visual specs live in `DESIGN.md`; this file references its tokens as `{colors.light.primary}` etc. On conflict with any mock or import, both spines win.

## Foundation

Two surfaces:

- **Learner app** — mobile, **Flutter**, **portrait-only**. The emotional core. Consumer-grade, accessibility-critical.
- **Admin panel** — **Blazor** web, desktop-first, utilitarian. Inherits DESIGN.md tokens via a component library (e.g. MudBlazor); function over polish.

North star for the learner app: **effortless and seamless — usable by a 7-year-old.** Everything below serves that. **Language: English only (v1).**

## Information Architecture

Learner app, three primary destinations (bottom nav):

1. **Practice** (home) — four persistent tiles: **Randomized Training**, **Rules of the Road**, **Road Signs**, **Vehicle Controls**. Each shows its mastery bar and resumes independently.
2. **Test** — entry to a faithful CLLT simulation (single code or valid combination).
3. **Profile** — progress home, UUID/QR, and settings (theme, TTS).

Code (1/2/3) is chosen **once at profile creation**, changeable later in Profile. It silently filters all content — the learner never picks a "category" mid-practice. **Changing the code** prompts a confirmation with two choices: **Recalibrate** existing progress to the new code, or **Start fresh** (reset progress) — for a learner who cleared one code and wants to focus on the next.

## Voice and Tone

Encouraging guru, plain English, short. Celebrates effort, never scolds. Microcopy examples: `Start learning` (first run), `Let's master it` (home), `To save your progress, copy this UUID to import your results in another app` (profile). Wrong answers get a calm *why*, never a rebuke. (Brand voice lives in DESIGN.md § Brand & Style.)

## Component Patterns (behavioral)

- **Practice tile** — tap opens that state exactly where it was left. Mastery bar = % of that section's bank answered correctly.
- **Question card** — stem + options. Tap an option to **select** (not submit). The **Confirm** button (`{components.button-primary}`) is disabled (`{components.button-disabled}`) until one is selected; this prevents accidental submits and lets the learner think.
- **Answer feedback (Practice only)** — on Confirm: correct → green pulse + celebratory Lottie + **haptic** + a tone that rises a semitone per streak increment; wrong → red highlight on the chosen option, green on the correct one, **no haptic**, no punishing animation, plus the explanation. Correct/wrong always carry a **check/cross icon + text label**, never colour alone.
- **Streak / XP chips** — streak drives the rising tone; XP accrues on every correct answer, **more per Test-mode answer than Practice**.
- **Timer** — countdown pill, **Test mode only**.
- **Result screen** — per-section pass/fail, overall pass/fail, and per-code breakdown for combinations.

## State Patterns

- **Practice = warm.** Feedback, sound, colour, celebration, no timer, no pressure.
- **Test = cold.** No feedback of any kind during the sitting, **visible countdown timer**, a **section progress indicator ("Section 2 of 4")**, honest complete result at the end. The warmth visibly drains at "Begin Test" — the mode switch is felt. (Back-navigation scope — whole-paper vs current-section — is an open decision; see Open Questions.)
- **Empty / first-run** — one `Start learning` CTA; nothing else to decide.
- **Resume** — every practice state and the current position persist; reopening restores the exact question.

## Interaction Primitives

Tap-to-select → Confirm. Single-column vertical scroll. Bottom-anchored primary action. No drag, no long-press dependencies, no gestures required to progress (accessibility). Haptic is a *reward* signal (correct only), never an error signal.

## Persistence & Sync (invented)

Practice is **one continuous session** per state. Position and progress survive app minimise, phone lock, **battery death**, and **a different device** — state syncs to the cloud keyed to the UUID profile. **Offline-first:** practice works with no connection at all; state syncs when connectivity returns, using **small diffs** to minimise data usage (SA learners are data-cost conscious). "Do 3 questions on a smoke break, finish at home" is the design guarantee.

## Identity & Profile (invented)

- **First run:** `Start learning` generates a UUID **silently** in the background — no upfront friction.
- **Profile:** the UUID is visible and copyable, with the save-your-progress note. **Transfer/linking is by QR code only** (QR encodes the UUID) — no manual paste.
- **Settings:** theme (light default; **dark mode is a profile setting** that also governs the future web surface) and **TTS opt-in**.
- *Future (out of scope v1):* scan the QR on a companion website to continue on the web.

## Progression & Rewards (invented)

Progress is a first-class screen: **per-section mastery bars**, **% of the bank mastered**, a **"you'd pass" readiness meter**, and **streaks / best runs**. Celebratory but never punishing — the emotional contract is confidence, not anxiety.

## Accessibility Floor (behavioral)

Consumer-grade, accessibility-critical. Body text never below 17px; tap targets ≥ 48px. **TTS reader** (opt-in, Settings) reads questions and options for reading difficulties. Support for visually impaired users (scalable type, high-contrast tokens per DESIGN.md). No interaction depends on colour alone (correct/wrong also carry icon + position, not just hue). No gesture-only paths. Nothing about a wrong answer induces anxiety.

Additional floor (from accessibility review):

- **Screen-reader semantics** — opt-in TTS is *separate* from the platform screen reader. Provide Flutter `Semantics` labels on every option, live-region announcements for selected / correct / result, and a sane focus order. Do not conflate the two.
- **Dynamic type** — honour OS text scaling up to 200% (`MediaQuery.textScaler`); cards reflow/grow rather than clip or truncate.
- **Reduced motion** — respect `prefers-reduced-motion`: replace the celebratory Lottie/pulse with a static success cue.
- **Contrast** — dark-mode primary button uses dark ink text (not white) to hold AA; verify success/danger highlights against their soft backgrounds in both themes.

## Key Flows

**Thabo (19, second attempt at his Code 1) — from first launch to test-ready.**

1. **First launch** — taps `Start learning`. UUID born silently; picks **Code 1** once, never thinks about categories again.
2. **Returning** — the app drops him exactly where he left off in Randomized Training, three days and one dead battery ago. Nothing lost.
3. **The rhythm** — 2 Rules, 2 Signs, 1 Control, rolling. Tap → Confirm wakes → confirm. Green pulse, tone a semitone higher, streak at 8. A wrong one: gentle red on his pick, green on the answer, no buzz, and the *why*.
4. **Interruption** — the taxi comes; he locks the phone mid-question. It doesn't matter.
5. **Climax** — that night on his Profile, the **"you'd pass" readiness meter crosses into green for the first time.** He believes he'll actually pass.
6. **Rehearsal** — he hits **Test mode.** The warmth drains: grey, a visible countdown, no sounds, no hints; he can page back and change answers, but he's on his own. The result is honest and complete — per section, overall, pass/fail. It stings a little. Better here than at the DLTC.

## Open Questions

- **Test-mode back-navigation scope** — the learner can revisit/change answers, but is that across the *whole paper* or only *within the current section*? Real-CLLT behaviour must be confirmed; it affects the section-progress model and fidelity.
- **Readiness "you'd pass" meter** — the green threshold (what mastery = "you'd pass") is undefined; needs a rule.
- **Recalibrate mapping** — how existing per-section progress maps onto a new code on "Recalibrate" (data-model detail for the code-change flow).

## Admin surface (invented)

Blazor, desktop-first, utilitarian. Oom-the-examiner's jobs (from SPEC): author questions, bulk-import CSV/JSON with reject-on-error validation, manage the sign catalog, publish/unpublish.
