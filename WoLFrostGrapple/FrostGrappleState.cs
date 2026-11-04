using System;
using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLFrostGrapple
{
    // The cast, start to finish, in one state (as Stone Charge does), so the wizard stays in it
    // while the pick flies and the chain pulls:
    //   Wind   the forehand throw until its execute point (can be dashed out of);
    //   Fly    the pick travels along the aim; the first enemy it reaches is bitten (level 1), a wall
    //          stops it; past Range it comes back (a miss: MissCooldownShare of the cooldown);
    //   Pull   you're dragged along the chain at Pull Speed, unhittable and over pits, for at most
    //          MaxSeconds, stopping LandGap short of the enemy (or at the wall);
    //   Land   the ice burst (level 2, BurstRadius; enhanced: the one upgrade, a wider burst).
    // SIGNATURE (cinematic): the stage darkens and the camera closes in; each landing is a stop
    // (level 3, a short hit-stop), the pick leaps to the nearest enemy not yet hit and you follow,
    // up to Targets; the last one is the finale (level 4, wide burst, hit-stop, white flash, a
    // flare of light and a moment of slow-motion).
    public class FrostGrappleState : Player.SkillState
    {
        public new static string staticID = "FrostGrapple";

        private enum Phase { Wind, Fly, Return, Pull, Recover, Done }

        private Phase phase;
        private bool signature, enhanced, missed;
        private Vector2 aim, pick, pickDir;
        private float flown, phaseTime;
        private GameObject target;            // the enemy the pick bit (null: a wall)
        private Vector2 anchor;               // where the pull goes (the enemy's body, or the wall)
        private readonly HashSet<int> visited = new HashSet<int>();
        private int stops;
        private SignatureCinematic cinematic;
        private GrappleLook look;
        private bool floating, invulnerable;
        private static readonly BoolVarStatMod FloatMod = new BoolVarStatMod("FrostGrapple_Float", true);

        public override string OnEnterAnimStr => parent.ForehandAnimStr;

        public FrostGrappleState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            isProjectileSkill = true;
            isMovementSkill = true;
            // The cooldown counts from the end (as Stone Charge's); a miss then shortens it.
            startCooldownOnEnter = false;
            SetAnimTimes(0.05f, 0.1f, 0.25f, 0.5f, 0.6f, 0.7f);
        }

        public override void OnEnter()
        {
            base.OnEnter();
            signature = isUltimate;
            enhanced = IsEmpowered;
            phase = Phase.Wind;
            phaseTime = 0f;
            missed = false;
            target = null;
            visited.Clear();
            stops = 0;
            aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            parent.movement.EndMovement();
            if (signature && FrostGrapplePlugin.Cinematic)
            {
                cinematic = SignatureCinematic.Begin();
                if (cinematic != null)
                    FrostGrapplePlugin.Log("Frost Grapple: signature, " + cinematic.TakeOff(FrostGrapplePlugin.StageDarken));
            }
        }

        public override void OnExit()
        {
            Cleanup(phase == Phase.Done ? "landed" : "cut short");
            base.OnExit();
        }

        private void Cleanup(string how)
        {
            Float(false);
            if (parent != null && parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (look != null)
            {
                look.Finish();
                look = null;
            }
            if (cinematic != null)
            {
                cinematic.Finish(how);
                cinematic = null;
            }
        }

        public override void Update()
        {
            UpdateRepeatLock(string.Empty);
            if (skillExecuted)
            {
                ConcludeSkill();
                return;
            }
            if (phase == Phase.Wind && !parent.anim.AnimPlayed(animExecTime))
            {
                CancelToDash(false);
                return;
            }
            ExecuteSkill();
        }

        public override void ExecuteSkill()
        {
            try
            {
                phaseTime += Time.deltaTime;
                switch (phase)
                {
                    case Phase.Wind: Throw(Hand(), aim, signature ? FrostGrapplePlugin.SignatureThrowSpeed : FrostGrapplePlugin.ThrowSpeed); return;
                    case Phase.Fly: Fly(); return;
                    case Phase.Return: Return(); return;
                    case Phase.Pull: Pull(); return;
                    case Phase.Recover: Recover(); return;
                }
            }
            catch (Exception e)
            {
                FrostGrapplePlugin.Log($"Frost Grapple: {phase} failed: {e.Message}");
                Finish();
            }
            if (phase == Phase.Done)
                base.ExecuteSkill();
        }

        private Vector2 Hand() => parent.attackOriginTrans != null ? (Vector2)parent.attackOriginTrans.position : (Vector2)parent.transform.position;

        // ---- Throw and flight ----

        private float speed;

        private void Throw(Vector2 from, Vector2 dir, float spd)
        {
            pick = from;
            pickDir = dir.normalized;
            flown = 0f;
            speed = spd;
            if (look == null)
                look = GrappleLook.Make(parent);
            look.Flying(pick);
            Sound("WindSlashBasic", from, 0.5f, 1.3f);
            Enter(Phase.Fly);
        }

        private void Fly()
        {
            float step = speed * Time.deltaTime;
            Vector2 next = pick + pickDir * step;
            // A wall (or obstacle) on the way stops it.
            RaycastHit2D wall = Physics2D.Raycast(pick, pickDir, step + 0.1f, ChaosCollisions.layerAllWallAndObst);
            GameObject hit = FirstEnemyAlong(pick, next, FrostGrapplePlugin.HitReach);   // 0.1.2 (Matt): 1.0, was 0.5
            if (hit != null)
            {
                Bite(hit);
                return;
            }
            if (wall.collider != null)
            {
                pick = wall.point - pickDir * 0.1f;
                look.Stuck(pick);
                target = null;
                anchor = wall.point - pickDir * 0.5f;
                Sound("IceHit", pick, 0.6f, 1.2f);
                StartPull();
                return;
            }
            pick = next;
            flown += step;
            look.Flying(pick);
            look.Chain(Hand(), pick);
            if (flown >= FrostGrapplePlugin.Range)
                Enter(Phase.Return);
        }

        private void Bite(GameObject enemy)
        {
            target = enemy;
            visited.Add(enemy.GetInstanceID());
            anchor = Body(enemy);
            pick = anchor;
            look.Stuck(pick);
            Burst(anchor, 1, 0.35f);
            Sound("IceHit", anchor, 0.7f, 1f);
            StartPull();
        }

        // A miss: the pick flies back to the hand, then only part of the cooldown is spent.
        private void Return()
        {
            Vector2 hand = Hand();
            pick = Vector2.MoveTowards(pick, hand, speed * 1.5f * Time.deltaTime);
            look.Flying(pick);
            look.Chain(hand, pick);
            if ((pick - hand).sqrMagnitude < 0.05f || phaseTime > 1f)
            {
                missed = stops == 0;
                Finish();
            }
        }

        // ---- The pull ----

        private void StartPull()
        {
            Float(true);
            if (!invulnerable)
            {
                invulnerable = true;
                try { parent.health.SetInvulnerabilityDuration(FrostGrapplePlugin.MaxPull + 0.15f, false); } catch { }
            }
            try { parent.anim.PlayDirectional(parent.DashAnimStr, -1, 0f); } catch { }
            if (cinematic != null)
            {
                if (stops == 0)
                    cinematic.Windup(FrostGrapplePlugin.CameraZoom);
                cinematic.Dive();
            }
            Enter(Phase.Pull);
        }

        private void Pull()
        {
            if (target != null && target.activeInHierarchy)
                anchor = Body(target);
            Vector2 me = parent.transform.position;
            Vector2 to = anchor - me;
            float stopAt = target != null ? FrostGrapplePlugin.LandGap : 0.05f;
            bool arrived = to.magnitude <= stopAt + 0.05f;
            // Don't pull through a wall that appeared in between.
            bool blocked = Physics2D.Raycast(me, to.normalized, Mathf.Min(to.magnitude, 0.35f), ChaosCollisions.layerAllWallAndObst).collider != null && target != null;
            if (arrived || blocked || phaseTime >= FrostGrapplePlugin.MaxPull)
            {
                Land();
                return;
            }
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = to.normalized * FrostGrapplePlugin.PullSpeed;
            look.Chain(Hand(), pick = target != null ? anchor : pick);
        }

        private void Land()
        {
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            invulnerable = false;
            if (target == null)
            {
                // A wall: no damage, just there.
                Enter(Phase.Recover);
                return;
            }
            Vector2 at = Vector2.Lerp(parent.transform.position, anchor, 0.6f);
            if (!signature)
            {
                float r = enhanced ? FrostGrapplePlugin.EnhancedRadius : FrostGrapplePlugin.BurstRadius;
                Burst(at, 2, r);
                look.Impact(at, r, enhanced);
                Shake(0.6f);
                Enter(Phase.Recover);
                return;
            }
            stops++;
            GameObject next = stops < FrostGrapplePlugin.SignatureTargets ? NextEnemy(anchor) : null;
            if (next != null)
            {
                // A stop on the way: a burst, a beat of hit-stop, and the pick leaps on.
                Burst(at, 3, FrostGrapplePlugin.BurstRadius);
                look.Impact(at, FrostGrapplePlugin.BurstRadius, false);
                HitStop(FrostGrapplePlugin.StopHitStop);
                Throw(parent.transform.position, Body(next) - (Vector2)parent.transform.position, FrostGrapplePlugin.SignatureThrowSpeed);
                return;
            }
            // The finale.
            float fr = FrostGrapplePlugin.SignatureFinaleRadius;
            Burst(at, 4, fr);
            look.Impact(at, fr, true);
            Shake(1.4f);
            Sound("IceHit", at, 1f, 0.7f);
            if (cinematic != null)
                FrostGrapplePlugin.Log("Frost Grapple: finale, " + cinematic.Impact(FrostGrapplePlugin.HitStopFrames, FrostGrapplePlugin.ImpactFlash, FrostGrapplePlugin.ImpactSlowMo));
            FrostGrapplePlugin.Log($"Frost Grapple: signature chain of {stops}");
            Enter(Phase.Recover);
        }

        private void Recover()
        {
            Float(false);
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (phaseTime >= 0.15f)
                Finish();
        }

        private void Finish()
        {
            if (phase == Phase.Done)
                return;
            float cd = FrostGrapplePlugin.Cooldown * (missed ? FrostGrapplePlugin.MissRefund : 1f);
            FrostGrapplePlugin.Log($"Frost Grapple: {(missed ? $"missed, cooldown {cd:0.0} s" : target != null ? "landed" : "pulled to a wall")}");
            Cleanup("landed");
            phase = Phase.Done;
            try { parent.anim.PlayDirectional(parent.ForehandAnimStr, -1, 0.65f); } catch { }
            base.ExecuteSkill();
            // A miss: the cooldown the game just started is replaced by the shorter one (no extra charge spent).
            if (missed)
                try { StartCooldownTimer(cd, false); } catch { }
        }

        private void Enter(Phase p)
        {
            phase = p;
            phaseTime = 0f;
        }

        // ---- Helpers ----

        private void Float(bool on)
        {
            try
            {
                if (parent == null || parent.fall == null || parent.fall.ignoreFall == null || floating == on)
                    return;
                if (on)
                    parent.fall.ignoreFall.AddMod(FloatMod);
                else
                    parent.fall.ignoreFall.RemoveMod(FloatMod.ID);
                floating = on;
            }
            catch { }
        }

        private void Burst(Vector2 at, int level, float radius)
        {
            try { IceBurst.CreateBurst(at, parent.skillCategory, skillID, level, radius); }
            catch (Exception e) { FrostGrapplePlugin.Log($"Frost Grapple: a burst failed: {e.Message}"); }
        }

        private static void HitStop(int frames)
        {
            if (frames <= 0)
                return;
            try { TimeScaleController.FreezeForFrames(frames, false); } catch { }
        }

        private static void Shake(float s)
        {
            try { CameraController.ShakeCamera(s); } catch { }
        }

        private static void Sound(string name, Vector2 at, float volume, float pitch)
        {
            try { SoundManager.PlayAudioWithDistance(name, at, null, 24f, volume, pitch); } catch { }
        }

        private GameObject FirstEnemyAlong(Vector2 from, Vector2 to, float reach)
        {
            if (GameController.enemies == null)
                return null;
            Vector2 d = to - from;
            float len = d.magnitude;
            Vector2 dir = len > 0.0001f ? d / len : pickDir;
            GameObject best = null;
            float bestAlong = float.MaxValue;
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null || !go.activeInHierarchy || visited.Contains(go.GetInstanceID()) || !AliveEnemy(go))
                    continue;
                Vector2 rel = Body(go) - from;
                float along = Vector2.Dot(rel, dir);
                if (along < -reach || along > len + reach)
                    continue;
                if (Mathf.Abs(rel.x * dir.y - rel.y * dir.x) <= reach && along < bestAlong)
                {
                    best = go;
                    bestAlong = along;
                }
            }
            return best;
        }

        private GameObject NextEnemy(Vector2 from)
        {
            if (GameController.enemies == null)
                return null;
            GameObject best = null;
            float bestD = FrostGrapplePlugin.SignatureSearch * FrostGrapplePlugin.SignatureSearch;
            foreach (GameObject go in GameController.enemies)
            {
                if (go == null || !go.activeInHierarchy || visited.Contains(go.GetInstanceID()) || !AliveEnemy(go))
                    continue;
                Vector2 p = Body(go);
                float d = (p - from).sqrMagnitude;
                if (d < bestD && Physics2D.Linecast(from, p, ChaosCollisions.layerAllWall).collider == null)
                {
                    best = go;
                    bestD = d;
                }
            }
            return best;
        }

        private static bool AliveEnemy(GameObject go)
        {
            var e = go.GetComponent<Entity>();
            return e == null || e.health == null || e.health.CurrentHealthValue > 0;
        }

        // The middle of the enemy's hurt box (its pivot is at the feet).
        private static int hurtLayer = -2;

        internal static Vector2 Body(GameObject go)
        {
            if (hurtLayer == -2)
                hurtLayer = LayerMask.NameToLayer("EnemyHurtBox");
            foreach (Collider2D c in go.GetComponentsInChildren<Collider2D>())
                if (c != null && c.enabled && (hurtLayer < 0 || c.gameObject.layer == hurtLayer))
                    return c.bounds.center;
            var e = go.GetComponent<Entity>();
            if (e != null && e.spriteRenderer != null)
                return e.spriteRenderer.bounds.center;
            return go.transform.position;
        }
    }
}
