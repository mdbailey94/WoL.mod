# Unlock Mod Content (Wizard of Legend)

While switched on, every arcana and relic that mods add through LegendAPI (such as Rolling Gale
and Slingshot) is marked **unlocked**, so it appears in your spellbook and loadout straight away
and is easy to find and test.

- Switch it on or off in the title screen **Mods** menu.
- It remembers exactly what it unlocked. Turning it **off** locks only those again (in your save
  and on your wizard, so they leave the spellbook); anything you unlocked yourself stays unlocked.
  Switched off on the title screen, they're locked as soon as your wizard loads in.
- 0.2.0 fixes switching off not sticking in 0.1.0. The first time 0.2.0 runs with the mod off, it
  locks every mod-added arcana once (0.1.0 had lost track of which ones it unlocked).
- Vanilla arcana and relics are never touched.

Needs BepInEx and LegendAPI. Download `WoLUnlockModContent.zip` from the
[latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into your
Wizard of Legend folder. The log lists each `Unlocked arcana:` / `Unlocked relic:`.
