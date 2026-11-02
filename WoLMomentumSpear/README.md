# Momentum Spear

An equippable **Air basic arcana** for Wizard of Legend: three successive spear thrusts, ending in a longer, stronger thrust. The opening hit scales with **actual movement/momentum**, as requested. A movement-speed stat bonus while standing still does not increase it.

| Combo hit | Default base damage | Behavior |
|---|---:|---|
| 1 — opening thrust | 8 | Receives the captured momentum multiplier |
| 2 — follow-up thrust | 10 | No momentum scaling |
| 3 — extended finisher | 16 | No momentum scaling; 25% longer/larger native attack scale |

Enhanced adds **15% forward reach** to all three hits. The skill has zero arcana cooldown and uses the game's normal basic-attack recovery, combo progression and hold-to-attack support.

## Momentum calculation

Default opening multiplier:

    min(2, 1 + 0.1 × actual speed)
    actual speed = sqrt(rigidbody velocity X² + rigidbody velocity Y²)

With no other damage modifiers or critical hits, standing still gives 8 opening damage, moving at 5 units/second gives 12, and moving at 10 units/second or faster gives the capped 16. Hits two and three remain 10 and 16. The game's native damage pipeline applies its normal rounding, combat modifiers and target handling.

Speed is a magnitude: sideways/backward movement and involuntary velocity also count if present at the capture point. Teleport distance or a high move-speed stat by itself does not count. Dash momentum counts only if actual velocity remains when the attack transition begins.

**Capture timing matters:** the installed game's `RunState.OnExit` calls `Movement.EndMovement`, which zeros the rigidbody before the new attack enters. A narrowly filtered Harmony prefix on `FSM.ChangeState(string, bool)` captures velocity immediately before that transition. It acts only for this skill on the owning player's matching FSM. The attack consumes a same-frame snapshot and rejects stale snapshots. No cross-frame momentum tracker or global player dictionary is used. Direct state entries without a valid snapshot use their current velocity.

The captured opening bonus remains fixed through the wind-up; changes after entering cannot inflate it. It is not reused on subsequent hits or the next combo. Duplicate execution callbacks do not create extra attacks or compound the multiplier. Player-specific state instances isolate co-op snapshots.

## Native combat integration and visuals

The state derives from the game's `Player.MeleeAttackState` and sets `isBasic`, `isMeleeSkill` and a three-hit maximum. It reuses the native Ice Dagger **melee hitbox/animation object**, stretched into a narrow spear profile and registered with this skill's Air damage stats. The object's `IceDagger` script is switched off before it starts, so no ice shards or break animation play; the sound is the game's `WindSlashBasic`. It does not spawn Ice Dagger projectiles or reuse its player skill state. The game owns collision timing, enemy/destructible targeting, damage/crit handling, basic execute events and combo recovery.

Momentum multiplies this attack instance's existing `Attack.damageModifier`; it does not edit shared skill stats or the player’s movement-speed stat for damage scaling. The native attack movement modifier is removed by the normal state exit.

The original dagger sprites are replaced with a drawn spear shaft/head after the replacement is created. The opening spearhead glows gold with momentum; ordinary hits are cyan. Native visuals remain when the custom material/drawing is unavailable. The owned melee object and its visual children are destroyed on state exit/cancellation. A generated spear icon accompanies the basic arcana.

Native basic-relic behavior is retained by using that pipeline; interactions with specific relics still need gameplay testing. Third-party mods/relics that rewrite native combo limits may change the default three-hit behavior.

## Configuration

BepInEx creates `BepInEx\config\mdbailey94.wol.momentumspear.cfg` on first load.

| Section / key | Default | Bounds / application |
|---|---:|---|
| General / Enabled | true | Offers new copies; already equipped copies remain usable |
| Momentum / BonusPerSpeedUnit | 0.1 | 0–1; opening damage bonus per actual speed unit |
| Momentum / MaximumMultiplier | 2 | 1–4; 1 disables the bonus |
| Spear / ReachScale | 1.8 | 0.5–3; stretches the native hitbox forward |
| Spear / WidthScale | 0.65 | 0.3–1.5; narrows the native hitbox |
| Balance / OpeningDamage | 8 | 1–100; requires restart |
| Balance / SecondDamage | 10 | 1–100; requires restart |
| Balance / FinisherDamage | 16 | 1–100; requires restart |

Momentum values apply to future snapshots; reach/width apply to future attacks. Nonfinite or out-of-range configuration values use defaults or clamps. Nonfinite velocity gives a neutral multiplier; extreme finite velocities are safely capped before float overflow.

## Build and verification

Requires BepInEx, LegendAPI and the game's installed Harmony library. Target: **C# 7.3 / .NET Framework 3.5**. The project has no installation/deployment target and uses no unsafe/private game API access.

From the repository root:

```powershell
& .\tests\momentum-spear\Verify-MomentumSpear.ps1
& .\tests\momentum-spear\Verify-InstalledReferences.ps1
```

The simulation compiles the actual five mod source files against explicit engine/API stubs. It tests numeric boundaries, opening-only damage, pre-stop sampling, the outgoing run-state stop, same-frame and stale snapshots, rejected/self transitions, unrelated FSMs, independent co-op state, combo progression/wrap, duplicate callbacks, damage modifier preservation, enhanced reach, configuration, visual fallback and cleanup.

**The simulation explicitly dispatches the prefix through a stub Harmony/FSM adapter. It does not prove real Harmony detouring or Unity gameplay.** The separate installed-reference check compiles against the actual original game, Unity, BepInEx, LegendAPI and Harmony DLLs, and verifies unchanged source/reference hashes. The pinned-reference build additionally snapshots the current source. Evidence is saved under `tests/momentum-spear` and the staged build directory.

## Installation and gameplay status

Version 0.1.0 is installed in the base game and the Chaos Depths profile (`BepInEx\plugins\WoLMomentumSpear`); building the project copies it into the base game. It has **not been played yet**: buy it in the shop, or use the Debugging save, and equip it as a basic.

Manual checks still needed: shop/basic-slot placement, spear alignment and hitbox reach, three-hit input timing, hold-to-combo, run-to-opening damage, dash/cancel momentum timing, standing still despite speed bonuses, multiple enemies and destructibles, critical/basic-relic interactions, co-op isolation, enhanced reach, cancellation cleanup, icon appearance and overall balance. Verify the real Harmony hook is installed and that a moving opener receives its bonus.
