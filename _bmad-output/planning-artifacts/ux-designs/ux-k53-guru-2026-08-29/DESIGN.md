---
status: final
updated: 2026-08-29
sources:
  - ../../../specs/spec-k53-learners-app/SPEC.md
  - ../../../specs/spec-k53-learners-app/test-structure.md
colors:
  light:
    primary: "#4338ca"        # indigo — primary actions, headers, selected state
    primary-strong: "#3730a3"
    accent: "#0ea5a4"         # teal — highlights, streak, brand accent
    success: "#16a34a"        # green — correct answer, progress fills, "pass"
    success-soft: "#e7f7ec"
    danger: "#dc2626"         # red — wrong-answer highlight only (gentle, non-punishing)
    danger-soft: "#fdeaea"
    surface: "#f8fafc"        # app background
    card: "#ffffff"
    ink: "#1e293b"            # primary text
    muted: "#64748b"          # secondary text
    line: "#e2e8f0"
  dark:
    primary: "#818cf8"
    primary-strong: "#a5b4fc"
    accent: "#2dd4bf"
    success: "#4ade80"
    success-soft: "#0f2a1b"
    danger: "#f87171"
    danger-soft: "#2a1516"
    surface: "#0b1220"
    card: "#131c2e"
    ink: "#e8eefc"
    muted: "#94a3b8"
    line: "#24314b"
typography:
  family: "Inter, system-ui, -apple-system, 'Segoe UI', Roboto, sans-serif"
  scale:
    display: { size: "28px", weight: 800, line: 1.2 }   # screen titles
    h2: { size: "22px", weight: 800, line: 1.25 }
    question: { size: "20px", weight: 700, line: 1.4 }   # question stem — large by mandate
    body: { size: "17px", weight: 400, line: 1.5 }       # min body size; never smaller
    option: { size: "17px", weight: 600, line: 1.4 }
    label: { size: "13px", weight: 700, line: 1.3 }      # chips, meta
  min-body-size: "17px"
rounded:
  sm: "8px"
  md: "14px"     # option cards, buttons
  lg: "20px"     # tiles, sheets
  pill: "999px"  # chips, progress bars
spacing:
  base: "4px"
  scale: ["4px","8px","12px","16px","20px","24px","32px","48px"]
  min-tap-target: "48px"
components:
  button-primary: { bg: "primary", text: "theme-aware: #ffffff on light primary; #0b1220 (dark ink) on dark-mode primary for AA contrast", radius: "md", height: "56px", weight: 800 }
  button-disabled: { bg: "line", text: "muted", radius: "md", height: "56px" }   # Confirm, pre-selection
  option-card: { bg: "card", border: "line", radius: "md", pad: "16px", min-height: "56px" }
  option-selected: { border: "primary", bg: "#eef2ff" }
  option-correct: { border: "success", bg: "success-soft" }
  option-wrong: { border: "danger", bg: "danger-soft" }
  progress-bar: { track: "line", fill: "success", radius: "pill", height: "10px" }
  chip: { bg: "#eef2ff", text: "primary", radius: "pill", pad: "6px 12px" }
  timer: { bg: "danger-soft", text: "danger", radius: "pill" }   # test mode only
---

# K53 Guru — DESIGN.md

> Visual identity contract. On conflict with any mock, import, or the admin component library, this file and `EXPERIENCE.md` win. Tokens are referenced from `EXPERIENCE.md` by `{path.to.token}`.

## Brand & Style

K53 Guru is a **calm, trustworthy guru** — the master who makes *you* the master of your learner's licence. Government-adjacent credibility, but warmer and friendlier than any official portal. The reference register is **Brilliant** (the education app): clean, structured, quietly confident, *educative not gamey*. The rewarding feedback (streaks, sounds, XP) lives in *behaviour*, never in loud visuals — the surface stays composed. Direction B ("Focused Coach") is the chosen identity.

## Colors

Indigo primary (`#4338ca`) carries actions and identity; teal (`#0ea5a4`) accents and marks streaks; green (`#16a34a`) is reserved for *correct* and *progress/pass*. Red (`#dc2626`) appears **only** as a gentle wrong-answer highlight — never as an alarm, never with haptics. Light is the default theme; **dark mode is a profile setting** (not a system toggle) and the same preference drives the future web surface. Full light/dark token sets in frontmatter.

## Typography

One crisp sans family (Inter or platform equivalent). **17px is the minimum body size and it never goes smaller** — legibility is an accessibility mandate, not a preference. Question stems render at 20px/700 so they read on a cracked screen in the sun. Weights: 400 body, 600–700 emphasis, 800 titles and primary buttons.

## Layout & Spacing

4px base scale. Portrait-only, single-column, thumb-reachable. **Minimum tap target 48×48px** with generous padding — a 7-year-old (and a nervous adult) must hit the right option without looking. Content breathes; density stays low.

## Elevation & Depth

Flat and soft. Cards use a single low shadow (`0 2px 10px rgba(15,23,42,.06)`) on the app surface; the guru register avoids heavy drop-shadows and skeuomorphism. Test mode deliberately *removes* warmth and elevation (see EXPERIENCE.md State Patterns).

## Shapes

Soft rounded throughout: 14px option cards and buttons, 20px tiles and sheets, pill chips and progress bars. No sharp corners.

## Components

Practice-home **tiles** (four: Randomized Training, Rules, Signs, Vehicle Controls) each carry a green mastery bar. **Option cards** have four visual states — default, selected (indigo), correct (green), wrong (red, gentle). The **Confirm** button spans the bottom and is disabled (grey) until an option is selected. **Chips** show streak and XP. The **countdown timer** (danger-tinted pill) renders in Test mode only. Behavioral specs live in EXPERIENCE.md.

## Do's and Don'ts

- **Do** keep body text ≥ 17px and tap targets ≥ 48px, always.
- **Do** reserve green for correct/progress, red for gentle wrong-highlight only.
- **Don't** make wrong answers loud, animated, or haptic — never punish.
- **Don't** let dopamine leak into the visuals as clutter; keep the guru calm.
- **Don't** carry practice warmth (colour, sound, celebration) into Test mode.
- **Do** keep the dark-mode primary button on **dark ink text** (not white) so it meets AA contrast.
- **Do** pair every correct/wrong state with an **icon (check / cross) and text label**, never colour alone.
