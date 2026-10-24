using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoLRelicPack
{
    // Storm Battery: every dash stores a charge (up to MaxCharges). The next time one of your
    // arcana hits an enemy, all the charges are spent at once on a chain of lightning bolts that
    // jumps from that enemy to the nearest others around it, one bolt per charge.
    //
    // Modeled on Boots of Celerity (DamageUpWithDash: counts dashes through the player's skill
    // events, stores up to three) and Double Attack (DoubleAttackChance: watches every hit through
    // Health.globalTakeDamageEnterHandlers and deals its extra damage straight to the target with
    // a copy of the hit's AttackInfo). The bolts look like the game's own lightning strikes and
    // lightning chain sparks.
    public class StormBattery : Item
    {
        public static string staticID = RelicPackPlugin.IDPrefix + "StormBattery";

        private const int MaxCharges = 3;            // dashes stored at most
        private const int BoltDamage = 10;           // damage of each bolt (the game's Lightning Aura strikes for 10)
        private const float BoltShockChance = 0.5f;  // chance each bolt shocks (0 to 1)
        private const int BoltShockLevel = 1;        // shock strength, like a basic lightning hit
        private const float ChainRange = 5f;         // how far a bolt can jump to the next enemy (world units, ~tiles)
        private const float FirstBoltDelay = 0.05f;  // seconds after the arcana hit before the first bolt
        private const float JumpDelay = 0.08f;       // seconds between bolts in the chain
        private const float BoltHitStun = 0.5f;      // hit stun of each bolt, relative to a normal hit

        private int charges;
        private string attackTag = string.Empty;

        // Public here only because the reference DLL is publicized (the game's own is protected).
        public override string ExtraInfo => $"{charges}/{MaxCharges}";

        public StormBattery()
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
            charges = 0;
            Player player = parentPlayer;
            player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Remove(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
            Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Remove(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
            if (givenStatus)
            {
                player.skillEnterEventHandlers = (Player.SkillEventHandler)Delegate.Combine(player.skillEnterEventHandlers, new Player.SkillEventHandler(OnSkillEnter));
                Health.globalTakeDamageEnterHandlers = (Health.GlobalOnTakeDamageHandler)Delegate.Combine(Health.globalTakeDamageEnterHandlers, new Health.GlobalOnTakeDamageHandler(OnAnyHit));
                UpdateItemBar(ItemStatusBar.ItemState.Disabled);
                RelicPackPlugin.Log($"Storm Battery active on player {player.playerID + 1}");
            }
            else
            {
                RemoveFromItemBar();
            }
        }

        // Any dash arcana starting stores a charge.
        private void OnSkillEnter(Player.SkillState givenSkill)
        {
            try
            {
                if (givenSkill == null || !givenSkill.isDash || charges >= MaxCharges)
                    return;
                charges++;
                UpdateItemBar(charges >= MaxCharges ? ItemStatusBar.ItemState.Active : ItemStatusBar.ItemState.Ready);
                RelicPackPlugin.Log($"Storm Battery: dash stored a charge ({charges}/{MaxCharges})");
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Storm Battery dash", e);
            }
        }

        private void OnAnyHit(AttackInfo givenInfo, Entity atkEntity, Entity hurtEntity)
        {
            try
            {
                if (charges <= 0 || parentPlayer == null || atkEntity != parentPlayer || !(hurtEntity is Enemy))
                    return;
                if (!RelicFx.IsArcanaHit(parentPlayer, givenInfo))
                    return;
                int bolts = charges;
                charges = 0;
                UpdateItemBar(ItemStatusBar.ItemState.Disabled);
                RelicPackPlugin.Log($"Storm Battery: {givenInfo.skillID} hit {hurtEntity.name}, spending {bolts} charge(s) on chain lightning");
                parentPlayer.StartCoroutine(Chain(hurtEntity, new AttackInfo(givenInfo), bolts));
            }
            catch (Exception e)
            {
                RelicPackPlugin.LogErrorOnce("Storm Battery hit", e);
            }
        }

        // One bolt per charge, each jumping from where the last struck to the nearest enemy not yet
        // struck within ChainRange. With nobody new in reach (a lone enemy or a boss) the bolt
        // strikes the same enemy again, so the charges are never wasted.
        private IEnumerator Chain(Entity first, AttackInfo source, int bolts)
        {
            yield return new WaitForSeconds(FirstBoltDelay);
            var struck = new List<Entity>();
            Entity current = first;
            Vector2 from = RelicFx.IsAlive(first) ? RelicFx.Center(first) : (Vector2)parentPlayer.transform.position;
            int landed = 0;
            for (int i = 0; i < bolts; i++)
            {
                Entity target = null;
                try
                {
                    target = Nearest(from, struck, current) ?? (RelicFx.IsAlive(current) ? current : null);
                    if (target != null)
                    {
                        Vector2 to = RelicFx.Center(target);
                        Zap(target, source, from, to);
                        struck.Add(target);
                        landed++;
                        from = to;
                        current = target;
                    }
                }
                catch (Exception e)
                {
                    RelicPackPlugin.LogErrorOnce("Storm Battery chain", e);
                    yield break;
                }
                if (target == null)
                    break;
                yield return new WaitForSeconds(JumpDelay);
            }
            RelicPackPlugin.Log($"Storm Battery: chain lightning landed {landed} of {bolts} bolt(s) " +
                $"on {CountDistinct(struck)} enemy(ies), {BoltDamage} damage each before crits/resistances");
        }

        // The nearest living enemy within ChainRange of a point that hasn't been struck yet (the
        // first jump also leaves out the enemy the arcana hit).
        private Entity Nearest(Vector2 point, List<Entity> struck, Entity exclude)
        {
            if (GameController.enemies == null)
                return null;
            Entity best = null;
            float bestDistance = ChainRange * ChainRange;
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null)
                    continue;
                Entity enemy = go.GetComponent<Entity>();
                if (enemy == null || enemy == exclude || struck.Contains(enemy) || !RelicFx.IsAlive(enemy))
                    continue;
                float distance = (RelicFx.Center(enemy) - point).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = enemy;
                }
            }
            return best;
        }

        // A bolt: the arcana hit's AttackInfo (so the wizard's crit chance and damage-number style
        // carry over) turned into a plain lightning hit with this relic's ID and damage.
        private void Zap(Entity target, AttackInfo source, Vector2 from, Vector2 to)
        {
            var bolt = new AttackInfo(source)
            {
                skillID = staticID,
                attacker = attackTag,
                damage = BoltDamage,
                isCritical = false,
                isUltimate = false,
                isDamageOverride = false,
                elementType = ElementType.Lightning,
                subElementType = ElementType.Lightning,
                knockbackVector = Vector2.zero,
                knockbackMultiplier = 0f,
                knockbackOverwrite = false,
                hitStunDurationModifier = BoltHitStun,
                attackerPosition = from,
                targetPosition = to,
                burnChance = 0f,
                slowChance = 0f,
                poisonChance = 0f,
                freezeChance = 0f,
                rootChance = 0f,
                chaosChance = 0f,
                shockChance = BoltShockChance,
                shockLevel = BoltShockLevel,
                showDamageNumber = true,
                showDamageEffect = true
            };
            bolt.attackInfoKey = bolt.skillCategory + bolt.skillID + bolt.skillLevel;
            RelicFx.Arc(from, to);
            RelicFx.Bolt(to, 0.8f);
            target.health.TakeDamage(bolt, parentEntity);
        }

        private static int CountDistinct(List<Entity> struck)
        {
            var distinct = new List<Entity>();
            foreach (Entity e in struck)
                if (!distinct.Contains(e))
                    distinct.Add(e);
            return distinct.Count;
        }
    }
}
