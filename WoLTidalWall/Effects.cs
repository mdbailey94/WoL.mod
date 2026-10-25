using System.Collections.Generic;
using UnityEngine;

namespace WoLTidalWall
{
    // The game's own water effects, sounds, camera shake and hit-stop, each made safe to call: if
    // one isn't available it's skipped (and logged once) instead of breaking the cast. Pool names
    // and sound IDs are the ones the game's water arcana use (see the comment on each).
    public static class Effects
    {
        private static readonly HashSet<string> failed = new HashSet<string>();

        // Tidal Blast's water drops (WaterBlast.Blast, WaterDropEmitter.LargeWaterExplosionOverride).
        public static void Drops(Vector2 position, int count, bool large) =>
            Try("water drops", () => PoolManager.GetPoolItem<WaterDropEmitter>().Emit(new int?(count), new Vector3?(position),
                large ? WaterDropEmitter.LargeWaterExplosionOverride : WaterDropEmitter.MedWaterExplosionOverride, null, 0f, null, null));

        // A ring of drops (WaterBlast.Dissipate uses the same call).
        public static void DropRing(Vector2 position, float radius, int perPoint) =>
            Try("water drop ring", () => WaterDropEmitter.EmitCircle(position, perPoint, 18, radius,
                WaterDropEmitter.LargeWaterExplosionOverride, 0f));

        // Water Bounce's directional splash (Player.UseWaterBounce, "WaterHitSmallEffect").
        public static void Splash(Vector2 position, Vector2 direction) =>
            Try("water splash", () => PoolManager.GetPoolItem<ParticleEffect>("WaterHitSmallEffect").Emit(new int?(1),
                new Vector3?(position), null, new Vector3?(Globals.GetRotationVector(direction)), 0f, null, null));

        // The burst most water projectiles end in (WaterBomb, AquaVortexBomb: "WaterExplosion").
        public static void Burst(Vector2 position) =>
            Try("water burst", () => PoolManager.GetPoolItem<ParticleEffect>("WaterExplosion").Emit(new int?(1),
                new Vector3?(position), null, null, 0f, null, null));

        // Whirlpool's landing splash (Player.UseWhirlpool: AnimEffect "WaterBombExplosion"), scaled.
        public static void BigSplash(Vector2 position, float scale) =>
            Try("big splash", () => PoolManager.GetPoolItem<AnimEffect>("WaterBombExplosion").Play(position, string.Empty, 0f,
                default(Vector2), 1f, scale, new Vector3?(Vector3.up)));

        // Whirlpool's water gathering in (Player.UseWhirlpool: "WaterFormEffect" with a negative
        // start speed so the drops stream inward), played for a while.
        public static ParticleEffect Gather(Vector2 position, float radius, float seconds)
        {
            ParticleEffect effect = null;
            Try("water gathering", () =>
            {
                effect = PoolManager.GetPoolItem<ParticleEffect>("WaterFormEffect");
                var inward = new ParticleSystemOverride { startSpeed = -40f, startLifetime = 0.15f, radius = radius };
                effect.Emit(new int?(60), new Vector3?(position), inward, null, 0f, null, null);
                effect.Play(new float?(80f), new Vector3?(position), inward, new float?(seconds), null, null, 0f, true);
            });
            return effect;
        }

        // Whirlpool's ring and twirl as the water forms ("WaterRingEffect", "WaterTwirlFormEffect").
        public static void Ring(Vector2 position, float radius) =>
            Try("water ring", () => PoolManager.GetPoolItem<ParticleEffect>("WaterRingEffect").Emit(new int?(100),
                new Vector3?(position), new ParticleSystemOverride { radius = radius }, null, 0f, null, null));

        public static void Twirl(Vector2 position) =>
            Try("water twirl", () => PoolManager.GetPoolItem<ParticleEffect>("WaterTwirlFormEffect").Emit(new int?(1),
                new Vector3?(position), null, null, 0f, null, null));

        // Ice shards bursting (Water Ward, the ice boss: "IcicleBreakEffect"), for the frozen crash.
        public static void Ice(Vector2 position, int count) =>
            Try("ice shards", () => PoolManager.GetPoolItem<ParticleEffect>("IcicleBreakEffect").Emit(new int?(count),
                new Vector3?(position), null, null, 0f, null, null));

        public static AudioSource Sound(string id, Vector2 position, float pitch = 1f)
        {
            AudioSource source = null;
            Try("sound " + id, () => source = SoundManager.PlayAudioWithDistance(id, new Vector2?(position), null, 24f, -1f, pitch, false));
            return source;
        }

        // A sound that follows the wave (Whirlpool's "WaterLoop" while it spins).
        public static AudioSource FollowSound(string id, Transform follow, float pitch = 1f)
        {
            AudioSource source = null;
            Try("sound " + id, () => source = SoundManager.PlayAudioWithDistance(id, null, follow, 24f, -1f, pitch, false));
            return source;
        }

        public static void Shake(float intensity, bool ignoreShakeCount = false) =>
            Try("camera shake", () => CameraController.ShakeCamera(intensity, ignoreShakeCount));

        // Freezes the game for a few frames, so a hit lands with weight; the game's own
        // TimeScaleController puts the time back by itself.
        public static void HitStop(int frames) =>
            Try("hit-stop", () => TimeScaleController.FreezeForFrames(frames, false));

        // Near-frozen time that eases back to normal over a moment of real time (what the game's
        // crit relic does on a super crit, SuperCritChance); also restored by the game itself.
        public static void SlowMo(float realSeconds, float timeScale) =>
            Try("slow motion", () => TimeScaleController.SetTimeScaleDelayOverRealTime(realSeconds, timeScale, true, true));

        public static void Try(string name, System.Action effect)
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
                TidalWallPlugin.Log($"Tidal Wall: effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }
    }
}
