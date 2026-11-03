using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLGiantWeapons
{
    // Giant Ice Sword (#83, numbers in Log - Modder 2026-11-03 12:44, approved by Matt): a slow
    // 3-hit greatsword basic on the game's melee combo, with no attack object of its own
    // (skipAtkObjSpawn). Hits 1-2 are wide arcs; hit 3 is an overhead chop, or, with the button
    // still held as it starts, a charge (the wizard glows ice blue) released into a spin. Levels:
    // 1 a swing, 2 the chop, 3 a spin hit, 4 a full-charge spin hit, 5 the enhanced frost ring.
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
            else if (charge >= GiantWeaponsPlugin.SpinMinCharge)
                Spin(Mathf.Clamp01(charge / GiantWeaponsPlugin.SpinFullCharge));
            else
                Chop(hand, aim);
        }

        // Hits 1-2: a wide arc in front.
        private void Swing(Vector2 hand, Vector2 aim)
        {
            int hits = 0;
            foreach (GameObject enemy in Heavy.InArc(hand, aim, GiantWeaponsPlugin.SwingReach, GiantWeaponsPlugin.SwingArc * 0.5f))
            {
                Heavy.Ice(enemy.transform.position, parent, skillID, 1, 0.3f);
                hits++;
            }
            FrostBlade.Show(parent, skillID, aim, false, 0f);
            GiantWeaponsPlugin.Log($"Giant Ice Sword: swing {comboCounter}, {hits} hit");
        }

        // Hit 3, tapped: an overhead chop along the aim.
        private void Chop(Vector2 hand, Vector2 aim)
        {
            int hits = 0;
            foreach (GameObject enemy in Heavy.AlongLine(hand, aim, GiantWeaponsPlugin.ChopReach, 0.5f))
            {
                Heavy.Ice(enemy.transform.position, parent, skillID, 2, 0.35f);
                hits++;
            }
            Heavy.Ice(hand + aim * GiantWeaponsPlugin.ChopReach, parent, skillID, 2, 0.5f);
            FrostBlade.Show(parent, skillID, aim, false, 0f);
            Heavy.Shake(0.8f);
            Heavy.Sound("EarthExplosion", hand + aim * GiantWeaponsPlugin.ChopReach, 0.8f);
            GiantWeaponsPlugin.Log($"Giant Ice Sword: chop, {hits} hit");
        }

        // Hit 3, held: the spin, SpinHits hits round the wizard over SpinTime.
        private void Spin(float full)
        {
            bool fullCharge = full >= 1f;
            float radius = Mathf.Lerp(GiantWeaponsPlugin.SpinRadius, GiantWeaponsPlugin.FullSpinRadius, full) * (IsEmpowered ? GiantWeaponsPlugin.EnhancedSpinWidth : 1f);
            int count = GiantWeaponsPlugin.SpinHits;
            Player p = parent;
            string id = skillID;
            bool enhanced = IsEmpowered;
            for (int i = 0; i < count; i++)
            {
                float delay = count > 1 ? i * GiantWeaponsPlugin.SpinTime / (count - 1) : 0f;
                Heavy.After(delay, () =>
                {
                    if (p == null)
                        return;
                    Heavy.Ice(p.transform.position, p, id, fullCharge ? 4 : 3, radius);
                    Heavy.Sound("WindSlashBasic", p.transform.position, 0.6f);
                });
            }
            FrostBlade.Show(p, id, Heavy.Aim(this, p), true, GiantWeaponsPlugin.SpinTime);
            Heavy.Shake(fullCharge ? 1f : 0.6f);
            if (enhanced)
                FrostRing.Lay(p, id, p.transform.position, radius);
            GiantWeaponsPlugin.Log($"Giant Ice Sword: spin, {radius:0.0} u{(fullCharge ? ", full charge" : "")}{(enhanced ? ", frost ring" : "")}");
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
