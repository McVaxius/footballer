# Changelog

## 2026-04-09

- Added a saved CharacterInspect preview-scaling selector in the main window plus an open-time guidance toast so capture math can match preview window UI scaling from 60% to 200%.
- Added an optional one-shot auto-refresh-on-showcase-open setting in the Footballer settings window.
- Added a saved main-window `Krangle Names` / `Un-Krangle` toggle so Footballer no longer pre-krangles all user-facing labels by default.
- Updated the main window, config window, chat/status text, manifests, and README so the saved krangle preference is reflected consistently.

## 2026-04-08

- Hid the raw CharacterInspect and BannerParty research surfaces behind `/footballer debug` instead of leaving them in the normal main-window path.
- Removed the normal-window frame-by-frame inspect and portrait research polling; debug snapshots now refresh only on demand.
- Added a queued CharacterInspect side-angle pose preset so inspect requests can land back on the previously working rotation before capture.
- Replaced the single-slot barefoot attempt with a preview-only CharacterInspect multi-seam feet clear and redraw path so `Without footwear` can drive the inspect item, model, draw-data, and live preview seams together.
- Reworked the main window around the normal flow: `Inspect`, then `Capture Current Preview`, with the accepted `65 / 20` crop profile kept as the stored default.
- Updated manifests, README, and plugin status text to reflect preview-only barefoot mode plus hidden debug research surfaces.
- Removed the approved-foot-asset packaging path and updated the live showcase/config text to point at direct CharacterInspect research instead.
- Added Inspect buttons plus a live CharacterInspect/inspect-side CharaView research section so the next foot-capture work can come from the character themselves.
- Kept the foot side honest by reporting direct-capture readiness/status instead of pretending bundled art is the intended runtime path.
- Added the first live party-foot showcase cards with bundled local image rendering.
- Added cached Lodestone face thumbnails next to showcase cards when privacy and config allow it.
- Added the Footballer-local KrangleService and applied krangled labels to the live UI, reports, and Lodestone warning logs.
- Updated manifests, README, and plugin status text to reflect the live showcase phase instead of the earlier shell-only wording.
- Corrected shell metadata to `McVaxius` in `footballer.json` and `repo.json`.
- Clarified the Footballer UI so it explicitly separates live shell controls from future research/default toggles.
- Updated the README to document what is live today versus what is still unimplemented.
- Fixed the stale scaffold leftovers that still referenced the wrong earlier shell history.

## 2026-04-07

- Bootstrapped the `Footballer` repository shell.
- Added the Dalamud project, solution, plugin manifest, windows, and DTR/Ko-fi baseline.
- Added icon assets and the initial README shell.
