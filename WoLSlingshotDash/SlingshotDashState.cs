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

        // [Vacuum Fist] settings (0.27.0; the defaults are the values these had in code).
        private static float DragInterval => SlingshotDashPlugin.VacuumDragInterval;
        private static float DragScale => SlingshotDashPlugin.VacuumDragSize;              // hit area of each drag along the dash
        private static float LaunchDragScale => SlingshotDashPlugin.VacuumLaunchDragSize;  // and where you launch
        private static int VacuumPulses => SlingshotDashPlugin.VacuumPulses;
        private static float VacuumPulseInterval => SlingshotDashPlugin.VacuumPulseInterval;
        private const float PunchAt = 0.55f;       // how far into the dash the punch winds up
        private const float GustInterval = 0.035f;
        private static float TrailInterval => SlingshotDashPlugin.VacuumGustInterval;
        private static float TrailScale => SlingshotDashPlugin.VacuumGustSize;
        private static float TrailRepeat => SlingshotDashPlugin.VacuumGustRepeat;
        private float nextGust;
        private float nextTrail;
        private float nextDrag;
        private Vector2 direction;
        private float launchedAt;
        private bool punchPlayed;

        // ---- The look (only for show) ----
        private const string Name = "Vacuum Fist";
        private const float RingLift = 0.1f;          // the whirlwind turns round the feet...
        private const float RingMinScale = 0.85f;     // ...growing from this at the start of the charge to...
        private const float RingMaxScale = 1.15f;     // ...this at a full charge
        private const int RingOrder = 2;              // its halves sort this far behind / in front of the wizard
        private const float StreakInterval = 0.05f;   // wind wisps left along the rush
        private const float StreakSpread = 0.45f;     // how far to either side of your path
        private const float StreakMinLift = 0.3f, StreakMaxLift = 1.1f; // at about body height
        private const float StreakDrift = 2f;         // they blow back the way you came
        private const int TrailVariants = 6;          // the game's wind trail has 6 kinds of wisp we use
        private const float GhostInterval = 0.06f;    // pale afterimages along the rush
        private const float GhostLife = 0.2f;
        // The game's palest wind colour (#cfebeb), for the afterimages and the white shockwave ring.
        private static readonly Color GhostColor = new Color(0.81f, 0.92f, 0.92f, 0.5f);
        private static readonly Color ShockColour = new Color(0.81f, 0.92f, 0.92f, 0.6f);
        private FxHandle ringBack, ringFront;
        private float ringScale;
        private float nextStreak, nextGhost;
        private bool fullCue;

        // Charging: a whirlwind spinning fast round your feet, quicker and wider as it builds.
        protected override float ChargeEffectInterval => 0.035f;
        private float whirl;

        // The game's whirlwind forms round the wizard and keeps turning: its far half behind them,
        // its near half in front, both following the hop.
        protected override void OnChargeStarted()
        {
            PixelFx.Setup(parent);
            fullCue = false;
            StopRing(false);
            ringScale = RingMinScale;
            Effects.Visual("whirlwind", () =>
            {
                SpriteRenderer body = parent.spriteRenderer;
                var back = new FxOpts { follow = parent.transform, offset = new Vector2(0f, RingLift), orderFrom = body, orderDelta = -RingOrder, scale = RingMinScale };
                var front = back;
                front.orderDelta = RingOrder;
                FxAnim far = AirArt.WhirlBack.Get();
                ringBack = FxSprite.Play(far, parent.transform.position, back);
                ringFront = FxSprite.Play(AirArt.WhirlFront.Get(), parent.transform.position, front);
                PixelFx.Once("air charge", $"{Name}: look 'charge' playing (" + (far != null && far.FromGame ? "the game's whirlwind" : "drawn wind ring") +
                    ", game air puffs and wind swirl)");
            });
        }

        protected override void WhileCharging(float holdTime)
        {
            ringScale = Mathf.Lerp(RingMinScale, RingMaxScale, Mathf.Clamp01(holdTime));
            ringBack.SetScale(ringScale);
            ringFront.SetScale(ringScale);
            // Full: the wind snaps tight round you (the game's spinning air burst).
            if (!fullCue && holdTime >= 1f)
            {
                fullCue = true;
                Effects.Game("SpinningAirBurst", 1, (Vector2)parent.transform.position + Vector2.up * 0.5f);
            }
        }

        // Ends the charging whirlwind; at the launch it's left behind, rising and breaking up.
        private void StopRing(bool released)
        {
            if (released && ringBack.Alive)
            {
                Vector2 at = (Vector2)parent.transform.position + new Vector2(0f, RingLift);
                float scale = ringScale;
                Effects.Visual("whirlwind", () => FxSprite.Play(AirArt.WhirlOut.Get(), at, new FxOpts { scale = scale, sortOffset = -0.2f }));
            }
            ringBack.Stop();
            ringFront.Stop();
            ringBack = default(FxHandle);
            ringFront = default(FxHandle);
        }

        protected override void OnStateExit() => StopRing(false);

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

        // Flair only: the game's dash air burst and wind twirl where you take off, its air
        // shockwaves along the aim (as Gale Blast throws them), the whirlwind left behind, and you
        // ride off in the wizard's own dash streaks.
        private void LaunchLook(Vector2 position, float charge)
        {
            PixelFx.Setup(parent);
            StopRing(true);
            nextStreak = 0f;
            nextGhost = 0f;
            float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Effects.Visual("dash air burst", () => PoolManager.GetPoolItem<DashAirBurst>().Burst(parent.attackOriginTrans.position, direction));
            Effects.Visual("launch twirl", () =>
            {
                FxAnim twirl = AirArt.Twirl.Get();
                // The drawn stand-in is a fan of speed lines, so it points back along the launch.
                FxSprite.Play(twirl, position + Vector2.up * 0.5f, new FxOpts
                {
                    rotation = twirl != null && twirl.FromGame ? 0f : aim + 180f,
                    layer = FxLayer.Effect,
                    scale = 0.8f + 0.3f * charge
                });
            });
            Effects.Game("AirWaveShockwave", 1, position + direction * 0.75f, ParticleEffectOverrides.StartLifeTime0p5, aim);
            if (charge > 0.5f)
                Effects.Game("AirWaveShockwave", 1, position + direction * 1.75f, ParticleEffectOverrides.StartLifeTime0p5, aim);
            Effects.DashTrails(parent, Name);
            PixelFx.Once("air launch", $"{Name}: look 'launch' played (game DashAirBurst, wind twirl, AirWaveShockwave, the whirlwind breaking up, dash trails)");
        }

        // Flair only: wind wisps whipping past and pale afterimages along the rush.
        private void RushLook(Vector2 position)
        {
            if (Time.time >= nextStreak)
            {
                nextStreak = Time.time + StreakInterval;
                Vector2 side = new Vector2(-direction.y, direction.x) * Random.Range(-StreakSpread, StreakSpread);
                float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                Effects.Visual("wind wisps", () =>
                {
                    FxAnim wisp = AirArt.Trail.Get();
                    FxSprite.Play(wisp, position + side, new FxOpts
                    {
                        // The game's wisps curl every which way; the drawn streak points along the rush.
                        rotation = wisp != null && wisp.FromGame ? 0f : aim,
                        variant = Random.Range(0, TrailVariants),
                        flipX = Random.value < 0.5f,
                        lift = Random.Range(StreakMinLift, StreakMaxLift),
                        velocity = -direction * StreakDrift,
                        drag = 3f,
                        sortOffset = -side.y
                    });
                });
            }
            if (Time.time >= nextGhost)
            {
                nextGhost = Time.time + GhostInterval;
                Effects.Visual("air afterimage", () => PixelFx.Afterimage(parent, GhostColor, GhostLife, Name));
            }
        }

        // Flair only: the vacuum where the punch lands: the game's air vortex drawing wisps in, its
        // shockwave ring closing in on the floor, and its wind slash for the punch.
        private void LandLook(Vector2 position, float charge)
        {
            float size = 0.9f + 0.35f * charge;
            float aim = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            // Flattened like the floor, so just behind the wizard (who stands in its middle).
            Effects.Visual("vacuum vortex", () =>
            {
                FxAnim vortex = AirArt.Vortex.Get();
                FxSprite.Play(vortex, position,
                    new FxOpts { lift = 0.4f, orderFrom = parent.spriteRenderer, orderDelta = -1, snapshotOrder = true, scale = size });
                // The same art as the game's AirVortex particle: only add that when the drawn one stands in.
                if (vortex == null || !vortex.FromGame)
                    Effects.Game("AirVortex", 2, position);
            });
            Effects.Visual("vacuum ring", () => FxSprite.Play(AirArt.Ring.Get(), position,
                new FxOpts { layer = FxLayer.Ground, reverse = true, scale = size, colour = ShockColour }));
            Effects.Visual("punch slash", () =>
            {
                FxAnim slash = AirArt.Slash.Get();
                FxSprite.Play(slash, position + direction * 0.7f, new FxOpts
                {
                    rotation = aim - (slash != null ? slash.BaseAngle : 0f),
                    lift = 0.7f,
                    layer = FxLayer.Effect,
                    scale = 0.75f + 0.25f * charge
                });
            });
            PixelFx.Once("air land", $"{Name}: look 'vacuum punch' played (game air vortex, closing shockwave ring and wind slash)");
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
            SlingshotDashPlugin.Run(Vacuum(position, SlingshotDashPlugin.VacuumSize + SlingshotDashPlugin.VacuumChargeSize * charge, parent.skillCategory, skillID));
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
                    Effects.Visual("vacuum ring", () => FxSprite.Play(AirArt.Ring.Get(), position,
                        new FxOpts { layer = FxLayer.Ground, reverse = true, scale = 0.35f * scale, speed = 1.4f, colour = ShockColour }));
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
