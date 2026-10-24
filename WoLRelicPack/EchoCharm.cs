using System;
using System.Collections;
using UnityEngine;

namespace WoLRelicPack
{
    // Echo Charm: casting your signature arcana makes it echo. Every enemy the cast hits (within
    // EchoWindow of casting, up to MaxEchoesPerCast hits) is struck again EchoDelay later by a
    // copy of that hit at EchoDamage of its power, with a flash of the arcana's element on it.
    //
    // Why an echo of the hits rather than casting the arcana again: re-entering the skill state
    // would take over the wizard (their aim, movement and animation, and the arcana's cooldown and
    // overdrive), break any arcana that's still running, and behave differently for every one of
    // the game's arcana. Repeating each hit works the same for all of them, lands where the cast
    // actually landed, and cleans up with nothing left running.
    //
    // Modeled on Double Attack (DoubleAttackChance: copies a hit's AttackInfo from
    // Health.globalTakeDamageEnterHandlers and deals it again a moment later at a fraction of the
    // damage) and the game's signature relics (Player.SkillState.isSignature, the arcana in the
    // signature slot).
    public class EchoCharm : Item
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "EchoCharm";

        private const float EchoDamage = 0.4f;       // echo power, as a fraction of the original hit
        private const float EchoDelay = 0.5f;        // seconds between a hit and its echo
        private const float EchoWindow = 2.5f;       // seconds after casting during which hits get echoed
        private const int MaxEchoesPerCast = 12;     // most hits one cast can echo (multi-hit arcana)
        private const float EchoHitStun = 0.5f;      // hit stun of an echo, relative to the original

        private string attackTag = string.Empty;
        private string castSkillID = string.Empty;
        private float castTime = -100f;
        private int echoesThisCast;
        private int echoesLanded;

        // Public here only because the reference DLL is publicized (the game's own is protected).
        public override string ExtraInfo => PercentToStr(EchoDamage, string.Empty);

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

        private void SetEventHandlers(bool givenStatus)
        {
            if (!SetParentAsPlayer())
                return;
            attackTag = staticID + parentPlayer.playerID;
            castSkillID = string.Empty;
            Player player = parentPlayer;
            player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Remove(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
            Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Remove(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
            if (givenStatus)
            {
                player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Combine(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
                Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Combine(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
                Player.SkillState signature = player.GetSignatureSkill();
                RelicPackPlugin.Log($"Echo Charm active on player {player.playerID + 1} (signature arcana: {(signature != null ? signature.skillID : "none")})");
            }
        }

        // Casting the signature arcana opens the echo window.
        private void OnSkillEnter(Player.SkillState givenSkill)
        {
            try
            {
                if (givenSkill == null || !givenSkill.isSignature || givenSkill.isBasic || givenSkill.isDash)
                    return;
                castSkillID = givenSkill.skillID;
                castTime = Time.time;
                echoesThisCast = 0;
                echoesLanded = 0;
                RelicPackPlugin.LogRateLimited("EchoCast", 1f, $"Echo Charm: signature {castSkillID} cast, echoing its hits for {EchoWindow}s");
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm cast", e);
            }
        }

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
                if (Time.time - castTime > EchoWindow || echoesThisCast >= MaxEchoesPerCast)
                    return;
                echoesThisCast++;
                parentPlayer.StartCoroutine(Echo(new AttackInfo(givenInfo), hurtEntity, castSkillID));
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm hit", e);
            }
        }

        private IEnumerator Echo(AttackInfo original, Entity target, string skillID)
        {
            yield return new WaitForSeconds(EchoDelay);
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
                    damage = Mathf.Max(1, Mathf.CeilToInt(baseDamage * EchoDamage)),
                    isCritical = false,
                    isUltimate = false,
                    isDamageOverride = false,
                    knockbackVector = Vector2.zero,
                    knockbackMultiplier = 0f,
                    knockbackOverwrite = false,
                    hitStunDurationModifier = original.hitStunDurationModifier * EchoHitStun,
                    // Status effects echo at the same reduced strength.
                    burnChance = original.burnChance * EchoDamage,
                    slowChance = original.slowChance * EchoDamage,
                    poisonChance = original.poisonChance * EchoDamage,
                    shockChance = original.shockChance * EchoDamage,
                    freezeChance = original.freezeChance * EchoDamage,
                    rootChance = original.rootChance * EchoDamage,
                    chaosChance = original.chaosChance * EchoDamage
                };
                Vector2 spot = RelicFx.Center(target);
                echo.attackerPosition = spot;
                echo.targetPosition = spot;
                RelicFx.ElementFlash(original.elementType, original.subElementType, spot);
                target.health.TakeDamage(echo, parentEntity);
                echoesLanded++;
                RelicPackPlugin.LogRateLimited("EchoHit", 0.5f,
                    $"Echo Charm: echoed {skillID} on {target.name} for {echo.damage} ({EchoDamage * 100f:0}% of {Mathf.RoundToInt(baseDamage)}), " +
                    $"{echoesLanded} echo(es) this cast");
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Echo Charm echo", e);
            }
        }
    }
}
