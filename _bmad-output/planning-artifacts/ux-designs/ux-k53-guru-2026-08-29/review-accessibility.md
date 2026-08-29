---
status: review
type: accessibility-review
reviewer: accessibility-reviewer
date: 2026-08-29
targets:
  - ./DESIGN.md
  - ./EXPERIENCE.md
rubric: WCAG 2.2 AA (spirit) + inclusive-design
verdict: CONCERN — 2 blocking FAILs on an accessibility-CRITICAL app
scores: { pass: 2, concern: 4, fail: 2 }
---

# K53 Guru — Accessibility Review

Scope: visual + behavioural design spines (`DESIGN.md`, `EXPERIENCE.md`) for a South African
learner's-licence practice app. Owner has declared accessibility **CRITICAL**. Target users
include people with reading difficulties, visually impaired users, and first-time/low-literacy
users ("usable by a 7-year-old"). Flutter, portrait-only, English-only v1, offline-first.

Ratings: **PASS** (meets AA spirit) · **CONCERN** (gap or risk, likely fixable in spec) ·
**FAIL** (violates AA spirit / blocks a target user).

---

## 1. Text legibility — **CONCERN**

**Justification:** 17px min body and 20px/700 question stem are stated and enforced
(DESIGN.md § Typography, `typography.min-body-size`), but the type scale is a fixed **px**
ladder with no stated support for OS **dynamic type / text scaling** or reflow, so a
low-vision user who bumps their system font size may get no benefit.

**Findings**
- Floor sizes are good and explicitly mandated ("never goes smaller").
- EXPERIENCE.md § Accessibility Floor says "scalable type" but nothing in either spine
  specifies honouring the platform text scale factor, a max scale, or reflow behaviour.
- Fixed px on cards with `min-height: 56px` risks clipping/overflow when text is scaled up.

**Recommendations**
- State that all type respects the OS text-scale setting (Flutter: `MediaQuery.textScaler`,
  do **not** hardcode `textScaleFactor: 1.0`), supporting at least 200% per WCAG 1.4.4.
- Make option cards and buttons grow with content (min-height, not fixed height) so scaled
  text does not clip; verify the question card reflows without horizontal scroll at 200%.
- Add an in-app text-size control (or defer to OS) given the reading-difficulty audience.

## 2. Colour contrast — **FAIL**

**Justification:** most ink/surface pairs pass, but the **dark-theme primary button** hard-codes
white text on a light indigo fill — `#ffffff` on `#818cf8` ≈ **2.8:1** — which fails AA for the
app's most important action (DESIGN.md `components.button-primary.text: "#ffffff"` +
`colors.dark.primary: "#818cf8"`).

**Pair analysis (approx.)**
- Light `ink #1e293b` on `card #ffffff` ≈ 13:1 — PASS.
- Light `ink #1e293b` on `surface #f8fafc` ≈ 12:1 — PASS.
- Light white text on `primary #4338ca` (primary button) ≈ 7:1 — PASS.
- Light `chip text #4338ca` on `chip bg #eef2ff` ≈ 6:1 — PASS.
- Light `muted #64748b` on `#ffffff` ≈ 4.6:1 — borderline PASS (fails on `surface`, ~4.4:1).
- Light `success #16a34a` as **text** on white ≈ 3.0:1 — fine as a 3:1 border/fill, **fails
  4.5:1 if used for text/labels**.
- Light `timer` `danger #dc2626` on `danger-soft #fdeaea` ≈ 4.0:1 — borderline, likely under 4.5:1.
- **Dark primary button: white on `#818cf8` ≈ 2.8:1 — FAIL.**
- Dark `success #4ade80` on `success-soft #0f2a1b`, `danger #f87171` on `danger-soft #2a1516`,
  `ink #e8eefc` on `card #131c2e` — all high contrast, PASS.

**Recommendations**
- Give `button-primary` a **theme-aware text token**: in dark mode use dark ink
  (e.g. `#0b1220`) on `#818cf8`, or darken the dark-mode button fill (e.g. reuse `primary-strong`
  as a solid fill) so white text clears 4.5:1.
- Never render `success`/`danger` as body/label **text** on light surfaces at 17px; keep them
  for borders/fills/icons, or darken the text variant (e.g. `#15803d` / `#b91c1c`) to reach 4.5:1.
- Nudge light `muted` darker (e.g. `#556070`) so secondary text passes on `surface`, not just `card`.
- Verify the borderline timer pill hits 4.5:1 (darken text or lighten background).

## 3. Colour independence — **CONCERN**

**Justification:** EXPERIENCE.md § Accessibility Floor **asserts** "correct/wrong also carry
icon + position, not just hue", but the actual state tokens only specify **border + background
colour** — `option-correct { border: success, bg: success-soft }`, `option-wrong { border:
danger, bg: danger-soft }` (DESIGN.md § components) — **no icon is specified**. The claim is
stated, not designed.

**Recommendations**
- Add a **non-colour marker to each answer state** in the component spec: e.g. a leading
  check glyph on the correct option and a cross/dot on the chosen-wrong option, plus a text
  label ("Correct" / "Correct answer") for screen readers and colour-blind users.
- Confirm the wrong-answer feedback distinguishes *chosen-wrong* vs *the-right-one* by more
  than red-vs-green (position + icon + label), since red/green is the classic deuteranope trap.
