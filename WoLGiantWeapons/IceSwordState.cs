using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLGiantWeapons
{
    // Giant Ice Sword (#83, numbers in Log - Modder 2026-11-03 12:44, approved by Matt): a slow
    // 3-hit greatsword basic on the game's melee combo, with no attack object of its own
    // (skipAtkObjSpawn). 0.4.0 (#93, Matt): hit 1 a 45-degree swing clockwise, hit 2 the same
    // counterclockwise, hit 3 a 360-degree spin; held as hit 3 starts, it charges first (the
    // wizard glows ice blue) for a wider, harder spin. Levels: 1 a swing, 2 a tapped spin, 3 a
    // charged spin, 4 a full-charge spin, 5 the enhanced frost ring.
    public sealed class IceSwordState : Player.MeleeAttackState
    {
        public new static string staticID = "GiantIceSwordBasic";
        private static readonly Color Glow = new Color(0.55f, 0.85f, 1f, 1f);

        private NumVarStatMod paceMod;
        private bool paced, executed, charging;
        private float chargeFrom, charge;

        public IceSwordState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.05f;
            animExecTime = 0.3f;
            baseCancelThreshold = 0.5f;
            finalCancelThreshold = 0.6f;
            baseRunThreshold = 0.6f;
            finalRunThreshold = 0.75f;
            baseExitThreshold = 0.75f;
            finalExitThreshold = 0.95f;
            alwaysStartForehand = true;
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 1f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0.5f, 10, VarStatModType.OverrideWithMods);
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic", "WindSlashBasic" };
        }

        public override void OnEnter()
        {
            base.OnEnter();
            executed = false;
            charge = 0f;
            Heavy.Pace(parent, ref paceMod, "GiantIceSword_Pace", GiantWeaponsPlugin.SwordPace, ref paced, true);
            // Still holding the button as the third hit starts: charge a spin instead of chopping.
            charging = comboCounter >= maxComboCount && ButtonHeld();
            chargeFrom = Time.time;
        }

        public override void OnExit()
        {
            Heavy.Pace(parent, ref paceMod, "GiantIceSword_Pace", GiantWeaponsPlugin.SwordPace, ref paced, false);
            Tint(Color.white);
            charging = false;
            base.OnExit();
        }

        public override void Update()
        {
            if (charging)
            {
                charge = Time.time - chargeFrom;
                // Held in the raised pose while it charges; full at SpinFullCharge, and it lets go
                // by itself a moment after that.
                PlayAnimation(comboCounter);
                parent.movement.EndMovement();
                float t = Mathf.Clamp01(charge / GiantWeaponsPlugin.SpinFullCharge);
                Tint(Color.Lerp(Color.white, Glow, t));
                if (ButtonHeld() && charge < GiantWeaponsPlugin.SpinFullCharge + 0.5f)
                    return;
                charging = false;
            }
            base.Update();
        }

        public override void PlayAnimation(int attackCount)
        {
            if (attackCount >= maxComboCount)
                parent.anim.PlayDirectional(parent.PBAoEAnimStr, -1, animStartTime);
            else
                base.PlayAnimation(attackCount);
        }

        // Once per hit, as Momentum Spear.
        public override void ExecuteAttack()
        {
            if (executed)
                return;
            executed = true;
            base.ExecuteAttack();
            Tint(Color.white);
            Vector2 aim = Heavy.Aim(this, parent), hand = Heavy.Hand(parent);
            if (comboCounter < maxComboCount)
                Swing(hand, aim);
            else
                Spin(aim, charge >= GiantWeaponsPlugin.SpinMinCharge ? Mathf.Clamp01(charge / GiantWeaponsPlugin.SpinFullCharge) : -1f);
        }

        // Hits 1-2 (#93, Matt): a 45-degree swing, hit 1 clockwise and hit 2 counterclockwise;
        // the hit box is the same 45 degrees (SwingArc) in front, at SwingReach.
        private void Swing(Vector2 hand, Vector2 aim)
        {
            int hits = 0;
            foreach (GameObject enemy in Heavy.InArc(hand, aim, GiantWeaponsPlugin.SwingReach, GiantWeaponsPlugin.SwingArc * 0.5f))
            {
                Heavy.Ice(enemy.transform.position, parent, skillID, 1, 0.3f);
                hits++;
            }
            float half = GiantWeaponsPlugin.SwingArc * 0.5f;
            bool clockwise = comboCounter % 2 == 1;   // hit 1 clockwise, hit 2 back
            FrostBlade.Sweep(parent, skillID, aim, clockwise ? half : -half, clockwise ? -2f * half : 2f * half, GiantWeaponsPlugin.SwingTurnTime);
            GiantWeaponsPlugin.Log($"Giant Ice Sword: swing {comboCounter} ({(clockwise ? "clockwise" : "counterclockwise")} {2f * half:0} deg), {hits} hit");
        }

        // Hit 3 (#93, Matt): always one 360-degree spin, clockwise from the aim, over SpinTime.
        // The hit box follows the blade: the circle is cut into SpinHits slices, and each slice's
        // enemies are hit as the blade passes them (once each). Tapped (full < 0): SpinRadius and
        // level 2; held past SpinMinCharge: wider with the charge, level 3, or level 4 at full.
        private void Spin(Vector2 aim, float full)
        {
            bool tapped = full < 0f, fullCharge = full >= 1f;
            float radius = (tapped ? GiantWeaponsPlugin.SpinRadius : Mathf.Lerp(GiantWeaponsPlugin.SpinRadius, GiantWeaponsPlugin.FullSpinRadius, full))
                * (IsEmpowered ? GiantWeaponsPlugin.EnhancedSpinWidth : 1f);
            int level = tapped ? 2 : fullCharge ? 4 : 3;
            int slices = GiantWeaponsPlugin.SpinHits;
            float slice = 360f / slices;
            float aimDeg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Player p = parent;
            string id = skillID;
            bool enhanced = IsEmpowered;
            var done = new System.Collections.Generic.HashSet<int>();
            for (int i = 0; i < slices; i++)
            {
                // Clockwise = decreasing angle; slice i is centred (i + 0.5) slices past the aim.
                float centre = (aimDeg - (i + 0.5f) * slice) * Mathf.Deg2Rad;
                var dir = new Vector2(Mathf.Cos(centre), Mathf.Sin(centre));
                float delay = (i + 1) * GiantWeaponsPlugin.SpinTime / slices;
                Heavy.After(delay, () =>
                {
                    if (p == null)
                        return;
                    foreach (GameObject enemy in Heavy.InArc(p.transform.position, dir, radius, slice * 0.5f + 1f))
                        if (done.Add(enemy.GetInstanceID()))
                            Heavy.Ice(enemy.transform.position, p, id, level, 0.12f);   // tight, so a neighbour isn't hit again by this slice's burst
                    Heavy.Sound("WindSlashBasic", p.transform.position, 0.6f);
                });
            }
            FrostBlade.Sweep(p, id, aim, 0f, -360f, GiantWeaponsPlugin.SpinTime);
            Heavy.Shake(fullCharge ? 1f : tapped ? 0.5f : 0.7f);
            if (enhanced)
                FrostRing.Lay(p, id, p.transform.position, radius);
            GiantWeaponsPlugin.Log($"Giant Ice Sword: 360 spin, {(tapped ? "tapped" : fullCharge ? "full charge" : "charged")}, {radius:0.0} u{(enhanced ? ", frost ring" : "")}");
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

        private void Tint(Color c)
        {
            if (parent != null && parent.spriteRenderer != null)
                parent.spriteRenderer.color = c;
        }

        // Enhanced: a frost ring on the floor where it spun, slowing (and lightly chilling) enemies
        // in it every 0.3 s for FrostRingTime (level 5: no damage, always slows).
        private class FrostRing : MonoBehaviour
        {
            private Player owner;
            private string skillID;
            private float radius, until, next;

            public static void Lay(Player owner, string skillID, Vector2 at, float radius)
            {
                var go = new GameObject("GiantIceSword FrostRing");
                go.transform.position = at;
                FrostRing r = go.AddComponent<FrostRing>();
                r.owner = owner;
                r.skillID = skillID;
                r.radius = radius;
                r.until = Time.time + GiantWeaponsPlugin.FrostRingTime;
            }

            private void Update()
            {
                if (owner == null || Time.time >= until)
                {
                    Destroy(gameObject);
                    return;
                }
                if (Time.time < next)
                    return;
                next = Time.time + 0.3f;
                foreach (GameObject enemy in Heavy.Near(transform.position, radius))
                    Heavy.Ice(enemy.transform.position, owner, skillID, 5, 0.3f);
                Heavy.Dust(transform.position, 6, radius);
            }
        }
    }
}
