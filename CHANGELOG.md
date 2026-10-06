# Changelog

## 2026-10-06 - Actions dependency revision

- Pin the existing AethertekUI checkout to published revision `6c193cf06ac67f954c549cafc2033ac0efdd630a`, which includes the Hindi text host required by this plugin. The preceding Actions run checked out the library before those APIs were published; local compilation alone did not establish runner compatibility.

## 2026-10-06 - Hindi text rendering

- Add all 478 Hindi catalog entries alongside the fourteen existing languages, retaining saved locale positions, native controls and opaque profile/service values. Route visible Hindi text, captions and editable values through Windows text shaping while preserving existing font roles and merges. Keep authored game-rendered DTR and toast text in English for Hindi. The Debug x64 build passes without warnings or errors; the focused Footballer/Dheacon probe passes 6,614 assertions with exact current library/resource bytes. Game, GPU, managed-host and IME acceptance remain separate.

## 2026-10-06 - Window appearance and transparency

- Add Window appearance settings for colour, compact mode, UI language and independent main-header visibility, plus a main transparency toggle. Persist 100% normal opacity and automatic 50% unfocused opacity after 10 seconds through existing configuration, applying opacity once after the accepted native minimize/corner restore on Main, Settings and font status. Retain preview controls, image tints, saved placement and original language/compact identities. Translate new settings in all fourteen catalogs. The unchanged local launcher builds successfully with zero warnings and errors. Native appearance/persistence checks and game acceptance remain pending.

## 2026-10-05 - Main typography and header links

- Match the approved title, subtitle and regular character-name sizing with local window-font scaling, retaining the original font atlas and restoring the caller's scale.
- Align header footprint proportions, Main insets, toolbar baselines, Scaling field and help artwork, section gaps and compact card identity rows with the approved reference while preserving complete native cards and scrolling.
- Restore brighter job details, the approved palette/language fields and coloured Ko-fi/Discord link artwork, preserving native identities and existing actions and save callbacks.

## 2026-10-05 - Rounded outer chrome and native minimize

- Draw rounded Main and Settings decorations for owned platform viewports as well as ordinary native windows, retaining original title controls, scrolling, layout, identities and actions.
- Animate the existing native collapse and restore controls while preserving expanded geometry and managed constraints; retain native behavior when motion is disabled.
- Apply the same rounded chrome and native minimize lifecycle to font loading and coverage-error status under the selected theme, preserving the existing status ID and font behavior.

## 2026-10-05 - Settings paragraph sizing

- Wrap explanatory Settings paragraphs to the visible native pane after horizontal content expands, retaining single-line controls, original identities and horizontal access to wider content.
- Restore the approved Ko-fi Heart artwork while retaining the existing action and native identity.

## 2026-10-04 - UI feedback follow-up

- Restore the current assembly version in the main native title bar while retaining its window identity and close/collapse controls.
- Measure character-card names to retain single-line identity labels and allow horizontal access to wider retained Settings controls.
- Size the main Scaling field for its actual percentage and native arrow, and reflow its complete translated label/field/help group together in the approved order.
- Retain four regular cards at the default width after narrow-window use, apply the intended card padding and align job details beneath character names. Keep the additional footwear preference after the primary toolbar actions.
- Restore the approved toolbar Power, footprint, Group, Link and footwear artwork, measuring each complete translated control for reflow while retaining its native identity and action.
- Keep translated field labels on one line above readable native editors, removing hidden English-label width while preserving control identities and existing zero-step numeric defaults.
- Preserve raw padded values, names, terminal newlines and multiline details in displayed service-message templates; allow empty arguments and translate the traced nested status wrappers at the display boundary.
- Add Vietnamese, Brazilian Portuguese, Indonesian, Polish and Turkish alongside the original languages, covering displayed service and diagnostic messages.
- Localize sourced self/row/world fallbacks and capture-stage labels while retaining native schema values and complete raw exception details, including separator-like text. Match refresh progress with its integer grammar so the self suffix stays with the character label.
- Keep lookup exception notes raw using their producer metadata and update displayed capture guidance to the current translated action names; log and copied-report text stays intact.

## 2026-10-03 - Translation encoding repair

- Restore the original authored nine-language resources after a Windows PowerShell pipe replaced native characters and punctuation with question marks. Correct seven damaged punctuation keys to match their displayed text.
- Merge the installed Segoe UI Symbol face into managed font roles, correct the verified Korean CJK face, include English fallback/support glyphs in the ranges and identify the role in missing-glyph diagnostics. Font files stay on the host.

## 2026-10-03 - Compact appearance

- Added the approved compact presentation with a header C checkbox and localized Compact mode setting, saved through existing configuration without changing its version.
- Shared density across all windows; retained readable body typography, native actions, saved widths and independent status colours.
- The main header displays the actual assembly version; its separate read-only AethertekUI Actions credential is now provisioned.


## 2026-10-02 - Build and release repair

- Pin GitHub builds to SDK 10.0.201 and pass the downloaded Dalamud library path. Restore and build plugin projects with matching configuration, platform and runtime; stop on restore failure.
- Keep build tokens read-only and release writes in a separate job. Use packaged manifest versions for untagged releases.
- Checkout private AethertekUI as a sibling using AETHERTEKUI_DEPLOY_KEY. Its consumer-specific read-only credential remains a CI prerequisite.
- Local launchers build the plugin directly in the pinned environment and return its exit status.

## 2026-10-02

- Adopted the approved Footballer main-window layout with native chrome, rose vector branding, a responsive toolbar, capture/privacy counts, and one saved feet preview per character card.
- Added **Without footwear** to the main toolbar using the existing saved preference and preview-only removal handler; retained the Settings control.
- Added shared colour/language selectors, relative whole-theme colours, nine embedded language resources, locale formatting and managed Segoe/CJK fonts with explicit readiness checks.
- Preserved native IDs, capture/privacy behavior, commands, window positioning and retained Settings/debug layouts. Logs, copied reports and raw diagnostic data remain in their original form.
- Updated the local batch launcher to build the plugin project with the pinned AethertekUI environment and packaging disabled. Versions remain Footballer 0.0.1.3, configuration 4 and AethertekUI 0.3.0.

## 2026-10-01

- Build only the plugin project in GitHub Actions so test and regression projects do not block production artifacts.

## 2026-07-25

- Split the normal-mode toolbar into clear primary and secondary workflow rows.
- Added an always-visible party-feet refresh status and contextual guidance for disabled, solo, and uncaptured states.
- Disabled refresh and preview-scaling controls while the sequential party capture queue is active so the running queue cannot be reset.
- Added compact foot status to normal showcase cards and actionable missing-preview guidance.
- Kept debug/research surfaces, services, privacy gates, configuration, and data behavior unchanged.

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
