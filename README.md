# Footballer
---

**Help fund my AI overlords' coffee addiction so they can keep generating more plugins instead of taking over the world**

[☕ Support development on Ko-fi](https://ko-fi.com/mcvaxius)

[XA and I have created some Plugins and Guides here at -> aethertek.io](https://aethertek.io/)
### Repo URL:
```
https://aethertek.io/x.json
```

---

[Join the Discord](https://discord.gg/ac6gjDvR8R)

Scroll down to "The Dumpster Fire" channel to discuss issues / suggestions for specific plugins.

## Plugin Concept

- Hide feet whenever a character face is unavailable on Lodestone.
- Use live CharacterInspect as the direct character-derived seam for preview capture, a preset side-angle pose, and preview-only barefoot mode.
- Let the main window store the CharacterInspect preview window scaling so snips still land on the feet when the inspect UI is not at 100%.
- Keep portrait replacement behavior behind hidden debug research surfaces until a write path is proven safe.
- Let the main window toggle player-facing labels between real and krangled names, then persist that preference for later testing and screenshots.

## Appearance and local build

The Colour popup and Window appearance settings offer **Auto, Picker, LCARS,
Glassmorphism, Spatial and Cyberpunk**. Picker and each new material keep
independent Background, Controls and Text colours, with per-colour and palette
resets. LCARS has its own background and three bar colours. Glass has three
editable wave colours, animation on by default and speed from 0.1 to 10 times
normal (default 1); wave colours remain editable with animation off. Switching
modes retains the other palettes, native actions, privacy rules and saved
preferences. Settings and Main remain separate windows; Footballer does not
have a separate Mini window.

**UI sounds** is optional and muted by default. Appearance and the LCARS titlebar
speaker control the same saved value; Preview sound is available after enabling
it. Leaving LCARS or muting stops only Footballer's own UI tones. The titlebar
Krangle shortcut uses the existing identifying-text preference.

**Transparency** applies to the complete plugin window, including its titlebar and popups. Settings provides normal opacity, automatic focus fade, faded opacity and delay; defaults are 100%, fading to 50% after 10 seconds without focus and restoring on focus. Compact and language controls can be hidden independently on Main while remaining available in Settings.

Main's titlebar opens Settings, toggles Enabled, refreshes the party and refreshes Lodestone; refresh actions use their current readiness checks. Titled windows also expose language, compact spacing, transparency and colour shortcuts, including while collapsed; Main retains its saved icon-visibility choices. The packaged plugin icon appears in Main branding and the titlebar, including when collapsed.

The main window follows the approved `Footballer-main-v3.png` layout: packaged icon branding, a wrapping toolbar, a privacy/capture status strip, and character cards with one saved feet capture. **Without footwear** is available in both the main toolbar and Settings. It uses the existing preview-only shoe-removal handler; refresh the party to save new captures.

The colour and language selectors appear in the native titlebar and Settings. English, German, French, Spanish, Italian, Russian, Japanese, Korean, Simplified Chinese, Vietnamese, Brazilian Portuguese, Indonesian, Polish, Turkish and Hindi resources are embedded in the plugin. `UiLanguage` and `UiAccentRgb` use the existing configuration/save path; configuration version remains 4. Auto colour selection adjusts the complete decorative theme relative to the approved rose palette. Privacy and capture status colours keep their meanings.

Content uses managed Segoe UI regular, semibold and bold fonts, with Dalamud's bundled CJK coverage. Font readiness and glyph coverage are checked before the windows render. Windows font files are not distributed. Settings and the debug/research surfaces retain their layouts, native IDs and actions; raw diagnostic data, logs and copied reports retain their original text.

Hindi is enabled only when the local font check passes. Otherwise the selector shows disabled **Hindi (unavailable)** while other languages remain usable. A saved Hindi choice that fails its required-font check shows an English status and **Use English**; that button explicitly saves English. Font failures never change the saved language automatically.

Run `Z:\footballer.bat` to restore and build only the plugin project in Debug/x64, with packaging disabled. The launcher enters the sibling AethertekUI environment and uses SDK 10.0.201. The build output includes `AethertekUI.dll` beside `footballer.dll`; both are needed by the plugin. The project version is 2.0.0.11 on .NET 10 and Dalamud API 15. A consumer-only checkout needs the sibling AethertekUI repository for this local project reference.

GitHub Actions checks out Footballer and the private AethertekUI repository as siblings, using the existing read-only `AETHERTEKUI_DEPLOY_KEY` Actions secret. It renders a 730 x 380 diagnostic preview from the checked-out theme with fabricated data and a visible version/build ID. The separate preview executable is never included in the game plugin ZIP. Missing or invalid previews fail the build.

The existing root `repo.json` and project manifest carry the same versioned `ImageUrls` as the release and ZIP manifests. The normal workflow validates those lists and uploads numbered PNGs alongside the existing plugin assets under `v<plugin-version>`. The existing version bumper advances the plugin's own release-image URLs with its version. Keep using `https://aethertek.io/x.json` or the existing TheDumpsterFire feed; its builder must preserve `ImageUrls`. No additional repository URL is needed. Installer appearance requires a separate manual check.

The approved reference uses 1536 × 1024 at 100%. Smaller windows and longer translations can wrap the toolbar and reduce the number of cards per row. Game font readiness, textures and visual fidelity require mcvaxius's screenshot acceptance; local build checks do not establish that acceptance.

Compact mode is available from its native titlebar shortcut when visible and in Settings. `UiCompact` defaults on, uses the existing save path, and shares reduced padding and control/card/row density across windows. Body text remains readable; optional columns and saved widths are preserved.

## Support logs

Use **Copy / ZIP Dalamud log** in Settings > Settings to create a local ZIP and open its folder. At 100 MiB or above, the first click warns that logging may have stopped and recent activity may be missing; click **Export capped log anyway** only if you still want that snapshot. Share the ZIP manually and remove exports when no longer needed. **Open Export Folder** reopens the completed export’s folder.

When XA Slave is loaded, **Open XA Slave log tools** opens its **Utility > XA Mods** panel, which contains Dalamud Log Cleaner. The existing **Copy / ZIP Dalamud log** action remains separate. Opening the panel does not run cleanup or change XA Slave settings.

Compact mode defaults on. The main Compact and Transparency controls start hidden; Appearance settings keeps density, transparency and independent main-control visibility choices. The one-time migration preserves opacity and unrelated preferences, and later loads retain your choices.
