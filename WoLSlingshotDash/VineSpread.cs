using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Enhanced Vine Slingshot: when the vines take hold, more vines spread from there to the
    // enemies nearby, coil round them and snare them (small hits that root, skill level 4) for as
    // long as you hold, then linger for LingerTime after the kick, still rooted where the kick
    // landed. Runs on the plugin, since it outlives the dash.
    public class VineSpread
    {
        private const float Radius = 4f;
        private const int MaxEnemies = 5;
        private const float LingerTime = 3f;
        private const float TickInterval = 0.5f;
        private const int SnareLevel = 4;
        private const float SnareScale = 0.6f;

        private readonly Player player;
        private readonly string skillCategory;
        private readonly string skillID;
        private readonly Enemy hubEnemy;
        private Vector2 hub;
        private readonly List<Enemy> enemies = new List<Enemy>();
        private readonly List<VineLines> lines = new List<VineLines>();
        private float endAt = float.MaxValue;
        private bool lingering;

        private VineSpread(Player player, string skillCategory, string skillID, Enemy hubEnemy, Vector2 hub)
        {
            this.player = player;
            this.skillCategory = skillCategory;
            this.skillID = skillID;
            this.hubEnemy = hubEnemy;
            this.hub = hub;
        }

        // Spreads from the grabbed enemy (or the spot the vines hold) to the nearest enemies
        // around it; null if there are none.
        public static VineSpread Begin(Player player, string skillCategory, string skillID, Enemy target, Vector2 anchor)
        {
            var spread = new VineSpread(player, skillCategory, skillID, target, anchor);
            var found = new List<Enemy>();
            foreach (Collider2D col in Physics2D.OverlapCircleAll(anchor, Radius))
            {
                Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                if (enemy != null && enemy != target && enemy.gameObject.activeInHierarchy && !found.Contains(enemy))
                    found.Add(enemy);
            }
            if (found.Count == 0)
                return null;
            found.Sort((a, b) => Vector2.Distance(a.transform.position, anchor)
                .CompareTo(Vector2.Distance(b.transform.position, anchor)));
            for (int i = 0; i < found.Count && i < MaxEnemies; i++)
            {
                VineLines vine = VineLines.Create(player);
                if (vine == null)
                    continue;
                spread.enemies.Add(found[i]);
                spread.lines.Add(vine);
                vine.Shoot(anchor, found[i].transform.position);
                Effects.Leaves(found[i].transform.position, 4);
            }
            SlingshotDashPlugin.Run(spread.Run());
            return spread;
        }

        // The kick landed (or the dash ended): stay put and let go after LingerTime.
        public void Linger()
        {
            if (lingering)
                return;
            lingering = true;
            if (hubEnemy != null && hubEnemy.gameObject.activeInHierarchy)
                hub = hubEnemy.transform.position;
            endAt = Time.time + LingerTime;
        }

        private IEnumerator Run()
        {
            float nextTick = 0f;
            while (Time.time < endAt && player != null)
            {
                // Rooted to the grabbed enemy while you hold; where the kick landed after.
                if (!lingering && hubEnemy != null && hubEnemy.gameObject.activeInHierarchy)
                    hub = hubEnemy.transform.position;
                bool tick = Time.time >= nextTick;
                if (tick)
                    nextTick = Time.time + TickInterval;
                for (int i = 0; i < enemies.Count; i++)
                {
                    Enemy enemy = enemies[i];
                    VineLines vine = lines[i];
                    if (vine == null)
                        continue;
                    if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    {
                        Object.Destroy(vine.gameObject);
                        lines[i] = null;
                        continue;
                    }
                    Vector2 position = enemy.transform.position;
                    vine.Hold(hub, position);
                    vine.Ensnare(position);
                    if (tick)
                    {
                        try
                        {
                            EarthBurst.CreateBurst(position, skillCategory, skillID, SnareLevel, SnareScale);
                        }
                        catch
                        {
                        }
                    }
                }
                yield return null;
            }
            foreach (VineLines vine in lines)
            {
                if (vine != null)
                    Object.Destroy(vine.gameObject);
            }
            lines.Clear();
            enemies.Clear();
        }
    }
}
