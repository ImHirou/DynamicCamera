# Dynamic Camera

A client-side mod for Vintage Story 1.22. The camera leans into strafes and turns, pitches with your speed,
sways gently while you stand still, and shakes from explosions, lightning, hard landings and hits.
Everything is visual only.

## Features

- **Movement tilt.** Strafing rolls the camera, moving forward and up or down pitches it. Each situation has its own profile,
  smoothly blended: walking, sprinting, swimming, glider, creative flight, climbing, mounts and boats.
- **Mouselook roll.** Turning quickly banks the view.
- **Idle sway.** A slow, subtle sway fades in while you stand still.
- **Camera smoothing.** Optional, per profile.
- **Screen shakes.** Explosions, lightning strikes and thunder, hand swings, rockslides, falling trees, the pulverizer.
  Any other sound can be added to the list in the settings.
- **Smooth vanilla shakes.** The game's own camera jitter (damage, weapon hits, spear throws, translocators, temporal storms,
  server effects) becomes the mod's smooth shake.
- **Landings and hits.** Hard landings dip the camera, hits knock it away from the attacker.
- **Targeting follows the camera.** The crosshair always marks what you interact with, even while the view lags behind or tilts.
- **Comfort.** Respects the game's "Camera shake strength" accessibility slider; a global intensity slider and soft angle limits
  keep the effects in check.

## Requirements

- Vintage Story 1.22 or higher.
- Optional: **Dear ImGui** (`vsimgui`) for the in-game settings window, and **Config lib** (`configlib`) to also show it in the shared config menu.

## Usage

| Input | Action |
|---|---|
| `Ctrl+Shift+O` | Open the settings window (needs vsimgui) |
| `Ctrl+Shift+K` | Turn the effects on or off |
| `.dcam settings` | Open the settings window |
| `.dcam toggle` | Turn the effects on or off |
| `.dcam reload` | Read `ModConfig/dynamiccamera.json` again |

Hotkeys can be changed in the game's controls settings. Without vsimgui, edit `VintagestoryData/ModConfig/dynamiccamera.json`
and run `.dcam reload`. If the file contains an error, the mod keeps the current settings and saves a copy of the broken file
as `dynamiccamera.json.broken`.

## Building

Set the `VINTAGE_STORY` environment variable to the game folder, then run `./build.sh` (or `build.ps1`) in the `DynamicCamera` folder.
The packaged mod is written to `DynamicCamera/Releases`.

## License

GNU General Public License 3.0, see [LICENSE.md](LICENSE.md).

---

The original idea and the code base come from [CameraOverhaul](https://github.com/Mirsario/Minecraft-CameraOverhaul) by Mirsario.
Оригинальная идея и основа кода взяты из репозитория [CameraOverhaul](https://github.com/Mirsario/Minecraft-CameraOverhaul) от Mirsario.
