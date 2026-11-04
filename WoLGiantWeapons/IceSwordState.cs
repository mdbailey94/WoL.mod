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
        private bool paced, executed, charging, tinted;
        // 0.10.0 (#105, Matt: "brace = charge: step forward and brace, swing FAST"): each hit starts
        // with a brace (the blade held drawn back while the wizard steps forward), then snaps.
        private float braceFrom, braceFor;
        private Vector2 braceAim;
        private FrostBlade.Handle held;
        // 0.7.0 (#101): the spin charges only on a FRESH hold: the button must have been let go
        // since hit 2 started, so holding attack through the combo no longer forces a full charge.
        private bool releasedSinceHit2 = true;
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
            if (comboCounter == maxComboCount - 1)
                releasedSinceHit2 = false;
            // Holding the button again as the third hit starts: charge the spin first.
            charging = comboCounter >= maxComboCount && ButtonHeld() && releasedSinceHit2;
            chargeFrom = Time.time;
            // The brace: hit 1 drawn back on one side, hit 2 on the other, the spin from the aim.
            braceFrom = Time.time;
            braceAim = Heavy.Aim(this, parent);
            bool spin = comboCounter >= maxComboCount;
            braceFor = spin ? (charging ? 0f : GiantWeaponsPlugin.SpinBraceTime) : GiantWeaponsPlugin.BraceTime;
            // 0.12.0 (Matt 13:00): the blade is held BACKWARDS (pointing behind the wizard, away
            // from the attack) through the wind-up, then swishes forward. Hit 1 comes round
            // clockwise, hit 2 counterclockwise (+180 and -180 are the same spot behind; the sign
            // only sets which way it comes round), the spin starts from behind too.
            float from = spin ? 180f : comboCounter % 2 == 1 ? 180f : -180f;
            held = FrostBlade.Brace(parent, skillID, braceAim, from);
        }

        public override void OnExit()
        {
            Heavy.Pace(parent, ref paceMod, "GiantIceSword_Pace", GiantWeaponsPlugin.SwordPace, ref paced, false);
            Tint(Color.white);
            charging = false;
            if (held != null)
            {
                held.Drop();   // cancelled before the snap
                held = null;
            }
            if (parent != null && parent.rigidbody2D != null && !executed)
                parent.rigidbody2D.velocity = Vector2.zero;
            base.OnExit();
        }

        public override void Update()
        {
            if (!ButtonHeld())
                releasedSinceHit2 = true;
            // The step forward while bracing (a wall stops it, as it stops any movement).
            if (!executed && parent.rigidbody2D != null)
            {
                float step = comboCounter >= maxComboCount ? GiantWeaponsPlugin.SpinBraceStep : GiantWeaponsPlugin.BraceStep;
                float span = charging ? Mathf.Max(GiantWeaponsPlugin.SpinBraceTime, 0.1f) : Mathf.Max(braceFor, 0.1f);
                bool stepping = Time.time - braceFrom < span;
                parent.rigidbody2D.velocity = stepping ? braceAim * (step / span) : Vector2.zero;
            }
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
            // 0.12.1 (Matt's video 13:18: the blade stayed held back and never swung): the game's
            // melee state ran on through its animation during the brace and ENDED the hit before
            // the brace was over (dropping the held blade), and the next press braced again. Now the
            // state is held in its opening pose for the whole brace (as the spin's charge is), then
            // the swing fires the moment the brace ends. A dash still cancels a brace.
            if (!executed)
            {
                if (Time.time - braceFrom < braceFor)
                {
                    if (CancelToDash(false))
                        return;
                    PlayAnimation(comboCounter);
                    return;
                }
                ExecuteAttack();
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
            // Still bracing: the swing waits (ExecuteAttack is asked again every frame).
            if (Time.time - braceFrom < braceFor)
                return;
            executed = true;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
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
        // 0.6.0 (#95): heavy: the hit lands as the blade comes through the middle of its arc
        // (it reaches half the sweep at EaseTimeOf(0.5) of HeavySwingTime, ~71% at Momentum 2).
        private void Swing(Vector2 hand, Vector2 aim)
        {
            float half = GiantWeaponsPlugin.SwingArc * 0.5f;
            bool clockwise = comboCounter % 2 == 1;   // hit 1 clockwise, hit 2 back
            aim = braceAim;   // the brace set the line
            // From straight behind, round one side, through the aim, and on SwingArc/2 past it.
            float sweep = (180f + half) * (clockwise ? -1f : 1f);
            if (held != null)
            {
                held.Release(sweep, GiantWeaponsPlugin.SwingTurnTime);
                held = null;
            }
            else
                FrostBlade.Sweep(parent, skillID, aim, clockwise ? 180f : -180f, sweep, GiantWeaponsPlugin.SwingTurnTime);
            // The hit lands as the blade crosses the aim: 180 of the (180 + half) degrees.
            float crossAt = GiantWeaponsPlugin.SwingTurnTime * GiantWeaponsPlugin.EaseTimeOf(180f / (180f + half));
            Player p = parent;
            string id = skillID;
            int n = comboCounter;
            Heavy.After(crossAt, () =>
            {
                if (!Heavy.Alive(p))
                    return;
                int hits = 0;
                float hitHalf = Mathf.Max(half, GiantWeaponsPlugin.SwingHitAngle * 0.5f);
                foreach (GameObject enemy in Heavy.InArc(Heavy.Hand(p), aim, GiantWeaponsPlugin.SwingReach, hitHalf))
                {
                    Heavy.Ice(Heavy.Body(enemy), p, id, 1, Heavy.OnBodyRadius);
                    hits++;
                }
                foreach (Vector2 prop in Heavy.PropsInArc(Heavy.Hand(p), aim, GiantWeaponsPlugin.SwingReach, hitHalf))
                    Heavy.Ice(prop, p, id, 1, 0.12f);
                GiantWeaponsPlugin.Log($"Giant Ice Sword: swing {n} ({(clockwise ? "clockwise" : "counterclockwise")} {2f * half:0} deg), {hits} hit");
            });
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
                float centre = (aimDeg + 180f - (i + 0.5f) * slice) * Mathf.Deg2Rad;   // from behind (0.12.0)
                var dir = new Vector2(Mathf.Cos(centre), Mathf.Sin(centre));
                // The blade passes the end of slice i when the eased turn reaches (i+1)/slices.
                float delay = GiantWeaponsPlugin.SpinTime * GiantWeaponsPlugin.EaseTimeOf((i + 1f) / slices);
                Heavy.After(delay, () =>
                {
                    if (!Heavy.Alive(p))
                        return;
                    foreach (GameObject enemy in Heavy.InArc(p.transform.position, dir, radius, slice * 0.5f + 1f))
                        if (done.Add(enemy.GetInstanceID()))
                            Heavy.Ice(Heavy.Body(enemy), p, id, level, Heavy.OnBodyRadius);
                    foreach (Vector2 prop in Heavy.PropsInArc(p.transform.position, dir, radius, slice * 0.5f + 1f))
                        Heavy.Ice(prop, p, id, level, 0.12f);   // tight, so a neighbour isn't hit again by this slice's burst
                    Heavy.Sound("WindSlashBasic", p.transform.position, 0.6f);
                });
            }
            if (held != null)
            {
                held.Release(-360f, GiantWeaponsPlugin.SpinTime);
                held = null;
            }
            else
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

        // Only resets the colour it set itself (0.7.0, #101), so a hurt flash or another mod's
        // tint isn't wiped on every hit.
        private void Tint(Color c)
        {
            if (parent == null || parent.spriteRenderer == null)
                return;
            bool white = c == Color.white;
            if (white && !tinted)
                return;
            parent.spriteRenderer.color = c;
            tinted = !white;
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
                // 0.12.2: only an enemy that isn't slowed yet gets the (no-damage) chill hit, so the
                // ring no longer lands a 0-damage hit (flinch, hit sound, on-hit effects) on every
                // enemy in it every 0.3 s; it re-chills one whose slow wore off.
                foreach (GameObject enemy in Heavy.Near(transform.position, radius))
                {
                    Entity e = enemy.GetComponent<Entity>();
                    if (e != null && e.dotManager != null && e.dotManager.HasDot(SlowDotEffect.slowStatusStr))
                        continue;
                    Heavy.Ice(Heavy.Body(enemy), owner, skillID, 5, 0.3f);
                }
                Heavy.Dust(transform.position, 6, radius);
            }
        }
    }
}
