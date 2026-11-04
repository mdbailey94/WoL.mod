using UnityEngine;

namespace WoLPricklyBallet
{
    // The cactus (#77, Matt): the base game's own, from its Earth arcana Prickly Ballet
    // (UsePlantBarrage): its PlantBarrage prefab with its animations, needle spray and hit box.
    // Here it rises where the dash started and (0.3.0) follows the wizard, needling every
    // enemy in its reach each HitEvery seconds for its lifetime, with this arcana's stats (level
    // 1), then plays its own death animation and goes.
    // Keeps the cactus trailing just behind the wizard, on the side they're moving away from.
    internal class CactusFollow : MonoBehaviour
    {
        private const float Catch = 14f;   // how quickly it catches up (per second)
        public Player owner;
        private Vector2 behind = Vector2.down;

        private void LateUpdate()
        {
            if (owner == null || owner.health == null || owner.health.CurrentHealthValue <= 0)
                return;
            Vector2 wizard = owner.transform.position;
            Vector2 move = owner.rigidbody2D != null ? owner.rigidbody2D.velocity : Vector2.zero;
            if (move.sqrMagnitude > 0.25f)
                behind = -move.normalized;
            Vector2 target = wizard + behind * PricklyBalletPlugin.FollowDistance;
            Vector2 now = transform.position;
            transform.position = Vector2.Lerp(now, target, 1f - Mathf.Exp(-Catch * Time.deltaTime));
        }
    }

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
                // From and to the same spot with no trips (the game never moves it again).
                cactus.Initialize(enhanced, at, at, 0, hits, every);
                // 0.3.0 (Matt, 11-04): it follows the wizard for its lifetime. With no trips the
                // game's cactus only places itself on its first physics step, so this moves it.
                if (PricklyBalletPlugin.Follow)
                    cactus.gameObject.AddComponent<CactusFollow>().owner = owner;
                PricklyBalletPlugin.Log($"Cactus Tango: cactus at ({at.x:0.0}, {at.y:0.0}), {hits} hits every {every:0.0#} s");
            }
            catch (System.Exception e)
            {
                PricklyBalletPlugin.Log($"Cactus Tango: couldn't raise the cactus: {e.Message}");
            }
        }
    }
}
