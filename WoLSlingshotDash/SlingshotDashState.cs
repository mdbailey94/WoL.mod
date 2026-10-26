using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vacuum Fist (Air): a charged launch rushes you forward on a wind that grabs enemies you pass
    // and drags them along with you (level 1, its knockback pointed along the dash: the Drag
    // setting), finishing with a punch that lands in a vacuum, pulling everyone nearby in for a
    // moment (level 2, negative knockback, like Gust Burst). Bigger with more charge. Enhanced, it
    // also leaves a trail of gusts along the dash (level 3) that each blow twice.
    public class SlingshotDashState : ChargedDashState
    {
        public new static string staticID = "SlingshotDash";

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        private const float DragInterval = 0.1f;
        private const float DragScale = 2f;        // hit area of each drag along the dash
        private const float LaunchDragScale = 3f;  // and where you launch
        private const int VacuumPulses = 4;
        private const float VacuumPulseInterval = 0.15f;
        private const float PunchAt = 0.55f;       // how far into the dash the punch winds up
        private const float GustInterval = 0.035f;
        private const float TrailInterval = 0.08f;
        private const float TrailScale = 1.1f;
        private const float TrailRepeat = 0.35f;
        private float nextGust;
        private float nextTrail;
        private float nextDrag;
        private Vector2 direction;
        private float launchedAt;
        private bool punchPlayed;

        // ---- The look (only for show) ----
        private const string Name = "Vacuum Fist";
        private const float RingLift = 0.1f;          // the wind ring sits round the feet...
        private const float RingMinScale = 0.75f;     // ...growing from this at a tap to...
        private const float RingMaxScale = 1.2f;      // ...this at a full charge
        private const int RingOrder = 2;              // its halves sort this far behind / in front of the wizard
        private const float StreakInterval = 0.045f;  // gust streaks left along the rush
        private const float StreakSpread = 0.45f;     // how far to either side of your path
        private const float StreakMinLift = 0.3f, StreakMaxLift = 1.1f; // at about body height
        private const float StreakDrift = 2f;         // they blow back the way you came
        private const float GhostInterval = 0.06f;    // pale afterimages along the rush
        private const float GhostLife = 0.2f;
        private static readonly Color GhostColor = new Color(0.78f, 1f, 0.95f, 0.5f);
        private FxHandle ringBack, ringFront;
        private float nextStreak, nextGhost;
        private bool fullCue;

        // Charging: a whirlwind spinning fast round your feet, quicker and wider as it builds.
        protected override float ChargeEffectInterval => 0.035f;
        private float whirl;

        // The drawn wind ring wraps round the wizard: its far half behind them, its near half in
        // front, both following the hop.
        protected override void OnChargeStarted()
        {
            PixelFx.Setup(parent);
            fullCue = false;
            StopRing();
            Effects.Visual("wind ring", () =>
            {
                SpriteRenderer body = parent.spriteRenderer;
                var back = new FxOpts { follow = parent.transform, offset = new Vector2(0f, RingLift), orderFrom = body, orderDelta = -RingOrder, scale = RingMinScale };
                var front = back;
                front.orderDelta = RingOrder;
                ringBack = FxSprite.Play(AirArt.RingBack, parent.transform.position, back);
                ringFront = FxSprite.Play(AirArt.RingFront, parent.transform.position, front);
                PixelFx.Once("air charge", $"{Name}: look 'charge' playing (drawn wind ring, game air puffs and wind swirl)");
            });
        }

        protected override void WhileCharging(float holdTime)
        {
            float charge = Mathf.Clamp01(holdTime);
            float scale = Mathf.Lerp(RingMinScale, RingMaxScale, charge);
            ringBack.SetScale(scale);
            ringFront.SetScale(scale);
            // Full: the wind snaps tight round you (the game's spinning air burst).
            if (!fullCue && holdTime >= 1f)
            {
                fullCue = true;
                Effects.Game("SpinningAirBurst", 1, (Vector2)parent.transform.position + Vector2.up * 0.5f);
            }
        }

        private void StopRing()
        {
            ringBack.Stop();
            ringFront.Stop();
            ringBack = default(FxHandle);
            ringFront = default(FxHandle);
        }

        protected override void OnStateExit() => StopRing();

        protected override void ChargeEffect(Vector2 position, float charge)
        {
            whirl += 75f * Mathf.Deg2Rad;
            float radius = 0.5f + 0.5f * charge;
            for (int i = 0; i < 2; i++)
            {
                float a = whirl + i * Mathf.PI;
                Effects.AirPuffs(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * radius,
                    1 + Mathf.RoundToInt(2 * charge));
            }
            if (charge >= 0.3f && Random.value < 0.25f)
                Effects.WindSwirl(position, 1);
        }

        protected override void OnLaunch(float charge)
        {
            Vector2 position = parent.transform.position;
            direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            launchedAt = Time.time;
            punchPlayed = false;
            nextGust = 0f;
            nextTrail = 0f;
            nextDrag = 0f;
            // A wide grab where you launch, dragging enemies along with you.
            Drag(position, LaunchDragScale);
            Effects.WindSwirl(position, 2 + Mathf.RoundToInt(3 * charge));
            Effects.AirPuffs(position, 8 + Mathf.RoundToInt(8 * charge));
            Effects.Shake(0.5f + 0.5f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                1.3f - 0.3f * charge, false);
            LaunchLook(position, charge);
        }

        // Flair only: the wind bursts out behind you (drawn gust and the game's air shockwaves,
        // as Gale Blast throws them), and you ride off in the wizard's own dash streaks.
        private void LaunchLook(Vector2 position, float charge)
        {
            PixelFx.Setup(parent);
            StopRing();
            nextStreak = 0f;
            nextGhost = 0f;
            float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Effects.Visual("launch gust", () => FxSprite.Play(AirArt.Cone, position + Vector2.up * 0.5f,
                new FxOpts { rotation = aim + 180f, layer = FxLayer.Effect, scale = 0.8f + 0.4f * charge }));
            Effects.Game("AirWaveShockwave", 1, position + direction * 0.75f, ParticleEffectOverrides.StartLifeTime0p5, aim);
            if (charge > 0.5f)
                Effects.Game("AirWaveShockwave", 1, position + direction * 1.75f, ParticleEffectOverrides.StartLifeTime0p5, aim);
            Effects.DashTrails(parent, Name);
            PixelFx.Once("air launch", $"{Name}: look 'launch' played (drawn launch gust, game AirWaveShockwave, dash trails)");
        }

        // Flair only: gust streaks whipping past and pale afterimages along the rush.
        private void RushLook(Vector2 position)
        {
            if (Time.time >= nextStreak)
            {
                nextStreak = Time.time + StreakInterval;
                Vector2 side = new Vector2(-direction.y, direction.x) * Random.Range(-StreakSpread, StreakSpread);
                float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Effects.Visual("gust streak", () => FxSprite.Play(AirArt.Streak, position + side, new FxOpts
                {
                    rotation = aim,
                    lift = Random.Range(StreakMinLift, StreakMaxLift),
                    velocity = -direction * StreakDrift,
                    drag = 3f,
                    sortOffset = -side.y
                }));
            }
            if (Time.time >= nextGhost)
            {
                nextGhost = Time.time + GhostInterval;
                Effects.Visual("air afterimage", () => PixelFx.Afterimage(parent, GhostColor, GhostLife, Name));
            }
        }

        // Flair only: the vacuum's look where the punch lands: drawn wind spiralling in to a pop,
        // a ring on the floor closing in, the punch's impact, and the game's air vortex.
        private void LandLook(Vector2 position, float charge)
        {
            float size = 0.9f + 0.45f * charge;
            float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // Flattened like the floor, so just behind the wizard (who stands in its middle).
            Effects.Visual("vacuum implosion", () => FxSprite.Play(AirArt.Implode, position,
                new FxOpts { lift = 0.3f, orderFrom = parent.spriteRenderer, orderDelta = -1, snapshotOrder = true, scale = size }));
            Effects.Visual("vacuum ring", () => FxSprite.Play(AirArt.Ring, position,
                new FxOpts { layer = FxLayer.Ground, reverse = true, scale = size }));
            Effects.Visual("punch impact", () => FxSprite.Play(AirArt.Star, position + direction * 0.6f,
                new FxOpts { rotation = aim, lift = 0.7f, layer = FxLayer.Effect, scale = 0.8f + 0.3f * charge }));
            Effects.Game("AirVortex", 2, position);
            PixelFx.Once("air land", $"{Name}: look 'vacuum punch' played (drawn implosion, closing ring and punch impact, game AirVortex)");
        }

        protected override void WhileDashing(float charge)
        {
            Vector2 position = parent.transform.position;
            // The rushing wind: grabs and drags enemies you pass along the dash.
            if (Time.time >= nextDrag)
            {
                nextDrag = Time.time + DragInterval;
                Drag(position, DragScale);
            }
            // Enhanced: gusts left along the dash line.
            if (IsEmpowered && Time.time >= nextTrail)
            {
                nextTrail = Time.time + TrailInterval;
                SlingshotDashPlugin.Run(TrailGust(position, parent.skillCategory, skillID));
            }
            // Flair only: wind up the punch for the end of the rush.
            if (!punchPlayed && Time.time - launchedAt >= dashDuration * PunchAt)
            {
                punchPlayed = true;
                parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            }
            // Flair only: a streaming gust behind you as you fly.
            RushLook(position);
            if (Time.time < nextGust)
                return;
            nextGust = Time.time + GustInterval;
            Effects.AirPuffs(position, 2);
            Effects.Dust(position, 3, 0.25f);
        }

        // The punch lands in a vacuum that keeps pulling everyone nearby in for a moment.
        protected override void OnLand(float charge)
        {
            Vector2 position = parent.transform.position;
            SlingshotDashPlugin.Run(Vacuum(position, 1.75f + 1.25f * charge, parent.skillCategory, skillID));
            Effects.WindSwirl(position, 3);
            Effects.DustSuction(position, 30);
            Effects.Shake(0.5f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                1.1f - 0.3f * charge, false);
            LandLook(position, charge);
        }

        // An invisible wind burst (level 1) whose knockback points along the dash, so it drags.
        private void Drag(Vector2 position, float scale)
        {
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
            if (burst == null)
                return;
            burst.emitParticles = false;
            if (burst.attack != null)
                burst.attack.knockbackOverwriteVector = direction;
        }

        // Level 2 pulses: negative knockback, pulling toward the centre like Gust Burst.
        private static IEnumerator Vacuum(Vector2 position, float scale, string skillCategory, string id)
        {
            for (int i = 0; i < VacuumPulses; i++)
            {
                WindBurst pulse = WindBurst.CreateBurst(position, skillCategory, id, 2, scale);
                if (pulse != null)
                    pulse.emitParticles = false;
                Effects.WindSwirl(position, 1);
                Effects.Dust(position, 24, scale);
                // Flair only: each pulse draws the floor in again (a smaller ring closing in).
                if (i > 0)
                    Effects.Visual("vacuum ring", () => FxSprite.Play(AirArt.Ring, position,
                        new FxOpts { layer = FxLayer.Ground, reverse = true, scale = 0.35f * scale, speed = 1.4f }));
                yield return new WaitForSeconds(VacuumPulseInterval);
            }
        }

        // One gust on the trail: blows now and once more a moment later.
        private static IEnumerator TrailGust(Vector2 position, string skillCategory, string id)
        {
            for (int i = 0; i < 2; i++)
            {
                WindBurst gust = WindBurst.CreateBurst(position, skillCategory, id, 3, TrailScale);
                if (gust != null)
                    gust.emitParticles = false;
                Effects.WindSwirl(position, 1);
                Effects.AirPuffs(position, 3);
                yield return new WaitForSeconds(TrailRepeat);
            }
        }
    }
}
