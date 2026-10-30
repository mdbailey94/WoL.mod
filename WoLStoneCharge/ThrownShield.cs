using System.Collections.Generic;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge's cast since 0.6.0 (Matt): the wizard throws a big stone shield (the game's own
    // Aegis Charge slab, grown) forward. It sails out in an arc, hangs in the air over the spot it
    // will land on for a beat (the ground there stirs: the tell), then slams down: an earth blast
    // round it that throws enemies outward, with the crater, rock spikes and flung stones of the
    // old crash. Enhanced: bigger, wider, and it hangs less. Signature: a huge shield with burning
    // cracks, the camera pulling back and the room dimming while it's up, a hit-stop, a flash, a
    // heavy shake and slow motion as it lands, then a shockwave ring further out.
    //
    // Its own object, so the wizard is free to move once it's thrown. The game's shield shoves
    // what's right in front of the wizard as it appears (level 1); its own hit box stays off.
    public class ThrownShield : MonoBehaviour
    {
        public const int ShoveLevel = 1, SlamLevel = 2, EnhancedSlamLevel = 3, SignatureSlamLevel = 4, ShockwaveLevel = 5;

        private static float SignatureFlightTime => AdvancedTuning.SignatureFlightTime;
        private static float SlamTime => AdvancedTuning.SlamTime;
        private static float SignatureSlamTime => AdvancedTuning.SignatureSlamTime;   // the drop from the air
        private const float TellEvery = 0.12f;
        // The shield's height (units) as it's thrown, at the end of its flight (plus an arc in the
        // middle), and when it starts to drop.
        private static float StartHeight => AdvancedTuning.StartHeight;
        private static float HangHeight => AdvancedTuning.HangHeight;
        private static float RiseHeight => AdvancedTuning.RiseHeight;
        private static float ArcHeight => AdvancedTuning.ArcHeight;
        private static float SigHangHeight => AdvancedTuning.SigHangHeight;
        private static float SigRiseHeight => AdvancedTuning.SigRiseHeight;
        private static float SigArcHeight => AdvancedTuning.SigArcHeight;
        // The signature's beats (the old phalanx crash's).
        private static float SigZoom => AdvancedTuning.SigZoom;
        private static float SigDim => AdvancedTuning.SigDim;
        private static int SigHitStopFrames => AdvancedTuning.SigHitStopFrames;
        private static float SigFlash => AdvancedTuning.SigFlash;
        private static float SigShake => AdvancedTuning.SigShake;
        private static float SigSlowMoTime => AdvancedTuning.SigSlowMoTime;
        private static float SigSlowMoScale => AdvancedTuning.SigSlowMoScale;
        private static float SigEaseBack => AdvancedTuning.SigEaseBack;
        private static float ShockwaveAfter => AdvancedTuning.ShockwaveAfter;    // real seconds after the slam (past the slow motion)
        private static float ShockwaveScale => AdvancedTuning.ShockwaveScale;     // of the slam radius
        // The drawn crash (the old crash's).
        private const float GameCrackFps = 12f;
        private const float CraterFps = 18f;
        private const float CraterLife = 1.1f;
        private const float CraterWidth = 3f;
        private const float CraterFill = 0.8f;
        private const int MinShards = 6;
        private const int MinSpikes = 8;
        private const float SpikeReach = 0.8f;
        private const float FloorSquashY = 0.6f;
        private const float SpikeStagger = 0.035f;
        private const float SpikeFps = 16f;
        private const float ShardFps = 12f;

        private enum Stage { Flight, Hang, Drop, Landed }

        private Player thrower;
        private string category, skillID;
        private ShieldCharge shield;
        private Transform picture;
        private Vector3 pictureBase;
        private Vector2 from, to, dir;
        private bool enhanced, signature;
        private float size, radius, flightTime, hangTime, dropTime;
        private float hang, rise, arc;
        private Stage stage;
        private float age, nextTell, shockwaveAt;
        private int cinematic;
        private static readonly HashSet<string> failed = new HashSet<string>();

        public static void Throw(Player player, string skillID, Vector2 direction, bool enhanced, bool signature)
        {
            if (player == null)
                return;
            var go = new GameObject("StoneChargeThrownShield");
            go.AddComponent<ThrownShield>().Begin(player, skillID, direction, enhanced, signature);
        }

        private void Begin(Player player, string id, Vector2 direction, bool enhanced, bool signature)
        {
            thrower = player;
            category = player.skillCategory;
            skillID = id;
            this.enhanced = enhanced;
            this.signature = signature;
            dir = direction != Vector2.zero ? direction.normalized : Vector2.right;
            size = signature ? StoneChargePlugin.SignatureShieldSize : StoneChargePlugin.ShieldSize * (enhanced ? 1.25f : 1f);
            radius = signature ? StoneChargePlugin.SignatureSlamRadius : enhanced ? StoneChargePlugin.EnhancedSlamRadius : StoneChargePlugin.SlamRadius;
            flightTime = signature ? SignatureFlightTime : StoneChargePlugin.FlightTime;
            hangTime = signature ? StoneChargePlugin.SignatureSlamDelay : StoneChargePlugin.SlamDelay * (enhanced ? 0.75f : 1f);
            dropTime = signature ? SignatureSlamTime : SlamTime;
            hang = signature ? SigHangHeight : HangHeight;
            rise = signature ? SigRiseHeight : RiseHeight;
            arc = signature ? SigArcHeight : ArcHeight;

            from = (Vector2)player.attackOriginTrans.position + dir * 0.8f;
            float distance = signature ? StoneChargePlugin.SignatureDistance : StoneChargePlugin.Distance;
            // It lands short of a wall in the way rather than inside it.
            try
            {
                RaycastHit2D wall = Physics2D.Raycast(from, dir, distance, ChaosCollisions.layerAllWall);
                if (wall.collider != null)
                    distance = Mathf.Max(0.5f, wall.distance - 0.6f);
            }
            catch { }
            to = from + dir * distance;

            try
            {
                shield = Globals.ChaosInst<ShieldCharge>(ShieldCharge.Prefab, player.transform.parent, (Vector3)from);
                if (shield == null || shield.attack == null)
                    throw new System.Exception("the prefab has no ShieldCharge or attack on it");
                shield.followTrans = null;
                shield.atkVector = dir;
                shield.SetAttackVars(category, skillID);
                shield.attack.knockbackOverwriteVector = dir;
                shield.SetAttackStatus(false);
                shield.transform.localScale = Vector3.one * size;
                picture = shield.transform.Find("Sprite");
                if (picture != null)
                    pictureBase = picture.localPosition;
                if (StoneChargePlugin.DrawnEffects)
                {
                    ShieldRunes runes = ShieldRunes.Attach(shield.spriteRenderer);
                    runes?.SetStage(signature ? ShieldRunes.Stages : enhanced ? 3 : 2);
                }
            }
            catch (System.Exception e)
            {
                StoneChargeState.LogOnce("thrown shield", $"couldn't make the game's stone shield ({e.Message}); the slam still lands");
                shield = null;
            }
            if (signature)
                cinematic = Cinematic.Begin();
            Effect("sound", () => SoundManager.PlayAudioWithDistance("BoulderShield", new Vector2?(from), null, 24f, -1f, signature ? 0.7f : 0.9f, false));
            StoneChargeState.Log($"{(signature ? "signature " : enhanced ? "enhanced " : "")}shield thrown {distance:0.0} units, size x{size:0.0}: " +
                $"it hangs {hangTime:0.00} s and slams at radius {radius:0.0}");
        }

        private void Update()
        {
            if (thrower == null)
            {
                Destroy(gameObject);
                return;
            }
            age += Time.deltaTime;
            switch (stage)
            {
                case Stage.Flight:
                {
                    float t = Mathf.Clamp01(age / flightTime);
                    float eased = 1f - (1f - t) * (1f - t);
                    Place(Vector2.Lerp(from, to, eased), Mathf.Lerp(StartHeight, hang, eased) + arc * 4f * t * (1f - t));
                    Zoom(t * flightTime / (flightTime + hangTime));
                    if (t >= 1f)
                    {
                        stage = Stage.Hang;
                        age = 0f;
                    }
                    break;
                }
                case Stage.Hang:
                {
                    float t = Mathf.Clamp01(age / Mathf.Max(0.01f, hangTime));
                    // Drawing itself up to drop, trembling as it goes.
                    float tremble = Mathf.Sin(age * 60f) * 0.04f * size * t;
                    Place(to + new Vector2(tremble, 0f), Mathf.Lerp(hang, rise, 1f - (1f - t) * (1f - t)));
                    Zoom((flightTime + t * hangTime) / (flightTime + hangTime));
                    Tell(t);
                    if (t >= 1f)
                    {
                        stage = Stage.Drop;
                        age = 0f;
                    }
                    break;
                }
                case Stage.Drop:
                {
                    float t = Mathf.Clamp01(age / dropTime);
                    Place(to, Mathf.Lerp(rise, 0f, t * t));
                    if (t >= 1f)
                        Slam();
                    break;
                }
                case Stage.Landed:
                    if (Time.unscaledTime >= shockwaveAt)
                    {
                        Shockwave();
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        private void OnDestroy()
        {
            if (shield != null)
            {
                try { shield.Reset(); } catch { }
                shield = null;
            }
            if (cinematic != 0)
            {
                Cinematic.EaseOut(cinematic, 0.5f, "the thrown shield was gone");
                cinematic = 0;
            }
        }

        // The shield over a spot on the ground, its picture lifted to a height (snapped to the
        // game's pixels, so it doesn't shimmer; the picture is a child of the grown shield).
        private void Place(Vector2 ground, float height)
        {
            if (shield == null)
                return;
            shield.transform.position = new Vector3(ground.x, ground.y, shield.transform.position.z);
            if (picture != null)
                picture.localPosition = pictureBase + new Vector3(0f, Mathf.Round(height * 16f) / 16f / size, 0f);
        }

        // The signature: the camera pulls back and the room dims while the shield is up.
        private void Zoom(float progress)
        {
            if (cinematic == 0)
                return;
            float eased = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
            Cinematic.Set(cinematic, SigZoom * eased, Mathf.Lerp(1f, SigDim, eased));
        }

        // The ground under it stirs, more as the drop nears: dust drawn in and a stone or two.
        private void Tell(float t)
        {
            if (age < nextTell)
                return;
            nextTell = age + TellEvery;
            Effect("tell dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(6 + Mathf.RoundToInt(10f * t), radius * 0.45f,
                -2f, -0.5f, new Vector3?(to), null));
            Effect("tell rocks", () => PoolManager.GetPoolItem<ParticleEffect>("RockFormEffect").Emit(new int?(2 + Mathf.RoundToInt(4f * t)),
                new Vector3?(to), RockFormEffect.SmallSettings, null, 0f, null, null));
            if (signature)
                Effect("tell rumble", () => CameraController.ShakeCamera(0.1f + 0.4f * t, false));
        }

        private void Slam()
        {
            Vector2 at = to;
            int level = signature ? SignatureSlamLevel : enhanced ? EnhancedSlamLevel : SlamLevel;
            // A signature's hits are marked as the game marks its own (isUltimate), so they don't
            // refill the gauge.
            Effect("slam", () => EarthBurst.CreateBurst(at, AttackInfo.GetInfo(thrower, category, skillID, level, signature), radius));
            if (shield != null)
            {
                try { shield.Reset(); } catch { }
                shield = null;
            }
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(signature ? 90 : 40, radius * 0.5f, -6f, -1f, new Vector3?(at), null));
            Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(signature ? 24 : 12),
                new Vector3?(at), RockDebrisEmitter.FloorExplosionOverrideR1p5, null, 0f, null));
            Effect("game crater", () => PoolManager.GetPoolItem<ParticleEffect>("FloorCraterLarge").Emit(new int?(1), new Vector3?(at), null, null, 0f, null, null));
            if (StoneChargePlugin.DrawnEffects)
            {
                Effect("crater", () => Crater(at, radius));
                Effect("rock spikes", () => SpikeRing(at, radius, MinSpikes + (signature ? 8 : enhanced ? 2 : 0)));
                Effect("rock shard", () => Shards(at, MinShards + (signature ? 8 : enhanced ? 2 : 0)));
            }
            Effect("sound", () => SoundManager.PlayAudioWithDistance(signature ? "EarthExplosion" : "EarthExplosionShort", new Vector2?(at), null, 24f, -1f, signature ? 0.75f : 0.95f, false));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthPebble", new Vector2?(at), null, 24f, -1f, 1f, false));
            if (!signature)
            {
                Effect("shake", () => CameraController.ShakeCamera(enhanced ? 1f : 0.8f, false));
                StoneChargeState.Log($"{(enhanced ? "enhanced " : "")}slam at radius {radius:0.0}");
                Destroy(gameObject);
                return;
            }
            Effect("hit-stop", () => TimeScaleController.FreezeForFrames(SigHitStopFrames, false));
            Cinematic.Flash(cinematic, SigFlash, 0.1f);
            Effect("quake", () => CameraController.ShakeCamera(SigShake, true));
            Effect("slow motion", () => TimeScaleController.SetTimeScaleDelayOverRealTime(SigSlowMoTime, SigSlowMoScale, true, true));
            StoneChargeState.Log($"signature slam at radius {radius:0.0}: hit-stop {SigHitStopFrames} frames, flash x{SigFlash:0.0}, shake {SigShake:0.0}, " +
                $"slow motion {SigSlowMoTime:0.00} s; the shockwave rolls out {ShockwaveAfter:0.00} s later");
            stage = Stage.Landed;
            shockwaveAt = Time.unscaledTime + ShockwaveAfter;
        }

        // The signature's shockwave: the ground erupts again in a wider ring, throwing enemies outward.
        private void Shockwave()
        {
            Vector2 at = to;
            float wide = radius * ShockwaveScale;
            Effect("shockwave", () =>
            {
                EarthBurst burst = EarthBurst.CreateBurst(at, AttackInfo.GetInfo(thrower, category, skillID, ShockwaveLevel, true), wide);
                burst.skipParticleEmit = true;
            });
            if (StoneChargePlugin.DrawnEffects)
                Effect("rock spikes", () => SpikeRing(at, wide, 18));
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60, wide * 0.6f, -6f, -1f, new Vector3?(at), null));
            Effect("shake", () => CameraController.ShakeCamera(1.3f, true));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", new Vector2?(at), null, 24f, -1f, 0.8f, false));
            Cinematic.EaseOut(cinematic, SigEaseBack, "shockwave finished");
            cinematic = 0;
        }

        // ---- The drawn crash (the old crash's crater, spikes and stones) ----

        private void Crater(Vector2 at, float r)
        {
            Sprite[] game = StoneArt.GameCrater;
            FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Crater(signature), at, "TargetLineGround", 0);
            if (fx == null)
                return;
            fx.fps = game != null ? GameCrackFps : CraterFps;
            fx.life = CraterLife;
            fx.fadeAt = 0.65f;
            fx.size = r * 2f * CraterFill / (game != null ? StoneArt.CrackWidth : CraterWidth);
        }

        // The game's rock spikes shooting up and crumbling round the edge, one after another from
        // the side facing the throw.
        private void SpikeRing(Vector2 at, float r, int count)
        {
            if (StoneArt.GameSpike(0) == null)
                return;
            float ahead = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            for (int i = 0; i < count; i++)
            {
                float angle = (ahead + i * 360f / count) * Mathf.Deg2Rad;
                Vector2 spot = at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * FloorSquashY) * r * SpikeReach
                    + Random.insideUnitCircle * 0.1f;
                Sprite[] frames = StoneArt.GameSpike(i);
                FxSprite fx = FxSprite.Spawn(frames, spot, "Actor", 0);
                if (fx == null)
                    return;
                int step = i == 0 ? 0 : (i + 1) / 2;
                fx.delay = step * SpikeStagger;
                fx.fps = SpikeFps;
                fx.life = fx.delay + frames.Length / SpikeFps;
                fx.fadeAt = 0.85f;
                fx.sr.flipX = Random.value < 0.5f;
                if (signature)
                    fx.size = 1.5f;
            }
        }

        // Stones flung out of the crash every way, each hopping, tumbling and fading.
        private void Shards(Vector2 at, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = Random.Range(0f, 360f);
                Vector2 way = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                Sprite[] game = StoneArt.GameShard(i);
                FxSprite fx = FxSprite.Spawn(game ?? StoneArt.Shard(i), at + way * 0.3f, "Effect", 0);
                if (fx == null)
                    return;
                fx.velocity = way * Random.Range(3f, 6.5f) * (signature ? 1.8f : 1f);
                fx.drag = 2.5f;
                fx.life = Random.Range(0.55f, 0.75f);
                fx.hop = Random.Range(0.5f, 1.2f) * (signature ? 1.6f : 1f);
                fx.hopTime = fx.life * 0.8f;
                fx.loop = true;
                fx.fps = ShardFps;
                if (game != null)
                {
                    fx.angle = 90f * i;
                    fx.spin = (i % 2 == 0 ? 90f : -90f) * ShardFps;
                    fx.spinStep = 90f;
                }
                else
                    fx.Skip(i);
                fx.fadeAt = 0.75f;
            }
        }

        // A cosmetic effect (or one of the hits); if it's unavailable it's skipped and logged once.
        private static void Effect(string name, System.Action effect)
        {
            if (failed.Contains(name))
                return;
            try
            {
                effect();
            }
            catch (System.Exception e)
            {
                failed.Add(name);
                StoneChargeState.Log($"effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }
    }
}
