# RaidBeacon pirate fleet - visual checkpoint

Standalone development preview, not the live raid action. The HTML, CSS and JS remain separate. C# integration is deliberately pending until this visual checkpoint passes. No goose files or previous C# checks are overwritten.

## Install and run

Merge the contents of this package into your existing `streamerbot-raidbeacon` project: `tests`, `assets` and `docs` are paths relative to the repository root. Do not create another nested repository folder.

1. Open `tests/fleet-overlay.html` in a browser. It starts automatically; refresh to replay.
2. In OBS, add a Browser Source `RaidBeacon Fleet` to the RaidBeacon scene.
3. Enable Local file and choose `tests/fleet-overlay.html`.
4. Set width/height to your canvas dimensions (for example, 1920 by 1080).
5. Hide the older goose/image/text test sources.
6. Refresh the fleet source to replay. Test off-stream: this preview starts on page load.

The ocean covers the entire canvas while active. It becomes transparent only after all ships, projectiles and impacts finish. This is a development preview; reconnect/replay control, production queuing, sound, raid messages, native shoutouts and shaders are not implemented here.

## Behaviour

Five ships per viewer. Every ship carries two independently animated cannons, one on each side; each cannon fires once. Thus 12 viewers create 60 ships, 120 shots and 120 impacts. Targets are randomized outward into the water. Target selection attempts to avoid current hull positions, but ships may subsequently sail over an impact; effects render underneath ships.

The original ship faces down, so the fleet sails from top to bottom. Speeds and small lateral bobbing vary. Ships queue beyond the simultaneous limit: the total count is never silently reduced. At 8 ships per second, 100 viewers means 500 ships and roughly a minute of spawning, plus travel time. Higher counts produce a longer full-screen alert. No duration limit is implemented in this checkpoint.

## Settings at the top of fleet-overlay.js

| Setting             | Meaning                                                         |
| ------------------- | --------------------------------------------------------------- |
| testViewers         | Simulated raid count; positive integer                          |
| shipsPerViewer      | Default 5; positive integer                                     |
| waterColor          | `#1C85D9`, sampled from the source artwork                      |
| assetFolder         | Relative asset directory, resolved from HTML location           |
| shipsPerSecond      | Rate of introducing ships                                       |
| maxActiveShips      | Concurrent ship limit; waiting ships are preserved              |
| shipWidth           | Approximate ship width in canvas pixels before random variation |
| minSpeed / maxSpeed | Vertical travel speed in pixels per second                      |
| cannonSize          | Rendered square size of each cannon frame                       |
| impactSize          | Rendered square size of each impact frame                       |

The animation uses simulation time with a capped delta. If rendering stalls it slows rather than skipping ships/shots. Later C# will own user-facing raid configuration and pass validated values to the renderer.

## Asset provenance and transformations

Scallywag - Ships (1.0), artwork commissioned from Pixel Carvel, distributed by Foozle.
Source: https://foozlecc.itch.io/scallywag-ships
The uploader marks the pack as made without generative AI. See `assets/visual/ships/LICENSE-source.txt`, copied unchanged from the supplied Readme.txt, for the author's CC0 declaration.

No new artwork was generated. The bundled PNGs are deterministic extracts:

- ship.png: crop `(552,400)-(696,656)` from Ships Mockup.png (144 x 256), the brown ship with two pale sails and wake.
- cannon.png: three 160 x 160 frames from Ship Canon 1 Animation.gif in horizontal order; original timing 150 ms per frame.
- impact.png: four 320 x 320 frames from Explosion 1.gif in horizontal order; original timing 150 ms per frame.
- Exact solid-water RGB `(28,133,217)` pixels were made transparent in the extracted assets for correct layering. All remaining RGB values were preserved.
- Right cannons are mirrored at rendering time; sprite dimensions are not inferred from the old goose assets.

## Validation record

2026-10-07:

- PASS: JavaScript syntax checked using Node.
- PASS: headless logic harness, 1 viewer -> 5 ships / 10 shots / 10 impacts.
- PASS: headless logic harness, 12 viewers -> 60 ships / 120 shots / 120 impacts.
- PASS: headless logic harness, 100 viewers -> 500 ships / 1000 shots / 1000 impacts.
- PASS: invalid zero-viewer input rejected.
- PASS: simulated missing asset reports error and does not start playback.
- PENDING: visual playback in a real browser and OBS; frame rate and composition are not verified by the headless harness.

