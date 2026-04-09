# Footballer

[Support development on Ko-fi](https://ko-fi.com/mcvaxius)

[Join the Discord](https://discord.gg/VsXqydsvpu)

Scroll down to "The Dumpster Fire" channel to discuss issues / suggestions for specific plugins.

## Current Status

Footballer is past the shell stage. The current `Debug x64` build now has a live party-foot showcase surface, cached Lodestone face gating, a saved main-window krangle toggle for player labels, a preset CharacterInspect side-angle pose, action-driven CharacterInspect preview capture, preview-only barefoot mode, and hidden `/footballer debug` research surfaces for CharacterInspect and BannerParty.

- Solution: `Z:\footballer\footballer.sln`
- Project: `Z:\footballer\footballer\footballer.csproj`
- Commands: `/footballer`, `/foot`
- Repository target: `Private`

### Live today

- Main window and settings window
- DTR toggle entry
- Party snapshot surfaces that follow the saved krangle toggle
- Live party foot showcase cards with honest direct-capture status
- Cached Lodestone face lookups with local face thumbnails
- Inspect buttons plus action-driven CharacterInspect preview capture
- Queued preset CharacterInspect side-angle pose on inspect request
- Preview-only barefoot multi-seam apply path when `Without footwear` is enabled
- Hidden `/footballer debug` CharacterInspect and BannerParty research surfaces
- Ko-fi + Discord links
- Standard `/footballer ws` and `/footballer j` window helpers

### Not implemented yet

- Direct foot-only crop/export from CharacterInspect
- Portrait write/import path
- Party portrait swapping
- Any non-local publish/release flow

## Plugin Concept

- Hide feet whenever a character face is unavailable on Lodestone.
- Use live CharacterInspect as the direct character-derived seam for preview capture, a preset side-angle pose, and preview-only barefoot mode.
- Keep portrait replacement behavior behind hidden debug research surfaces until a write path is proven safe.
- Let the main window toggle player-facing labels between real and krangled names, then persist that preference for later testing and screenshots.
