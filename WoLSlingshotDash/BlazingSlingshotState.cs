using Chaos.AnimatorExtensions;
using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Blazing Kick (Fire): a charged launch rushes you forward wrapped in Blazing Blitz's
    // flame trail, dropping flame bursts that shove enemies out of your path. The kick at the end
    // lands in a fiery blast that throws everyone nearby away. Bigger with more charge.
    //
    // Level 1 is the launch and trail (knockback pushes away from each burst), level 2 the blast.
    // Enhanced, levels 3 and 4 stand in for them: the same, but every hit sets enemies on fire.
    public class BlazingSlingshotState : ChargedDashState
    {
        public new static string staticID = "BlazingSlingshot";

        private const float TrailInterval = 0.1f;
        // Hit area of each trail burst; the visible flames are smaller.
        private const float TrailHitScale = 2f;
        // Hit area of the burst where you launch.
        private const float LaunchHitScale = 3f;
        private const float FlameInterval = 0.03f;
        // The blast where you land.
        private const float BlastScale = 2f;
        private const float BlastChargeScale = 1.25f;

        private float nextTrail;
        // Enhanced uses the burning copies of the levels (3 and 4).
        private int LevelOffset => IsEmpowered ? 2 : 0;
        private float nextFlame;

        public BlazingSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // When in the dash the wizard swings into the kick (flair only), and whether it has.
        private const float KickAt = 0.55f;
        private float launchedAt;
        private bool kickPlayed;

        // ---- The look (only for show) ----
        private const string Name = "Blazing Kick";
        private const int Tongues = 6;                 // drawn flames ringing your feet while charging
        private const float TongueRadius = 0.6f;       // how far out they stand...
        private const float TongueSquash = 0.45f;      // ...on a ring flattened like the floor
        private const int TonguesAtTap = 3;            // flames lit at the start of the charge; all at full
        private const float TongueMinScale = 0.55f, TongueMaxScale = 1.05f;
        private const float WispInterval = 0.05f;      // dying flames left along the rush
        private const float WispSpread = 0.35f;
        private const float ScorchInterval = 0.14f;    // the game's scorch marks along the rush
        private const float SmokeInterval = 0.2f;
        private const float EmberRise = 2.2f;          // embers float up this fast (units a second)
        private const int BlastEmbers = 10;            // thrown out by the blast
        private FxHandle[] tongues;
        private float nextWisp, nextScorch, nextSmoke;
        private Vector2 aim;
        private bool fullCue;

        // Charging: flames licking up round your feet, hotter as it builds, and the wizard
        // trembling with the heat.
        protected override void ChargeEffect(Vector2 position, float charge)
        {
            EmitBlitzFlames(position, 2 + Mathf.RoundToInt(5 * charge));
            // Flair only: embers drifting up out of the flames.
            for (int i = 0; i < 1 + Mathf.RoundToInt(charge); i++)
            {
                Vector2 at = position + new Vector2(Random.Range(-0.6f, 0.6f), Random.Range(-0.2f, 0.2f));
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember, at, new FxOpts
                {
                    lift = Random.Range(0.2f, 0.8f),
                    velocity = new Vector2(Random.Range(-0.4f, 0.4f), EmberRise * Random.Range(0.7f, 1.2f)),
                    drag = 0.8f,
                    layer = FxLayer.Effect
                }));
            }
        }

        // Flair only: a ring of drawn flames lights up round your feet, more of them and taller
        // as the charge builds (those on the far side behind the wizard, the rest in front).
        protected override void OnChargeStarted()
        {
            PixelFx.Setup(parent);
            fullCue = false;
            StopTongues();
            Effects.Visual("flame tongues", () =>
            {
                if (tongues == null)
                    tongues = new FxHandle[Tongues];
                SpriteRenderer body = parent.spriteRenderer;
                for (int i = 0; i < Tongues; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / Tongues;
                    var offset = new Vector2(Mathf.Cos(a) * TongueRadius, Mathf.Sin(a) * TongueRadius * TongueSquash);
                    tongues[i] = FxSprite.Play(FireArt.Tongue, parent.transform.position, new FxOpts
                    {
                        follow = parent.transform,
                        offset = offset,
                        orderFrom = body,
                        orderDelta = offset.y > 0f ? -1 : 1,
                        flipX = i % 2 == 1,
                        speed = 0.85f + 0.3f * PixelArt.Hash(i, 3),
                        scale = i < TonguesAtTap ? TongueMinScale : 0.01f
                    });
                }
                PixelFx.Once("fire charge", $"{Name}: look 'charge' playing (drawn flame ring and embers, game Blitz flames)");
            });
        }

        protected override void WhileCharging(float holdTime)
        {
            if (tongues == null)
                return;
            float charge = Mathf.Clamp01(holdTime);
            int lit = TonguesAtTap + Mathf.RoundToInt((Tongues - TonguesAtTap) * charge);
            float scale = Mathf.Lerp(TongueMinScale, TongueMaxScale, charge);
            for (int i = 0; i < tongues.Length; i++)
                tongues[i].SetScale(i < lit ? scale * (0.9f + 0.2f * PixelArt.Hash(i, 5)) : 0.01f);
            // Full: the flames flare (the game's ring of fire bursting at your feet).
            if (!fullCue && holdTime >= 1f)
            {
                fullCue = true;
                Effects.Game("FlameCircleBurstEmitter", 12, parent.transform.position, ParticleEffectOverrides.Radius0p5);
            }
        }

        private void StopTongues()
        {
            if (tongues == null)
                return;
            for (int i = 0; i < tongues.Length; i++)
            {
                tongues[i].Stop();
                tongues[i] = default(FxHandle);
            }
        }

        protected override void OnStateExit() => StopTongues();

        // Flair only: a small blast where you take off, scorching the floor.
        private void LaunchLook(Vector2 position, float charge)
        {
            PixelFx.Setup(parent);
            StopTongues();
            aim = inputVector.sqrMagnitude > 0.01f ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            nextWisp = 0f;
            nextScorch = 0f;
            nextSmoke = 0f;
            Effects.Visual("drawn fire blast", () => FxSprite.Play(FireArt.Blast, position, new FxOpts
            {
                lift = 0.35f,
                layer = FxLayer.Effect,
                scale = 0.45f + 0.2f * charge,
                speed = 1.3f
            }));
            Effects.Game("FlameCircleBurstEmitter", 10 + Mathf.RoundToInt(10 * charge), position, ParticleEffectOverrides.Radius0p75);
            Effects.Game("BurnMarkMedEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
            PixelFx.Once("fire launch", $"{Name}: look 'launch' played (drawn fire blast, game FlameCircleBurstEmitter and BurnMarkMedEmitter)");
        }

        // Flair only: dying flames and embers left along the rush, scorch marks and smoke.
        private void RushLook(Vector2 position)
        {
            if (Time.time >= nextWisp)
            {
                nextWisp = Time.time + WispInterval;
                Vector2 at = position + new Vector2(-aim.y, aim.x) * Random.Range(-WispSpread, WispSpread);
                Effects.Visual("flame wisp", () => FxSprite.Play(FireArt.Wisp, at, new FxOpts
                {
                    flipX = Random.value < 0.5f,
                    scale = Random.Range(0.8f, 1.1f),
                    speed = Random.Range(0.9f, 1.15f)
                }));
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember, at, new FxOpts
                {
                    lift = Random.Range(0.3f, 1f),
                    velocity = -aim * Random.Range(1f, 2.5f) + Vector2.up * EmberRise,
                    drag = 1.5f,
                    layer = FxLayer.Effect
                }));
            }
            if (Time.time >= nextScorch)
            {
                nextScorch = Time.time + ScorchInterval;
                Effects.Game("BurnMarkSmallEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeOne);
            }
            if (Time.time >= nextSmoke)
            {
                nextSmoke = Time.time + SmokeInterval;
                Effects.Game("SmokeEmitter", 2, position + Vector2.up, SmokeEmitter.SmokeSmallOverride);
            }
        }

        // Flair only: the kick's blast: drawn fireball and scorch ring, the kick's impact,
        // embers thrown out, and the game's ring of fire, scorch mark and smoke.
        private void LandLook(Vector2 position, float charge)
        {
            float size = 0.85f + 0.4f * charge;
            float angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            Effects.Visual("drawn fire blast", () => FxSprite.Play(FireArt.Blast, position, new FxOpts { lift = 0.4f, layer = FxLayer.Effect, scale = size }));
            Effects.Visual("scorch ring", () => FxSprite.Play(FireArt.Ring, position, new FxOpts { layer = FxLayer.Ground, scale = size }));
            Effects.Visual("fire kick impact", () => FxSprite.Play(FireArt.Star, position + aim * 0.6f,
                new FxOpts { rotation = angle, lift = 0.6f, layer = FxLayer.Effect, scale = 0.8f + 0.3f * charge }));
            for (int i = 0; i < BlastEmbers; i++)
            {
                float a = (i + Random.value * 0.5f) * Mathf.PI * 2f / BlastEmbers;
                var fling = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f) * Random.Range(3f, 5.5f) * size;
                Effects.Visual("ember", () => FxSprite.Play(FireArt.Ember, position, new FxOpts
                {
                    lift = 0.5f,
                    velocity = fling + Vector2.up * 1.5f,
                    drag = 3f,
                    speed = 0.7f,
                    layer = FxLayer.Effect
                }));
            }
            Effects.Game("FlameCircleBurstEmitter", 24 + Mathf.RoundToInt(16 * charge), position, ParticleEffectOverrides.RadiusTwo);
            Effects.Game("BurnMarkLargeEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
            Effects.Game("SmokeEmitter", 8, position + Vector2.up, SmokeEmitter.SmokeMedOverride, float.NaN, 0.1f);
            PixelFx.Once("fire land", $"{Name}: look 'blast' played (drawn fire blast, scorch ring, kick impact and embers, " +
                "game FlameCircleBurstEmitter, BurnMarkLargeEmitter and SmokeEmitter)");
        }

        protected override float ChargeShake(float charge) => 0.03f + 0.04f * charge;

        protected override void OnLaunch(float charge)
        {
            nextTrail = 0f;
            nextFlame = 0f;
            launchedAt = Time.time;
            kickPlayed = false;
            // Flair only: a fiery blast and a shake as you take off.
            Effects.FireBlast(parent.transform.position, 6 + Mathf.RoundToInt(6 * charge));
            Effects.Shake(0.5f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f,
                0.9f - 0.2f * charge, false);
            // A wide hit where you launch, shoving enemies away (no effect of its own; the
            // trail's flames show it).
            FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1 + LevelOffset, LaunchHitScale, false);
            LaunchLook(parent.transform.position, charge);
        }

        protected override void WhileDashing(float charge)
        {
            Vector3 position = parent.transform.position;
            // Flair only: swing into the kick for the end of the rush.
            if (!kickPlayed && Time.time - launchedAt >= dashDuration * KickAt)
            {
                kickPlayed = true;
                parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            }
            nextFlame -= Time.deltaTime;
            if (nextFlame <= 0f)
            {
                nextFlame = FlameInterval;
                EmitBlitzFlames(position, 2 + Mathf.RoundToInt(3 * charge));
            }
            RushLook(position);
            nextTrail -= Time.deltaTime;
            if (nextTrail <= 0f)
            {
                nextTrail = TrailInterval;
                // The hit: a full-size burst with no effect of its own (the small visible flames
                // are the Blitz trail), so it catches enemies you dash past and shoves them out of
                // your way.
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, TrailHitScale, false);
                // The small visible burst (same skill, so it can't hit anyone twice).
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, 0.8f + 0.4f * charge, true);
            }
        }

        // The kick lands in a fiery blast that throws everyone nearby away (level 2).
        protected override void OnLand(float charge)
        {
            Vector2 position = parent.transform.position;
            float scale = BlastScale + BlastChargeScale * charge;
            FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 2 + LevelOffset, scale, true);
            EmitBlitzFlames(position, 14);
            // Flair only: the blast's fire, a ring of dust thrown out and a shake.
            Effects.FireBlast(position, 12 + Mathf.RoundToInt(8 * charge));
            Effects.Dust(position, 60, scale);
            Effects.Shake(0.9f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                0.7f - 0.1f * charge, false);
            LandLook(position, charge);
        }

        // The flame particles Blazing Blitz trails behind the wizard.
        private static void EmitBlitzFlames(Vector3 position, int count)
        {
            try
            {
                FireBurst fire = PoolManager.GetPoolItem<FireBurst>();
                if (fire != null)
                    fire.EmitSingle(new int?(count), new Vector3?(position));
            }
            catch
            {
            }
        }
    }
}
