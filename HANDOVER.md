# Handover brief: Wizard of Legend mods

This brief is for a new Claude Code session that continues the work of the cloud session
"WoL Modder". Read it first, then the README of whichever mod you're touching.

## The project

- **Repo:** `mdbailey94/WoL.mod`, working branch `claude/happy-ritchie-qit7sb`, open pull
  request #17 into `main`. Merge only when the owner says "merge".
- **What it is:** 21 BepInEx 5 mods for Wizard of Legend (Unity 5.6, .NET 3.5, C# 7.3,
  HarmonyX, LegendAPI). Each mod is a folder `WoL<Name>/` with its own `.csproj`, plugin class
  and `README.md`.
- **Owner:** Matt (mdbailey94). He plays and asks for changes in plain language.
- **Build:** `.\build.ps1` (all mods) or `.\build.ps1 WoLCyclone` on Windows with the .NET 8
  SDK. It downloads the pinned reference DLLs into `lib\` and copies each build into the base
  game's `BepInEx\plugins`. Chaos Depths is the exception: its `.csproj` never copies into the
  base game (pass `-p:ChaosDepthsProfileDir=C:\Users\mdbai\ChaosDepths\profile` to copy into
  its profile instead). CI (`.github/workflows/build.yml`) builds every push to the PR.
- **Shipping a change:** bump the mod's `PluginVersion`, add a short section to its README,
  build, zip `BepInEx/plugins/<Mod>/<Mod>.dll` (Chaos Depths also ships its `Art/` folder and
  `Profile/`), commit, push, and give the owner the zip.

## The team and how work flows

- **Master WoL task sheet** (Google Sheets `1OjZRfxg4HKvsv6V9r_rNqyZ27qZjAByavjx694gJ8qE`) is
  where the WoL Manager assigns work. Rows 1-4 set the CHECK-IN STATUS (open hours, shutdowns,
  Eastern time) and the cadence: read them before anything else, every time. Columns: ID, Date,
  Task, Category, Reported By, Assigned To, Priority, Status (H), Notes ARCHIVE (I, read-only),
  Date Done (J), File Link (K), Latest update (L, filled from the logs). Rows are added, removed
  and IDs reused, so match a task by its text as well as its ID.
- **Writing rule (Matt, 2026-10-28 12:15pm):** each bot writes only in its own tab ('Log -
  Modder' for you): append a row (Date/Time ET | Task # | Note | File Link), never edit old rows
  or other tabs. On Sheet1 change only the Status cell of your own tasks; new problems go in your
  log with Task # 'NEW' and the Manager adds them.
- WoL Player is reachable as the local session "WOL - player" (SendMessage); it tests each build
  in the profile and reports in 'Log - Player'. Install into the profile only while the game is
  closed (it locks the DLLs; check `Get-Process wizardoflegend`).
- **WoL Modder** (you) builds the mods; tasks with Assigned To "WoL Modder" are yours.
- **WoL Player** is a Claude session on Matt's PC that runs the game, reads logs, takes
  screenshots, and reports on the sheet. It has the decompiled game at
  `C:\Users\mdbai\source\wol-decompiled\Assembly-CSharp\` (the reference DLLs in `lib\` are
  stubs with no method bodies, so ask it, or read the decompile, for real game code).
- **WoL Art** draws pixel art. Files go to Drive folder "Wizard of Legend Mod - Art" >
  Finished Art (`1UvjUL3OMohimq2AWhAoQ5x4KGZ_CWQ05`); the task row gets a File Link and the
  status "Needs Review". Reference sheets from the game are in Reference (game art) >
  WoL reference art. Art must be true pixel size, transparent PNG, frames side by side.

## Since the handover (2026-10-28, local session as WoL Modder)

The evening of 2026-10-28 (Modder + Player open until 10pm, goal: a fresh Chaos Depths save):

- Chaos Depths 0.13.6-0.14.8, Ascension 0.7.0-0.7.3, Unlock Mod Content 0.2.1, Mod Menu 0.1.5:
  #30 each player opens their own inventory (needs Matt for a two-player test); #31 Fresh and
  Debugging saves picked on the title screen (Tab / LB), fresh-save drop rates; #27 depths gone
  and the approved Ascension 1-10 table (Ascension holds it, Chaos Depths reads it through
  `AscensionLink`); #32 levels unlock in order in the fresh save; #16 the inventory drawn with
  WoL Art's pieces on her mockup (`InventoryPanelArt.cs`, `[Panel] Look = Coded` for the old
  one); #5 foot auras and glowing eyes; #26 the stalls were the uncapped frame rate, fixed by
  Mod Menu's `[Display] FrameCap` (120). WoL Player passed #27 at A10, #16, #5, #26, #33 (not a
  bug: the trials portal only catches a wizard within ~1.5 u of its centre).
- Still waiting: Matt's two-player fresh-save run (#30, #31 drops and first-boss Legendary, #32
  unlocking); #23 and #29 art (WoL Art is off until Monday).

Earlier that day:

- Chaos Depths 0.13.1 (#25: pickup no longer fires the basic attack), 0.13.2 (#26: `[Debug]
  HitchLog` frame probe, cached text styles), 0.13.3 (#17: WoL Art's elite affix markers),
  0.13.4 (#26: no per-frame string keys for worn gear), 0.13.5 (#18 drop glows, #5 foot auras).
- Trailblazer 0.9.3 (#26: trail hits only near enemies; one shared texture search).
- Slingshot Dash 0.25.5 (#24: Feint Swap signature fan at full range, 52 degrees), 0.25.6 (#26:
  slingshot HUD icon no longer rewritten every frame).
- Trailblazer 0.9.2, Ascension 0.6.1, Mod Menu 0.1.4 (#26: pooled trail columns, cached styles,
  no title-screen search during runs).
- #23 (Trailblazer at level 10 = maxed): waiting on WoL Art to redraw the white-hot flame at the
  trail's 25x60 tongue size before it is hooked in.
- To build without deploying, point `-GameDir` at a copy holding only `BepInEx\core` and
  `WizardOfLegend_Data\Managed` (no `plugins` folder, so nothing is copied).

## Chaos Depths (the main project, `WoLChaosDepths`, 0.14.8)

A Diablo-like loot layer on Wizard of Legend: character level and XP, items in three slots
(Amulet, Ring, Charm) at four rarities with rolled affixes, six unique legendaries with powers,
elites (difficulty from Ascension's table; depths and layer bosses removed in 0.14.0), rifts,
physical drops picked up with Interact, gear
drawn on the wizard, and an inventory panel (I key / right stick). Design doc: Claude Doc
https://claude.ai/artifact/9ygkyb3WgVeYYtpFDpSGxM.

- **Runs as its own game profile**, never in the base game. `Profile/Install-ChaosDepthsProfile.ps1`
  sets it up in `%USERPROFILE%\ChaosDepths\profile` (not AppData: a packaged app such as the
  Claude desktop app redirects AppData writes into its sandbox, which once left the shortcut
  launching the base game). The launcher passes `--doorstop-target` to the profile's preloader
  and sets `CHAOSDEPTHS_PROFILE=1`. The installer copies the base game's other mods into the
  profile. In the profile the game's own save lives in a `ChaosDepths` subfolder of the save
  folder (`ProfileSave.cs` sets the private `GameDataManager.basePathStr` early), seeded once from
  the base save.
- **Art** loads from `WoLChaosDepths/Art/*.png`, embedded in the DLL; a file of the same name in
  an `Art` folder beside the DLL overrides it. Code-drawn pictures are the fallback.
- **Debug keys** (`[Debug] Enabled`): F3 rift, F4 uniques, F5 showcase enemies
  (`ShowcaseEnemy`, `ShowcaseHealth` for plain high-health targets; GhoulLarge works well), F6
  kill all, F7 clear floor, F8 god mode, F9 gold, F10 auto-dive, F11 relic IDs, F12 texture
  dump (`DumpTextures`) for reference art.

### Open work on Chaos Depths

- **Task #7, inventory tabs and sorting:** built to WoL Art's mockup in 0.12.0-0.12.3. Tabs on
  left/right, sort on Y/F/Tab, equipped first, VS EQUIPPED gains green / losses red. 0.12.3 moved
  the Hub depth picker to its own line (Up from the top of the list, then left/right; Q/E
  anywhere) after a held Y + right never worked, and hides 0% differences. **Untested;** WoL
  Player retests Monday. Salvage is untested too (it destroys an item, so it needs Matt's OK).
- **Task #1, slim amulets** (WoL Art's task, mod side in `Adornments.cs`): all 12 views are in.
  0.12.3 reads the wizard's outline per frame for the pendant's position, mirrored to match the
  picture as drawn, with the height at 70% of each frame's own picture. Earlier builds put it
  at the back of the neck facing left and on the hood running toward the camera. **Untested.**
- **Task #16:** WoL Art will cut the panel pieces (9-slice frame, slot and rarity frames,
  empty-slot icons, dust icon, chevrons). Then replace the code-drawn ones and match
  `inventory_mockup_1x.png` exactly.
- Waiting on art: #5 (a visible change on the wizard per unique), #8 (item subcategories by
  strongest stat), #17 elite markers, #18 drop glow, #19 rift/layer boss presentation, #20 title.
- Also pending from earlier: a seated wizard for Gale Rider (Art Requests doc #13), and a
  stronger Galeheart swirl.

## The other mods

| Mod | Version | Notes |
|---|---|---|
| Gale Rider | 0.3.7 | Ride a wind ball; drawn ball (`[Look] Ball`), held crouch on top (`RidePose`, `RideSeat`), hittable while riding, ball radius 0.8-1.6 (old defaults migrate once). Done per the sheet. |
| Cyclone | 0.4.4 | Hand-drawn twister in three sizes; `[Look] Twister = Coded` for the old funnel. |
| Bramble Ring | 0.5.2 | Standard thicket drawn in (rows 0.8 / 1.5). |
| Thunderhead | 0.9.4 | Drawn icon; crackling strike cracks. |
| Tidal Wall | 0.4.3 | Drawn icon; larger crest that keeps its throw angle. |
| Trailblazer | 0.9.3 | Flames at the feet, no ring. |
| Slingshot Dash | 0.25.6 | Holds Vacuum Fist, Blazing Kick, Feint Swap, Vine Slingshot, Charged Leap; drawn icons. |
| Rolling Gale | 0.4.2 | Drawn icon. |
| Stone Charge | 0.4.1 | |
| Ascension | 0.7.3 | Chaos Depths reads its level for loot. |
| Others | | Camera Zoom, Cloak Upgrades, Custom Paintings, Damage Meter, Duelist's Cloak, Extended Stats, Mod Menu, Potion Price, Relic Pack, Unlock Mod Content. |

Matt's base-game loadout is Stone Shot / Charged Leap / Trailblazer / Feint Swap. Restore it if
a test changes it.

## Conventions that matter

- Never commit Matt's personal photos to the repo.
- Config files keep their stored values over new defaults. If a default change must reach an
  existing install, migrate it in code; see Gale Rider's `[Ball]` sizes.
- An `AcceptableValueList` resets any value not matching exactly, including case, to the
  default. Prefer free text matched case-insensitively.
- Edits to a mod's config while the game is running are overwritten on exit.
- Commit messages end with the session attribution lines the harness gives you.
