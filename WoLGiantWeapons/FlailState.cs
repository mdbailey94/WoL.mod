using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLGiantWeapons
{
    // Giant Lightning Flail (#84, numbers in Log - Modder 2026-11-03 12:44, approved by Matt): a
    // slow 3-hit basic on the game's melee combo, with no attack object of its own
    // (skipAtkObjSpawn). Hits 1-2 swing the flail round in a full circle; hit 3 winds up overhead
    // and slams the head down in front. Enhanced: the slam arcs lightning to nearby enemies.
    // Levels: 1 a circle swing, 2 the slam, 3 an enhanced arc.
    public sealed class FlailState : Player.MeleeAttackState
    {
        public new static string staticID = "GiantLightningFlailBasic";

        private NumVarStatMod paceMod;
        private bool paced, executed;

        public FlailState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.05f;
            animExecTime = 0.25f;
            baseCancelThreshold = 0.5f;
            finalCancelThreshold = 0.8f;
            baseRunThreshold = 0.6f;
            finalRunThreshold = 0.9f;
            baseExitThreshold = 0.75f;
            finalExitThreshold = 1f;
            alwaysStartForehand = true;
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0.3f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0f, 10, VarStatModType.OverrideWithMods);
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic", "WindSlashBasic" };
        }

        public override void OnEnter()
        {
            base.OnEnter();
            executed = false;
            Heavy.Pace(parent, ref paceMod, "GiantFlail_Pace", GiantWeaponsPlugin.FlailPace, ref paced, true);
        }

        public override void OnExit()
        {
            Heavy.Pace(parent, ref paceMod, "GiantFlail_Pace", GiantWeaponsPlugin.FlailPace, ref paced, false);
            base.OnExit();
        }

        // The circle swings in the arms-out spin pose; the slam in the ground-slam pose.
        public override void PlayAnimation(int attackCount)
        {
            parent.anim.PlayDirectional(attackCount >= maxComboCount ? parent.GSlamAnimStr : parent.PBAoEAnimStr, -1, animStartTime);
        }

        // Once per hit, as Momentum Spear.
        public override void ExecuteAttack()
        {
            if (executed)
                return;
            executed = true;
            base.ExecuteAttack();
            if (comboCounter < maxComboCount)
                Circle();
            else
                Slam(Heavy.Hand(parent), Heavy.Aim(this, parent));
        }

        // Hits 1-2: everything within CircleRadius round the wizard.
        private void Circle()
        {
            Vector2 at = parent.transform.position;
            Heavy.Bolt(at, parent, skillID, 1, GiantWeaponsPlugin.CircleRadius);
            Heavy.Sound("WindSlashBasic", at, 0.55f);
            GiantWeaponsPlugin.Log($"Giant Lightning Flail: circle swing {comboCounter}");
        }

        // Hit 3: SlamWindUp later, the head smashes down SlamDistance ahead.
        private void Slam(Vector2 hand, Vector2 aim)
        {
            Player p = parent;
            string id = skillID;
            bool enhanced = IsEmpowered;
            Vector2 spot = hand + aim * GiantWeaponsPlugin.SlamDistance;
            Heavy.Sound("WindSlashBasic", hand, 0.5f);
            Heavy.After(GiantWeaponsPlugin.SlamWindUp, () =>
            {
                if (p == null)
                    return;
                Heavy.Bolt(spot, p, id, 2, GiantWeaponsPlugin.SlamRadius);
                Heavy.Shake(1.2f);
                Heavy.Sound("ThunderClap", spot, 0.8f);
                Heavy.Sound("EarthExplosion", spot, 0.7f);
                Heavy.Dust(spot, 14, GiantWeaponsPlugin.SlamRadius);
                int arcs = 0;
                if (enhanced)
                    foreach (GameObject enemy in Heavy.Near(spot, GiantWeaponsPlugin.ArcReach))
                    {
                        if (arcs >= GiantWeaponsPlugin.ArcTargets)
                            break;
                        if (((Vector2)enemy.transform.position - spot).sqrMagnitude <= GiantWeaponsPlugin.SlamRadius * GiantWeaponsPlugin.SlamRadius)
                            continue;
                        Heavy.Bolt(enemy.transform.position, p, id, 3, 0.4f);
                        arcs++;
                    }
                GiantWeaponsPlugin.Log($"Giant Lightning Flail: slam {GiantWeaponsPlugin.SlamDistance:0.0} u out{(enhanced ? $", {arcs} arcs" : "")}");
            });
        }
    }
}
