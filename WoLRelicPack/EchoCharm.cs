using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLRelicPack
{
    // Echo Charm: when your signature arcana finishes, it is cast once more by itself (the echo) at
    // EchoPower of its damage, with no cooldown and no overdrive spent on it.
    //
    // How the echo is cast: the wizard's FSM re-enters the arcana's own skill state
    // (fsm.QueueChangeState(skillID), the way the overdrive cast enters the signature and Chaos
    // Mirror re-enters the last arcana), so the echo is the real arcana: it reads the current
    // aim/movement input, plays the animation, and can be dash-cancelled like any cast. The queued
    // change runs in the player's next Update (FSM.AnyStateUpdate); changing state from inside the
    // cast's OnExit would re-enter the FSM while it's mid-transition.
    //
    // Before the echo enters, the skill's disableStartCooldown is set (BaseOnEnter/OnExit then skip
    // StartCooldownTimer; a Harmony prefix catches the arcana that start the timer themselves),
    // isUltimate is cleared (the plain arcana, not its overdrive variant) and a named multiplicative
    // damage mod is put on the skill's StatData: AttackInfo.GetInfoFromData reads "damage" through
    // those mods when an attack is created, so every hit the echo makes is scaled. All of it is
    // undone when the echo state exits (naturally, by a dash cancel, by being hurt or killed), by a
    // watchdog coroutine if the state never got entered, on level load and when the relic is dropped.
    //
    // Charge arcana (cooldownRef.chargeCount: several uses stored up, each cast spending one, and
    // the skill itself pausing/restarting the recharge) can't be given a clean free recast, so for
    // them the charm falls back to echoing each hit instead (the 0.1.x behaviour: every enemy the
    // cast hits is struck again HitEchoDelay later at EchoPower of that hit).
    public class EchoCharm : Item
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "EchoCharm";

        // Echo power, as a fraction of the arcana's normal damage: the recast's damage mod, and the
        // strength of each hit echo for charge arcana. (0.2.0: a real recast at 50%; was a 30%
        // echo of every hit.)
        public const float EchoPower = 0.5f;
        // How long the echo waits for a dash/basic the signature was cancelled into (and whatever
        // that chains into) to finish before it gives up.
        private const float EchoWaitWindow = 2f;
        // Hit-echo fallback (charge arcana only).
        private const float HitEchoDelay = 0.5f;      // seconds between a hit and its echo
        private const float HitEchoWindow = 2.5f;     // seconds after casting during which hits get echoed
        private const int MaxHitEchoesPerCast = 12;   // most hits one cast can echo (multi-hit arcana)
        private const float HitEchoStun = 0.5f;       // hit stun of a hit echo, relative to the original

        // The damage mod's name on the skill's StatData (removed by this ID).
        private static readonly string EchoModID = staticID + "Echo";
        // States the echo won't fire into (the list Player.HandleOverdrive checks before it casts
        // the signature, plus the parry); any Hurt state counts too.
        private static readonly string[] NoEchoStates = { "Dead", "Fall", "Frozen", "Paused", "TeleToLoc", "Parry" };
        // Skill states currently running as an echo (for the cooldown hook).
        private static readonly HashSet<Player.SkillState> echoingSkills = new HashSet<Player.SkillState>();

        private readonly NumVarStatMod echoDamageMod = new NumVarStatMod(EchoModID, EchoPower - 1f, 10, VarStatModType.Multiplicative);

        // The signature cast that just ended and is owed an echo (waiting for a chained dash/basic).
        private Player.SkillState pendingSkill;
        private float pendingSince;
        // The echo in progress.
        private Player.SkillState echoSkill;
        private bool echoEntered;
        private bool savedDisableStartCooldown;

        // Hit-echo fallback state.
        private string attackTag = string.Empty;
        private string castSkillID = string.Empty;
        private float castTime = -100f;
        private int echoesThisCast;
        private int echoesLanded;

        // Public here only because the reference DLL is publicized (the game's own is protected).
        public override string ExtraInfo => PercentToStr(EchoPower, string.Empty);

        public EchoCharm()
        {
            ID = staticID;
            category = Category.Offense;
        }

        public override void Activate()
        {
            SetEventHandlers(true);
        }

        public override void Deactivate()
        {
            SetEventHandlers(false);
        }

        // For the cooldown hook: is this skill state running as an echo right now?
        public static bool IsEchoing(Player.SkillState skill) => skill != null && echoingSkills.Contains(skill);

        private void SetEventHandlers(bool givenStatus)
        {
            if (!SetParentAsPlayer())
                return;
            attackTag = staticID + parentPlayer.playerID;
            castSkillID = string.Empty;
            pendingSkill = null;
            EndEcho("relic removed");
            Player player = parentPlayer;
            player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Remove(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
            player.skillExitEventHandlers = (Player.SkillEventHandler)Delegate.Remove(player.skillExitEventHandlers, new Player.SkillEventHandler(OnSkillExit));
            Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Remove(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
            GameController.levelLoadEventHandlers = (GameController.LevelLoadEventHandler)Delegate.Remove(GameController.levelLoadEventHandlers, new GameController.LevelLoadEventHandler(OnLevelLoad));
            if (givenStatus)
            {
                player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Combine(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
                player.skillExitEventHandlers = (Player.SkillEventHandler)Delegate.Combine(player.skillExitEventHandlers, new Player.SkillEventHandler(OnSkillExit));
                Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Combine(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
                GameController.levelLoadEventHandlers = (GameController.LevelLoadEventHandler)Delegate.Combine(GameController.levelLoadEventHandlers, new GameController.LevelLoadEventHandler(OnLevelLoad));
                Player.SkillState signature = player.GetSignatureSkill();
                RelicPackPlugin.Log($"Echo Charm active on player {player.playerID + 1} (signature arcana: {(signature != null ? signature.skillID : "none")}" +
                    $"{(signature != null && UsesHitEcho(signature) ? ", a charge arcana: its hits will be echoed instead of recasting it" : string.Empty)})");
            }
        }

        private static bool IsSignatureCast(Player.SkillState skill) =>
            skill != null && skill.isSignature && !skill.isBasic && !skill.isDash;

        // Charge arcana keep their own charge/recharge bookkeeping inside the skill state: fall back.
        private static bool UsesHitEcho(Player.SkillState skill) => skill != null && skill.isChargeSkill;

        // ---- The recast ----

        private void OnSkillEnter(Player.SkillState givenSkill)
        {
            try
            {
                if (givenSkill == null)
                    return;
                if (echoSkill != null && givenSkill == echoSkill)
                {
                    // The echo is starting. BaseOnEnter checks disableStartCooldown right after this
                    // handler, so set it again here in case the skill's own OnEnter reset it.
                    echoEntered = true;
                    givenSkill.disableStartCooldown = true;
                    givenSkill.isUltimate = false;
                    RelicPackPlugin.Log($"Echo Charm: echoing {givenSkill.skillID} at {EchoPower * 100f:0}% power");
                    return;
                }
                if (!IsSignatureCast(givenSkill))
                    return;
                // A fresh real cast: whatever echo was still waiting is dropped (this cast gets its own).
                pendingSkill = null;
                if (UsesHitEcho(givenSkill))
                {
                    castSkillID = givenSkill.skillID;
                    castTime = Time.time;
                    echoesThisCast = 0;
                    echoesLanded = 0;
                    RelicPackPlugin.LogRateLimited("EchoFallback", 1f,
                        $"Echo Charm: {castSkillID} is a charge arcana, so its hits are echoed at {EchoPower * 100f:0}% instead of recasting it");
                }
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm cast", e);
            }
        }

        // Runs inside the skill state's OnExit: fsm.nextState is where the wizard is going.
        private void OnSkillExit(Player.SkillState givenSkill)
        {
            try
            {
                if (givenSkill == null || parentPlayer == null)
                    return;
                if (echoSkill != null)
                {
                    if (givenSkill == echoSkill)
                        EndEcho(string.Empty);
                    return;
                }
                if (IsSignatureCast(givenSkill) && !UsesHitEcho(givenSkill))
                {
                    pendingSkill = givenSkill;
                    pendingSince = Time.time;
                }
                if (pendingSkill == null)
                    return;
                FSM fsm = parentPlayer.fsm;
                IState next = fsm != null ? fsm.nextState : null;
                // Cancelled into a dash, basic or other arcana: the echo waits for that to end.
                if (next is Player.SkillState)
                    return;
                Player.SkillState skill = pendingSkill;
                pendingSkill = null;
                string nextName = next != null ? next.name : string.Empty;
                string reason;
                if (Time.time - pendingSince > EchoWaitWindow)
                    reason = $"the cast was cancelled and the chain took over {EchoWaitWindow}s";
                else
                    CanEcho(skill, nextName, out reason);
                if (reason != null)
                {
                    RelicPackPlugin.LogRateLimited("EchoSkip", 1f, $"Echo Charm: no echo of {skill.skillID} ({reason})");
                    return;
                }
                StartEcho(skill);
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm exit", e);
            }
        }

        private bool CanEcho(Player.SkillState skill, string nextStateName, out string reason)
        {
            reason = null;
            Player player = parentPlayer;
            if (player == null || player.gameObject == null || !player.gameObject.activeInHierarchy)
                reason = "wizard gone";
            else if (player.health == null || player.health.CurrentHealthValue <= 0)
                reason = "wizard dead";
            else if (LoadingScreen.loading)
                reason = "level loading";
            else if (TimeScaleController.paused)
                reason = "game paused";
            else if (nextStateName.Contains("Hurt") || Array.IndexOf(NoEchoStates, nextStateName) >= 0)
                reason = $"wizard went to {nextStateName}";
            else if (player.fsm == null || !player.fsm.HasState(skill.skillID))
                reason = "skill state missing";
            else if (player.GetSignatureSkill() != skill)
                reason = "no longer the signature arcana";
            else if (skill.skillData == null)
                reason = "skill has no stat data";
            return reason == null;
        }

        private void StartEcho(Player.SkillState skill)
        {
            echoSkill = skill;
            echoEntered = false;
            echoingSkills.Add(skill);
            savedDisableStartCooldown = skill.disableStartCooldown;
            skill.disableStartCooldown = true;
            skill.isUltimate = false;
            skill.skillData.AddMod(StatData.damageStr, echoDamageMod);
            parentPlayer.fsm.QueueChangeState(skill.skillID);
            parentPlayer.StartCoroutine(WatchEcho(skill));
        }

        // Undo everything the echo set up. Safe to call when no echo is running.
        private void EndEcho(string reason)
        {
            Player.SkillState skill = echoSkill;
            echoSkill = null;
            if (skill == null)
                return;
            echoingSkills.Remove(skill);
            try
            {
                if (skill.skillData != null)
                    skill.skillData.RemoveMod(StatData.damageStr, EchoModID);
                skill.disableStartCooldown = savedDisableStartCooldown;
            }
            finally
            {
                RelicPackPlugin.Log($"Echo Charm: echo of {skill.skillID} ended" + (string.IsNullOrEmpty(reason) ? string.Empty : $" ({reason})"));
            }
        }

        // Safety net: the queued state change could be overridden (a hurt, a death, the overdrive
        // queueing its own cast) so the echo state never gets entered, or the player could vanish
        // mid-echo. Either way the mod and flags must not stay on the skill.
        private IEnumerator WatchEcho(Player.SkillState skill)
        {
            // The queued change happens in the player's next Update.
            yield return null;
            yield return null;
            while (echoSkill == skill)
            {
                if (!echoEntered)
                {
                    EndEcho("the echo never started");
                    break;
                }
                if (parentPlayer == null || parentPlayer.fsm == null || parentPlayer.fsm.currentStateName != skill.skillID || LoadingScreen.loading)
                {
                    EndEcho("watchdog");
                    break;
                }
                yield return null;
            }
        }

        private void OnLevelLoad(string nextLevel, string prevLevelName)
        {
            try
            {
                pendingSkill = null;
                castSkillID = string.Empty;
                EndEcho("level loading");
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm level load", e);
            }
        }

        // ---- Hit-echo fallback (charge arcana) ----

        private void OnAnyHit(AttackInfo givenInfo, Entity atkEntity, Entity hurtEntity)
        {
            try
            {
                if (castSkillID.Length == 0 || parentPlayer == null || atkEntity != parentPlayer || !(hurtEntity is Enemy))
                    return;
                if (givenInfo == null || givenInfo.isStatusEffect || RelicFx.IsOurs(givenInfo) || string.IsNullOrEmpty(givenInfo.skillID))
                    return;
                // The arcana's own hits (its ID, or a part of it named after it).
                if (!givenInfo.skillID.StartsWith(castSkillID))
                    return;
                // ...but not another of the wizard's arcana that just shares the start of its name
                // (Earth Axe's basic version, say).
                if (givenInfo.skillID != castSkillID && parentPlayer.skillsDict != null && parentPlayer.skillsDict.ContainsKey(givenInfo.skillID))
                    return;
                if (Time.time - castTime > HitEchoWindow || echoesThisCast >= MaxHitEchoesPerCast)
                    return;
                echoesThisCast++;
                parentPlayer.StartCoroutine(HitEcho(new AttackInfo(givenInfo), hurtEntity, castSkillID));
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm hit", e);
            }
        }

        private IEnumerator HitEcho(AttackInfo original, Entity target, string skillID)
        {
            yield return new WaitForSeconds(HitEchoDelay);
            try
            {
                if (!RelicFx.IsAlive(target))
                    yield break;
                // Echo the hit before any crit (the echo rolls its own).
                float baseDamage = original.isCritical && original.critDmgModifier > 1f
                    ? original.damage / original.critDmgModifier
                    : original.damage;
                var echo = new AttackInfo(original)
                {
                    attacker = attackTag,
                    damage = Mathf.Max(1, Mathf.CeilToInt(baseDamage * EchoPower)),
                    isCritical = false,
                    isUltimate = false,
                    isDamageOverride = false,
                    knockbackVector = Vector2.zero,
                    knockbackMultiplier = 0f,
                    knockbackOverwrite = false,
                    hitStunDurationModifier = original.hitStunDurationModifier * HitEchoStun,
                    // Status effects echo at the same reduced strength.
                    burnChance = original.burnChance * EchoPower,
                    slowChance = original.slowChance * EchoPower,
                    poisonChance = original.poisonChance * EchoPower,
                    shockChance = original.shockChance * EchoPower,
                    freezeChance = original.freezeChance * EchoPower,
                    rootChance = original.rootChance * EchoPower,
                    chaosChance = original.chaosChance * EchoPower
                };
                Vector2 spot = RelicFx.Center(target);
                echo.attackerPosition = spot;
                echo.targetPosition = spot;
                RelicFx.ElementFlash(original.elementType, original.subElementType, spot);
                target.health.TakeDamage(echo, parentEntity);
                echoesLanded++;
                RelicPackPlugin.LogRateLimited("EchoHit", 0.5f,
                    $"Echo Charm: echoed {skillID} on {target.name} for {echo.damage} ({EchoPower * 100f:0}% of {Mathf.RoundToInt(baseDamage)}), " +
                    $"{echoesLanded} echo(es) this cast");
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm echo", e);
            }
        }

        // The echo is free: while a skill state runs as an echo its cooldown timer is never started.
        // BaseOnEnter/OnExit already skip it (disableStartCooldown), but many arcana call
        // StartCooldownTimer themselves from ExecuteSkill or OnExit; this catches those. Overrides of
        // StartCooldownTimer (there is one) call the base, so they're covered too.
        [HarmonyPatch(typeof(Player.SkillState), nameof(Player.SkillState.StartCooldownTimer))]
        internal static class NoCooldownPatch
        {
            private static bool Prefix(Player.SkillState __instance) => !IsEchoing(__instance);
        }
    }
}
