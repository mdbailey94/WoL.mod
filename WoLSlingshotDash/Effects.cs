using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // The game's own impact effects (its particle pools, camera shake and hit-stop), each made safe
    // to call: if one isn't available it's skipped (and logged once) instead of breaking the dash.
    public static class Effects
    {
        private static readonly HashSet<string> failed = new HashSet<string>();

        public static void Dust(Vector2 position, int count, float radius) =>
            Try("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(count, radius, -3f, -1f,
                new Vector3?(position), null));

        public static void Pebbles(Vector2 position, int count) =>
            Try("pebbles", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(new int?(count),
                new Vector3?(position), null, null, 0f, null));

        // Rocks thrown out along a direction.
        public static void RockSpray(Vector2 position, Vector2 direction, int count)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            Try("rock spray", () => PoolManager.GetPoolItem<RockBurstDirectionalEffect>().Emit(new int?(count),
                new Vector3?(position), null, new Vector3?(new Vector3(0f, 0f, angle)), 0f, null, null));
        }

        public static void Crack(Vector2 position) =>
            Try("floor crack", () => PoolManager.GetPoolItem<FloorCrackEmitter>().Emit(null, new Vector3?(position),
                null, null, 0f, null, null));

        public static void Shake(float intensity) =>
            Try("camera shake", () => CameraController.ShakeCamera(intensity, false));

        // Freezes the game for a few frames, so a hit lands with weight.
        public static void HitStop(int frames) =>
            Try("hit-stop", () => TimeScaleController.FreezeForFrames(frames, false));

        private static void Try(string name, System.Action effect)
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
                SlingshotDashPlugin.Log($"Effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }
    }
}
