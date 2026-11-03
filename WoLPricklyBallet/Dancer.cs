using System.Collections.Generic;
using UnityEngine;

namespace WoLPricklyBallet
{
    // WoL Art's frames (Log - Art 2026-11-02 20:11): 24x32 dancer frames with the feet at the
    // bottom centre (x 12, y 31), and an 8x3 needle pointing right. 16 px per unit, Point filter.
    internal static class DancerArt
    {
        public static Sprite[] Rise, Spin, Burst, Needle;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Rise = Frames("prickly_dancer_rise.png", 24, new Vector2(0.5f, 1f / 32f));
            Spin = Frames("prickly_dancer_spin.png", 24, new Vector2(0.5f, 1f / 32f));
            Burst = Frames("prickly_dancer_burst.png", 24, new Vector2(0.5f, 1f / 32f));
            Needle = Frames("prickly_needle.png", 8, new Vector2(0.5f, 0.5f));
            log?.LogInfo($"Prickly Ballet art: rise {Rise.Length}, spin {Spin.Length}, burst {Burst.Length}, needle {Needle.Length} frames");
        }

        private static Sprite[] Frames(string file, int width, Vector2 pivot)
        {
            Texture2D texture = PricklyBalletPlugin.LoadTexture(file);
            if (texture == null || texture.width < width)
                return new Sprite[0];
            var frames = new Sprite[texture.width / width];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(texture, new Rect(i * width, 0, width, texture.height), pivot, 16f);
            return frames;
        }
    }

    // The cactus dancer: rises out of the ground (4 frames), pirouettes for its lifetime (8-frame
    // loop) pricking every enemy within Reach every PrickEvery seconds, then bursts (4 frames) into
    // a ring of needles. The hits are the game's own bursts with this arcana's stats (levels 1 and
    // 2), so gear, crits and talents apply as to any arcana.
    internal class Dancer : MonoBehaviour
    {
        private const float RiseFps = 12f, SpinFps = 12f, BurstFps = 12f;

        private Player owner;
        private string category, skillID;
        private float lifetime, born, nextPrick;
        private SpriteRenderer body;
        private bool bursting;
        private float burstAt;

        public static void Spawn(Player owner, string skillID, Vector2 at, float lifetime)
        {
            var go = new GameObject("PricklyBallet Dancer");
            go.transform.position = at;
            Dancer d = go.AddComponent<Dancer>();
            d.owner = owner;
            d.category = owner.skillCategory;
            d.skillID = skillID;
            d.lifetime = lifetime;
            d.born = Time.time;
            d.nextPrick = Time.time + DancerArt.Rise.Length / RiseFps;
            d.body = go.AddComponent<SpriteRenderer>();
            Sort(d.body, owner);
            Sound("RockExplode", at, 1.3f);
            PricklyBalletPlugin.Log($"Prickly Ballet: dancer at ({at.x:0.0}, {at.y:0.0}) for {lifetime:0.0} s");
        }

        // Drawn on the wizard's own sorting layer, so it sits among the wizards and enemies.
        private static void Sort(SpriteRenderer r, Player owner)
        {
            SpriteRenderer wizard = owner != null ? owner.GetComponentInChildren<SpriteRenderer>() : null;
            if (wizard != null)
            {
                r.sortingLayerID = wizard.sortingLayerID;
                r.sortingOrder = wizard.sortingOrder;
            }
        }

        private void Update()
        {
            float age = Time.time - born;
            float riseTime = DancerArt.Rise.Length / RiseFps;
            if (!bursting && age >= riseTime + lifetime)
                StartBurst();
            if (bursting)
            {
                int f = (int)((Time.time - burstAt) * BurstFps);
                if (f >= DancerArt.Burst.Length)
                {
                    Destroy(gameObject);
                    return;
                }
                body.sprite = DancerArt.Burst[f];
                return;
            }
            if (age < riseTime)
            {
                body.sprite = Frame(DancerArt.Rise, (int)(age * RiseFps));
                return;
            }
            body.sprite = Frame(DancerArt.Spin, (int)((age - riseTime) * SpinFps) % Mathf.Max(1, DancerArt.Spin.Length));
            if (Time.time >= nextPrick)
            {
                nextPrick = Time.time + PricklyBalletPlugin.PrickEvery;
                Hit(transform.position, 1, PricklyBalletPlugin.Reach, quiet: true);
            }
        }

        private static Sprite Frame(Sprite[] frames, int i) =>
            frames.Length == 0 ? null : frames[Mathf.Clamp(i, 0, frames.Length - 1)];

        private void StartBurst()
        {
            bursting = true;
            burstAt = Time.time;
            Sound("RockExplode", transform.position, 1.6f);
            int count = PricklyBalletPlugin.Needles;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Needle.Fire(this, (Vector2)transform.position + Vector2.up * 0.6f, new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)));
            }
        }

        // A game burst of this arcana's stats: radius = the burst's scale (ElementalBurst sets the
        // collider radius to it).
        // A quiet one draws nothing (the pricks: the dancer's spin is their look).
        internal void Hit(Vector2 at, int level, float radius, bool quiet = false)
        {
            try
            {
                NeutralBurst burst = NeutralBurst.CreateBurst(at, category, skillID, level, radius);
                if (quiet && burst != null)
                {
                    foreach (Renderer r in burst.GetComponentsInChildren<Renderer>(true))
                        r.enabled = false;
                    // Stopped rather than switched off, so the Attack child is never touched.
                    foreach (ParticleSystem p in burst.GetComponentsInChildren<ParticleSystem>(true))
                    {
                        ParticleSystem.EmissionModule emission = p.emission;
                        emission.enabled = false;
                        p.Clear();
                    }
                }
            }
            catch (System.Exception e)
            {
                PricklyBalletPlugin.Log($"Prickly Ballet: a hit failed: {e.Message}");
            }
        }

        private static void Sound(string name, Vector2 at, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch);
            }
            catch
            {
            }
        }

        // One needle: flies straight out NeedleRange, stops at a wall, and hits the first enemy it
        // reaches (one hit each).
        private class Needle : MonoBehaviour
        {
            private Dancer from;
            private Vector2 dir;
            private float travelled;
            private SpriteRenderer look;

            public static void Fire(Dancer from, Vector2 at, Vector2 dir)
            {
                var go = new GameObject("PricklyBallet Needle");
                go.transform.position = at;
                go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
                Needle n = go.AddComponent<Needle>();
                n.from = from;
                n.dir = dir;
                n.look = go.AddComponent<SpriteRenderer>();
                n.look.sprite = Frame(DancerArt.Needle, 0);
                Sort(n.look, from.owner);
                // Outlives the dancer's burst.
                Destroy(go, 3f);
            }

            private void Update()
            {
                float step = PricklyBalletPlugin.NeedleSpeed * Time.deltaTime;
                Vector2 at = transform.position;
                if (Physics2D.Raycast(at, dir, step + 0.1f, ChaosCollisions.layerAllWallAndObst).collider != null)
                {
                    Destroy(gameObject);
                    return;
                }
                at += dir * step;
                transform.position = at;
                travelled += step;
                look.sprite = Frame(DancerArt.Needle, (int)(Time.time * 12f) % Mathf.Max(1, DancerArt.Needle.Length));
                GameObject enemy = NearestEnemy(at, 0.45f);
                if (enemy != null && from != null)
                {
                    from.Hit(enemy.transform.position, 2, 0.35f);
                    Destroy(gameObject);
                    return;
                }
                if (travelled >= PricklyBalletPlugin.NeedleRange)
                    Destroy(gameObject);
            }

            private static GameObject NearestEnemy(Vector2 at, float within)
            {
                if (GameController.enemies == null)
                    return null;
                GameObject best = null;
                float bestSq = within * within;
                foreach (GameObject go in GameController.enemies)
                {
                    if (go == null || !go.activeInHierarchy)
                        continue;
                    float sq = ((Vector2)go.transform.position - at).sqrMagnitude;
                    if (sq < bestSq)
                    {
                        bestSq = sq;
                        best = go;
                    }
                }
                return best;
            }
        }
    }
}
