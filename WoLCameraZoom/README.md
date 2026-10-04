# Camera Zoom (Wizards of Legend)

A BepInEx 5 plugin that zooms the gameplay camera out so you can see more of the arena.

## Setup

1. Install BepInEx 5 (x64) into the Wizards of Legend folder, run the game once, then close it.
2. Install the .NET SDK (6 or newer).
3. Build:
   ```
   dotnet build -c Release -p:GameDir="C:\Path\To\Wizards of Legend"
   ```
   The DLL is copied to `BepInEx\plugins\WoLCameraZoom\` automatically.

## Controls

| Key       | Action             |
|-----------|--------------------|
| Page Down | Zoom out (+0.1x)   |
| Page Up   | Zoom in (−0.1x)    |
| Home      | Reset to vanilla   |

The default is 1.4x. You can change it and the hotkeys in
`BepInEx\config\mdbailey94.wol.camerazoom.cfg`.

## How it works

Just before the main camera renders, the plugin multiplies its `orthographicSize`.
Right after rendering, it puts the original value back. Game code never sees the
changed value, so camera follow, room bounds and spawning behave as in the
unmodified game. Only how much you can see changes.

## Things to check in-game

- **Black edges / void**: some rooms may not be built for a wider view. If so, lower the zoom.
- **Off-screen enemies**: enemies that are now visible might still be inactive because the
  game decides by its own camera bounds. If that feels off, the next step is a Harmony patch
  on whatever the game uses to check visibility.
- **UI**: only `Camera.main` is scaled, so the HUD should stay the same size. If the HUD
  shrinks too, the game draws UI with the main camera and we'll need to target the camera
  by name instead.