- Do the same audit for progress/mastery bars (green fill on a neutral track needs a value label).

## 4. Tap targets — **PASS**

**Justification:** `spacing.min-tap-target: "48px"` is defined and applied — option cards
`min-height: 56px`, primary button `height: 56px`, "48×48px … always" reaffirmed in
DESIGN.md § Layout & Spacing and Do's. Meets WCAG 2.5.8 (24px) comfortably and the app's own
48px inclusive floor.

**Minor note:** bottom-nav item and chip hit sizes are not stated explicitly — confirm nav
icons and any tappable chip also carry the 48px minimum touch area even if the visual is smaller.

## 5. TTS / screen reader — **FAIL**

**Justification:** the only assistive-reading provision is an **opt-in "TTS reader" in Settings**
that reads questions/options (EXPERIENCE.md § Settings, § Accessibility Floor). This is a bespoke
read-aloud feature and is **conflated with platform screen readers** — there is no mention of
**Flutter Semantics**, semantic labels, focus order, or TalkBack/VoiceOver support anywhere.
A blind user relying on TalkBack gets nothing from an opt-in in-app TTS.

**Recommendations**
- Separate the two concerns explicitly: (a) the in-app TTS convenience feature, and
  (b) **first-class platform screen-reader support** — the real accessibility requirement.
- Specify **Semantics** for every interactive element: options as radio-like semantics with
  selected state, Confirm button enabled/disabled announced, answer result announced via a
  live region ("Correct" / "Incorrect, the answer is …"), progress bars exposing value + label.
- Define **traversal/focus order** for the question card and bottom nav, and ensure the timer
  in Test mode is announced appropriately (see §8) without spamming.
- Make in-app TTS **not** suppress or fight the platform screen reader when one is active.

## 6. Cognitive load & anxiety — **PASS**

**Justification:** tap-to-select → explicit **Confirm** (disabled until selection) prevents
accidental submits and invites thinking; **no drag / long-press / gesture-only** paths
(EXPERIENCE.md § Interaction Primitives); wrong answers are **calm, non-haptic, non-punishing**
with a *why*; the code is chosen once and silently filters content so there is **no mid-practice
category decision**; first-run is a single CTA. This is a strong, low-anxiety model.

**Minor note:** the confirmation on "Changing the code" (Recalibrate vs Start fresh) is a rare
destructive choice — keep its copy plain and make "Start fresh (reset progress)" clearly the
heavier option to avoid accidental data loss.

## 7. Motion — **CONCERN**

**Justification:** correct-answer feedback fires a **green pulse + celebratory Lottie + haptic +
rising tone** (EXPERIENCE.md § Answer feedback), but **neither spine addresses reduced-motion**.
Nothing respects `prefers-reduced-motion` / the OS "Reduce Motion" setting, which matters for
vestibular sensitivity (WCAG 2.3.3 / 2.2.2 spirit).

**Recommendations**
- Add a spec line: when the OS reduce-motion flag is set (Flutter:
  `MediaQuery.disableAnimations` / `accessibleNavigation`), replace the Lottie/pulse with a
  **static or minimal** success indicator (e.g. a quick icon + colour change), keeping the
  tone/haptic reward intact.
- Provide an in-app "Reduce animations" toggle for parity, given the accessibility-critical bar.
- Ensure no essential information is conveyed *only* by the animation.

## 8. Timed content — **CONCERN**

**Justification:** the Test-mode **visible countdown** is intentional CLLT fidelity (EXPERIENCE.md
§ State Patterns, § Component Patterns) and WCAG 2.2.1 exempts timing that is *essential* to a
real exam simulation — so the timer itself is acceptable. **However, no accessibility
accommodation is considered**: a low-literacy / low-vision / reading-difficulty user needs more
reading time than the sighted baseline, and none is offered or even discussed.

**Recommendations**
- Keep the authentic timer as default, but **consider an optional "extended time" / "practice
  timing" accommodation** for Test mode (clearly labelled as non-official), so the timer does not
  silently exclude the exact users the app prioritises.
- Ensure the countdown is exposed to the screen reader **non-intrusively** (e.g. announce at
  milestones, not every second) and is legible at scaled text sizes.
- Note in the spec that the timer is an *essential* exception under WCAG 2.2.1, so the decision
  is explicit rather than implicit.

---

## Summary table

| # | Criterion | Rating |
|---|-----------|--------|
| 1 | Text legibility | CONCERN |
| 2 | Colour contrast | FAIL |
| 3 | Colour independence | CONCERN |
| 4 | Tap targets | PASS |
| 5 | TTS / screen reader | FAIL |
| 6 | Cognitive load & anxiety | PASS |
| 7 | Motion / reduced-motion | CONCERN |
| 8 | Timed content | CONCERN |

**Counts:** PASS 2 · CONCERN 4 · FAIL 2

**Overall verdict:** **CONCERN — not yet ship-ready.** The design is thoughtful and its
interaction/anxiety model is genuinely strong, but for an app that declares accessibility
CRITICAL two items are **blocking FAILs**: (2) the dark-mode primary button fails AA contrast,
and (5) real platform screen-reader support is absent (in-app TTS is not a substitute). Fix the
two FAILs and specify the four CONCERNs before build.
