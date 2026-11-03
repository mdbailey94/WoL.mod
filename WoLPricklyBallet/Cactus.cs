using UnityEngine;

namespace WoLPricklyBallet
{
    // The cactus (#77, Matt): the base game's own, from its Earth arcana Prickly Ballet
    // (UsePlantBarrage): its PlantBarrage prefab with its animations, needle spray and hit box.
    // Here it stands still where the dash started (no trips out and back) and needles every
    // enemy in its reach each HitEvery seconds for its lifetime, with this arcana's stats (level
    // 1), then plays its own death animation and goes.
    internal static class Cactus
    {
        public static void Spawn(Player owner, string skillID, Vector2 at, float lifetime, bool enhanced)
        {
            try
            {
                if (PlantBarrage.Prefab == null)
                {
                    PricklyBalletPlugin.Log("Cactus Tango: the game's cactus prefab wasn't found");
                    return;
                }
                float every = PricklyBalletPlugin.HitEvery;
                int hits = Mathf.Max(1, Mathf.RoundToInt(lifetime / every));
                PlantBarrage cactus = Globals.ChaosInst<PlantBarrage>(PlantBarrage.Prefab, null, at);
                cactus.attack.SetAttackInfo(owner.skillCategory, skillID, 1, false);
                // From and to the same spot with no trips: it stays put.
                cactus.Initialize(enhanced, at, at, 0, hits, every);
                PricklyBalletPlugin.Log($"Cactus Tango: cactus at ({at.x:0.0}, {at.y:0.0}), {hits} hits every {every:0.0#} s");
            }
            catch (System.Exception e)
            {
                PricklyBalletPlugin.Log($"Cactus Tango: couldn't raise the cactus: {e.Message}");
            }
        }
    }
}
