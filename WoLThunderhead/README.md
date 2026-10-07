# Thunderhead (Wizard of Legend)

A new **Lightning standard arcana**, added with [LegendAPI](https://github.com/yekoc/LegendAPI).

> **Thunderhead**: Rise into the air, untouchable, and call down a storm of lightning on everything
> beneath you!
>
> *Enhanced: The storm lasts longer and strikes wider!*

- Your wizard **rises into the air**, arms raised and crackling, and hovers there. Like the game's
  jumping arcana, they're **airborne and can't be hurt** until they're nearly back down.
- For **1.5 seconds**, lightning rains down on a **small area beneath you**: bolts strike all over it
  (5 damage each, one every 0.12 s) and a **big strike** lands right below you every half second
  (8 damage, with a camera jolt). Strikes can **shock**.
- Then you drift back down. Dash to cancel at any time. Cooldown 6 seconds.
- **Enhanced**: the storm lasts 2.2 seconds, strikes more often (every 0.09 s) and covers a wider
  area.

The bolts are the game's own lightning from the sky (just its animation; the hits come from the
game's lightning bursts). The log says `Thunderhead: using the game's lightning bolts`.

## Install

Needs BepInExPack of Legend, **LegendAPI** and **HookGenPatcher**. Download `WoLThunderhead.zip` from
the [latest release](https://github.com/mdbailey94/wol.mod/releases/tag/latest) and unzip it into
your Wizard of Legend folder. It shows up in the arcana shop (or straight away with Unlock Mod
Content on).
