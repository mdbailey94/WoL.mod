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
        // 0.11.0 (#106): hit 3 can be CHARGED: hold to whirl the ball overhead, let go to slam.
        private bool charging, releasedSinceHit2 = true;
        private float chargeFrom, charge;
        private FlailLook.Fx whirl;

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
            charge = 0f;
            Heavy.Pace(parent, ref paceMod, "GiantFlail_Pace", GiantWeaponsPlugin.FlailPace, ref paced, true);
            if (comboCounter == maxComboCount - 1)
                releasedSinceHit2 = false;
            // A fresh hold as the third hit starts (let go after hit 2, then hold): charge.
            charging = comboCounter >= maxComboCount && ButtonHeld() && releasedSinceHit2;
            chargeFrom = Time.time;
            if (charging)
                whirl = FlailLook.Whirl(parent);
        }

        public override void OnExit()
        {
            Heavy.Pace(parent, ref paceMod, "GiantFlail_Pace", GiantWeaponsPlugin.FlailPace, ref paced, false);
            charging = false;
            StopWhirl();
            base.OnExit();
        }

        private void StopWhirl()
        {
            if (whirl != null)
                whirl.Stop();
            whirl = null;
        }

        public override void Update()
        {
            if (!ButtonHeld())
                releasedSinceHit2 = true;
            if (charging)
            {
                charge = Time.time - chargeFrom;
                float full = Mathf.Clamp01(charge / GiantWeaponsPlugin.SlamFullCharge);
                if (whirl != null)
                    whirl.charge = full;
                PlayAnimation(comboCounter);
                parent.movement.EndMovement();
                if (ButtonHeld() && charge < GiantWeaponsPlugin.SlamFullCharge + 0.5f)
                    return;
                charging = false;
            }
            base.Update();
        }

        private bool ButtonHeld()
        {
            try
            {
                return parent.inputDevice != null && parent.inputDevice.GetButton("Skill" + skillSlot);
            }
            catch
            {
                return false;
            }
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

        // Hits 1-2: the ball goes once round the wizard at CircleRadius over CircleTime (hit 1
        // counterclockwise, hit 2 clockwise), starting at the aim; 0.5.0 (#92): the hit follows
        // the ball, the circle cut into CircleSlices slices hit as the ball passes (once each).
        private void Circle()
        {
            Player p = parent;
            string id = skillID;
            Vector2 aim = Heavy.Aim(this, p);
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            bool clockwise = comboCounter % 2 == 0;
            float time = GiantWeaponsPlugin.CircleTime, radius = GiantWeaponsPlugin.CircleRadius;
            int slices = GiantWeaponsPlugin.CircleSlices;
            float slice = 360f / slices;
            var done = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < slices; i++)
            {
                float centre = (aimDeg + (clockwise ? -1f : 1f) * (i + 0.5f) * slice) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(centre), Mathf.Sin(centre));
                Heavy.After((i + 1) * time / slices, () =>
                {
                    if (!Heavy.Alive(p))
                        return;
                    foreach (GameObject enemy in Heavy.InArc(p.transform.position, dir, radius, slice * 0.5f + 1f))
                        if (done.Add(enemy.GetInstanceID()))
                            Heavy.Bolt(Heavy.Body(enemy), p, id, 1, Heavy.OnBodyRadius);   // on the body (0.9.0); the done set stops a second hit this swing
                    foreach (Vector2 prop in Heavy.PropsInArc(p.transform.position, dir, radius, slice * 0.5f + 1f))
                        Heavy.Bolt(prop, p, id, 1, 0.12f);
                });
            }
            FlailLook.Circle(p, radius, time, aimDeg, clockwise);
            Heavy.Sound("WindSlashBasic", p.transform.position, 0.5f);
            GiantWeaponsPlugin.Log($"Giant Lightning Flail: circle swing {comboCounter} ({(clockwise ? "clockwise" : "counterclockwise")}, {time:0.00} s)");
        }

        // Hit 3: SlamWindUp later, the head smashes down SlamDistance ahead.
        private void Slam(Vector2 hand, Vector2 aim)
        {
            Player p = parent;
            string id = skillID;
            bool enhanced = IsEmpowered;
            Vector2 spot = hand + aim * GiantWeaponsPlugin.SlamDistance;
            // 0.11.0 (#106): a charged slam (held past SlamMinCharge) drops straight from the whirl:
            // wider and harder with the charge, level 4 at full; a tap slam is the overhead swing.
            bool charged = charge >= GiantWeaponsPlugin.SlamMinCharge;
            float full = charged ? Mathf.Clamp01(charge / GiantWeaponsPlugin.SlamFullCharge) : 0f;
            float radius = charged ? Mathf.Lerp(GiantWeaponsPlugin.SlamRadius, GiantWeaponsPlugin.FullSlamRadius, full) : GiantWeaponsPlugin.SlamRadius;
            int level = full >= 1f ? 4 : 2;
            float windUp = charged ? GiantWeaponsPlugin.ChargedDrop : GiantWeaponsPlugin.SlamWindUp;
            StopWhirl();
            Heavy.Sound("WindSlashBasic", hand, 0.5f);
            FlailLook.Reach(p, spot, windUp, charged);
            Heavy.After(windUp, () =>
            {
                if (!Heavy.Alive(p))
                    return;
                Heavy.Bolt(spot, p, id, level, radius);
                FlailLook.Impact(p, spot, radius);
                Heavy.Shake(1.2f + 0.8f * full);
                Heavy.Sound("ThunderClap", spot, 0.8f);
                Heavy.Sound("EarthExplosion", spot, 0.7f);
                Heavy.Dust(spot, 14, GiantWeaponsPlugin.SlamRadius);
                int arcs = 0;
                if (enhanced)
                {
                    // 0.7.0 (#101): the NEAREST enemies outside the slam, not the first in the list.
                    var near = Heavy.Near(spot, GiantWeaponsPlugin.ArcReach);
                    near.Sort((a, b) => (Heavy.Body(a) - spot).sqrMagnitude.CompareTo((Heavy.Body(b) - spot).sqrMagnitude));
                    foreach (GameObject enemy in near)
                    {
                        if (arcs >= GiantWeaponsPlugin.ArcTargets)
                            break;
                        if ((Heavy.Body(enemy) - spot).sqrMagnitude <= GiantWeaponsPlugin.SlamRadius * GiantWeaponsPlugin.SlamRadius)
                            continue;
                        Heavy.Bolt(Heavy.Body(enemy), p, id, 3, 0.4f);
                        arcs++;
                    }
                }
                GiantWeaponsPlugin.Log($"Giant Lightning Flail: slam {GiantWeaponsPlugin.SlamDistance:0.0} u out{(enhanced ? $", {arcs} arcs" : "")}");
            });
        }
    }
}
