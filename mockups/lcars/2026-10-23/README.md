# Footballer LCARS concepts

Approved LCARS design concepts. Implementation is selected for the repositories in Z:\myrepos.txt, with the rollout tracked in Z:\!todo\2026-10-22GOATFUCK0002.md. These images are design references, not runtime or test evidence.

- [Main window comparison](01-main-regular-compact.png)
- [Settings and supporting owners](02-settings-mini-and-auxiliary.png)
- [Exact source-checked prompts](PROMPTS.md)

The image links are the planned sheet filenames; root review publishes the final generated artwork. Regular/compact compare spacing at the same readable text size. A separate Mini, wizard, access loader or browser view stays a separate owner. Selected supporting views are shown at useful scale; other source windows and conditional states retain their existing layouts and actions, rather than each receiving an illegible thumbnail. Generated text and sample values are proposals, not test results.

## Source scope

Source commit: `195b2e6c63605d2c4d02134fa0a8a3da30d4a51a`. Existing dirty paths were inspected by count (0); none are included or changed by this mockup task.

Normal showcase and debug-only research controls are deliberately separate; there is no source Mini.

## Verified controls and owners

- footballer/Windows/MainWindow.cs:284 — normal toolbar and scaling selector; debug toolbar is separate at 321.
- footballer/Windows/MainWindow.cs:720 — normal card status/images; debug-only Search/Inspect/Profile/Retry at 741.
- footballer/Windows/ConfigWindow.cs:67 — Live Shell Controls; stored defaults at 106; Window Behavior at 181.

## Existing UI source inventory

- `footballer/Windows/ConfigWindow.cs`
- `footballer/Windows/MainWindow.cs`

## Shared appearance proposal

Auto / Picker / LCARS remain in that order, with LCARS third. Preserve native actions, IDs, saved scope and semantic status colours. Krangle obfuscates identifying text. Proposed shared UI tones default off; titlebar speaker and Settings use the same value, with no volume slider. Browser controls remain in the browser header; existing script-free owners do not gain unsupported timed fade. This appearance contract is approved for the allowlisted rollout; implementation and local verification remain separate from artwork approval.