Run logic checks with `node tests/fleet-count-check.cjs` from the repository root. This harness stubs image loading and drawing; it checks lifecycle counts and error paths, not pixels.

## Streamer.bot and OBS integration — 2026-10-07

Passed manual checks:

- C# starts the fleet through the OBS Browser Source.
- One viewer produces five ships and ten shots.
- Ships travel from left to right.
- The ocean and effects clear after playback.
- Repeating the same test starts a fresh fleet.
- Twelve viewers produce sixty ships.
- A zero-viewer input is rejected.

Asset location: `assets/visuals/ships/`
Test action: `RaidBeacon - Fleet Check`
Test code: `tests/FleetOverlayCheck.cs`

Actual incoming Twitch raid verification remains pending.
Overlapping alerts and replay protection are not implemented yet.

## Fleet integration checkpoint — 2026-10-07

Status: Passed manual testing in Streamer.bot and OBS.

- C# successfully starts the fleet overlay.
- Each viewer produces five ships.
- Each ship fires two shots.
- Ships travel from left to right.
- The ocean and effects clear after playback.
- Repeated tests start a fresh fleet.
- Invalid zero-viewer input is rejected.

Assets are stored in `assets/visuals/ships/`.
The test action is `RaidBeacon - Fleet Check`.

## Configurable raid messages — 2026-10-07

Status: Implementation provided; runtime verification pending.

### Changes

- Added an upright HTML message panel above the rotated canvas.
- Added configurable heading and message templates to the C# action.
- Added placeholders for display name, viewer count and singular/plural wording.
- Encoded messages before passing them through URL parameters.
- Used `textContent` to display received values as plain text.
- Added message cleanup after playback and during error handling.

### Manual verification

- [x] One viewer displays “1 raider” alongside five ships.
- [x] Twelve viewers display “12 raiders” alongside sixty ships.
- [x] Accented characters and ampersands display correctly.
- [x] The message remains upright.
- [x] The message and ocean disappear after the sequence finishes.
- [x] Repeating the action starts a fresh fleet and message.

### Remaining work

- Verify an actual incoming Twitch raid.
- Add chat messages and native Twitch shoutouts.
- Add sound and optional shader effects.
- Handle overlapping raids and unwanted replay after source reloads.

### Arrival sound integration

Standalone sound playback: confirmed working.

The fleet action requests one arrival sound per alert.
Sound playback does not wait for completion.
Audio errors are logged without interrupting the visual alert.

- [x] One viewer produces five ships and one arrival sound.
- [x] Twelve viewers produce sixty ships and one arrival sound.
- [x] Disabling audio preserves the visual alert.
- [x] A missing audio file preserves the visual alert and logs an error.
- [x] Another run works after the previous fleet finishes.
- [x] Both the overlay and sound are captured in an OBS recording.

Live Twitch shoutout verification remains pending.

### Completion tracking and queueing

The fleet action waits for overlay status reports through the
Streamer.bot WebSocket server.

Alerts use the blocking `RaidBeacon Alerts` queue.
Status reports use the separate blocking `RaidBeacon Signals` queue.

Each execution has a unique run ID and temporary state.
Inactive run reports are ignored.
The arrival sound is requested after the overlay reports startup.
The browser source is reset to `about:blank` after completion or failure.

Recovery settings:

- Startup timeout: 20 seconds.
- Playback timeout: 1800 seconds.
- Status polling interval: 100 milliseconds.

The playback timeout is an emergency limit and can interrupt a fleet
that exceeds it.

### Verification — 2026-10-08

- [x] Both C# actions compile and execute.
- [x] One alert reports startup and completion.
- [x] Two overlapping requests execute sequentially with different run IDs.
- [x] Arrival sound playback is requested once per successful alert.
- [x] An unavailable WebSocket server triggers the startup timeout.
- [x] Missing assets report failure and release the queue.
- [x] Failed startup does not request arrival audio.
- [x] Playback completes after restoring the server and assets.

Live Twitch shoutout verification remains pending.

## Your visual checks

1. Test 1 viewer: five ships, each fires once from each side; ten visible impacts in total.
2. Test 12 viewers: larger fleet; no blue rectangular patches around cannons or explosions.
3. Observe whether cannon positions, ship speed and size fit your canvas.
4. After the last impact, the entire ocean disappears to reveal the scene underneath.
5. Refresh: a fresh complete sequence starts.

Record your results before committing as a passing OBS integration.

Suggested commit after the visual check passes:
`feat: add pirate fleet overlay with cannon fire and impacts`
