# Footballer UI/UX Recommendations

**Review date:** 2026-08-18  
**Scope:** UI code review only; no runtime behaviour or implementation changes are included in this document.

## Product goal

Refresh a party showcase, respect Lodestone privacy, understand why a card is unavailable, and capture a correctly scaled preview.

## Reviewed surfaces

- `footballer/Windows/MainWindow.cs`
- `footballer/Windows/ConfigWindow.cs`

## What is already working

- Normal mode intentionally keeps research surfaces behind a session-only debug mode.
- Lodestone face availability gates foot display and is explained as a privacy rule.
- Party refresh, card status, inspect, capture, scaling, Krangle, and stored display defaults are represented.

## Prioritized recommendations

| Priority | Recommendation | Rationale and completion signal |
| --- | --- | --- |
| P0 | Give every showcase card one clear state. | Use Loading, Ready, Private, Not found, Inspect required, and Capture failed badges with one relevant next action per state. |
| P0 | Make Refresh party a visible job. | Show per-character progress, current inspect target, completed/failed counts, cancel, and retry failed rather than only `Refreshing...`. |
| P0 | Design a real empty state. | When no party is available, explain whether the user must log in, form a party, enable the showcase, or refresh, and provide the applicable action. |
| P1 | Replace long normal-mode research prose with concise help. | Keep the privacy promise and current workflow in two short lines; move capture mechanics, seams, crop defaults, and test-report guidance to debug/help. |
| P1 | Preview display defaults visually. | Show a sample card while toggling face, own feet, male/female display, barefoot preview, portrait replacement, and UI scaling. |
| P1 | Make scaling part of capture readiness. | Show selected scale beside Capture and warn when the CharacterInspect preview scale cannot be confirmed. |
| P2 | Keep privacy status persistent. | Use a visible privacy-on badge and explain unavailable cards without exposing names when Krangle is active. |

## Suggested information hierarchy

1. Privacy and refresh status
2. Showcase cards
3. Capture controls
4. Display preview/settings
5. Debug research

## Validation checklist

- A new user can identify the primary action and current blocker within five seconds.
- Every disabled control has a nearby plain-language reason and, when possible, a direct corrective action.
- Healthy, warning, error, running, and disabled states remain distinguishable without colour.
- The UI remains usable at narrow window widths and common Dalamud UI scales without clipped labels or unreachable controls.
- Destructive, global, or high-impact actions identify their scope and require confirmation or provide a safe undo.
- Empty, loading, stale-data, success, partial-success, and failure states each provide an appropriate next action.
- Settings clearly identify whether they apply globally, per account, per character, per preset, or only for the current session.
- Advanced diagnostics are still reachable but do not compete with the everyday workflow.

## Recommended implementation order

1. Implement P0 items and validate the primary workflow plus blocker recovery.
2. Implement P1 information-architecture and configuration improvements.
3. Apply P2 polish, then test at multiple UI scales with both fresh and mature configurations.
