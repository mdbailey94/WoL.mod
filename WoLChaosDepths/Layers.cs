using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLChaosDepths
{
    // M4, second slice: layer bosses and rifts.
    //
    // Layer bosses: every few depths (default every 5), the realm boss met at that depth is a
    // layer boss: more health again, a tint, more experience, and a Legendary drop for each
    // player who hurt it.
    //
    // Rifts: a trial floor can open as a rift (default 1 in 4). For a few minutes the floor's
    // elite chance and loot quality are raised; leave the floor through its exit before the rift
    // collapses and each player gets a bonus item of Rare or better. A timer is drawn at the top
    // of the screen while a rift is open.
    public static class Layers
    {
        private const string Prefix = "ChaosDepthsLayer_";
        private static readonly Color LayerTint = new Color(1f, 0.55f, 0.9f);

        private static ConfigEntry<int> layerEvery;
        private static ConfigEntry<float> layerHealth, layerXp;
        private static ConfigEntry<float> riftChance, riftSeconds, riftEliteBonus, riftRarityBonus;
        private static ConfigEntry<int> riftRewardMinRarity;

        private static readonly HashSet<int> layerBosses = new HashSet<int>();
        private static readonly Dictionary<int, Entity> layerEntities = new Dictionary<int, Entity>();

        // The open rift, if any: when it collapses, and whether this floor was a rift at all.
        private static bool riftOpen;
        private static float riftEndsAt;
        private static int riftDepth;
        private static bool riftDecidedForThisFloor;

        public static void Bind(ConfigFile config)
        {
            layerEvery = config.Bind("Layers", "LayerEvery", 5,
                new ConfigDescription("Every this many depths, the realm boss is a layer boss (0 = never).", new AcceptableValueRange<int>(0, 50)));
            layerHealth = config.Bind("Layers", "LayerBossHealth", 0.5f, "Extra health for a layer boss, on top of depth (0.5 = +50%).");
            layerXp = config.Bind("Layers", "LayerBossXp", 2f, "Experience multiplier for a layer boss, on top of a boss's usual.");
            riftChance = config.Bind("Rifts", "Chance", 0.25f,
                new ConfigDescription("Chance a trial floor (not a boss arena) opens as a rift.", new AcceptableValueRange<float>(0f, 1f)));
            riftSeconds = config.Bind("Rifts", "Seconds", 180f, "How long a rift stays open.");
            riftEliteBonus = config.Bind("Rifts", "EliteChanceBonus", 0.25f, "Added to the elite chance while a rift is open (0.25 = +25 points).");
            riftRarityBonus = config.Bind("Rifts", "RarityBonus", 0.5f, "Added to Magic-and-better weight while a rift is open (0.5 = +50%).");
            riftRewardMinRarity = config.Bind("Rifts", "RewardMinRarity", 2,
                "Each player's bonus item for leaving a rift in time is at least this rarity (0 Common, 1 Magic, 2 Rare, 3 Legendary).");
        }

        // ---- Layer bosses ----

        public static bool IsLayerDepth(int depth) => layerEvery.Value > 0 && depth > 0 && depth % layerEvery.Value == 0;

        public static bool IsLayerBoss(Entity e) => e != null && layerBosses.Contains(e.GetInstanceID());

        public static float XpMultiplier(Entity e) => IsLayerBoss(e) ? layerXp.Value : 1f;

        // From the game's boss spawn event (and Depth's scan as a fallback): a realm boss met at
        // a layer depth becomes a layer boss.
        public static void OnBossSpawn(Entity e, int depth)
        {
            try
            {
                if (!(e is Boss) || !IsLayerDepth(depth) || e.health == null || e.health.healthStat == null)
                    return;
                int id = e.GetInstanceID();
                if (!layerBosses.Add(id))
                    return;
                layerEntities[id] = e;
                float before = e.health.healthStat.ModifiedValue;
                // Depth owns the boss's health formula (its override is scaled in place and
                // re-applied when the game resets it); the layer bonus is one more factor in it.
                Depth.SetExtraHealthFactor(e, 1f + layerHealth.Value);
                NumVarStat stat = e.health.healthStat;
                if (e.spriteRenderer != null)
                    e.spriteRenderer.color = LayerTint;
                string name = e.GetType().Name;
                try
                {
                    var boss = e as Boss;
                    if (boss != null && !string.IsNullOrEmpty(boss.BossDisplayName))
                        name = boss.BossDisplayName;
                }
                catch
                {
                }
                ChaosDepthsPlugin.Log($"Layer boss at depth {depth}: {name}, max health {before:0} -> {stat.ModifiedValue:0} (+{layerHealth.Value:P0} on top of depth)");
                ChaosDepthsPlugin.Notice($"Layer {depth / Mathf.Max(1, layerEvery.Value)} boss: {name}", 4f);
            }
            catch (Exception ex)
            {
                ChaosDepthsPlugin.LogError($"layer boss: {ex.Message}");
            }
        }

        // ---- Rifts ----

        public static bool RiftOpen => riftOpen && Time.time < riftEndsAt;
        public static float RiftSecondsLeft => RiftOpen ? riftEndsAt - Time.time : 0f;
        public static float EliteChanceBonus => RiftOpen ? riftEliteBonus.Value : 0f;
        public static float RarityBonus => RiftOpen ? riftRarityBonus.Value : 0f;

        // A new trial floor (not a boss arena) has loaded: roll for a rift.
        public static void OnFloor(int depth, bool bossArena)
        {
            layerBosses.Clear();
            layerEntities.Clear();
            riftOpen = false;
            riftDecidedForThisFloor = true;
            if (bossArena || riftChance.Value <= 0f || UnityEngine.Random.value >= riftChance.Value)
                return;
            Open(depth, "opened");
        }

        // The floor is being left: a rift still open pays out; one that collapsed doesn't.
        public static void OnLeaveFloor(IEnumerable<Player> players, Action<Player, int> reward)
        {
            if (!riftOpen)
                return;
            bool inTime = Time.time < riftEndsAt;
            riftOpen = false;
            if (!inTime)
            {
                ChaosDepthsPlugin.Log($"Rift at depth {riftDepth}: collapsed before the floor was cleared; no reward");
                return;
            }
            int rewarded = 0;
            if (players != null)
                foreach (Player p in players)
                {
                    if (p == null)
                        continue;
                    reward(p, riftRewardMinRarity.Value);
                    rewarded++;
                }
            ChaosDepthsPlugin.Log($"Rift at depth {riftDepth}: cleared with {riftEndsAt - Time.time:0} s to spare; {rewarded} reward(s)");
            ChaosDepthsPlugin.Notice("Rift cleared!", 3f);
        }

        // Debug: open a rift on this floor now.
        public static void ForceOpen(int depth)
        {
            Open(depth, "forced open");
        }

        private static void Open(int depth, string how)
        {
            riftOpen = true;
            riftDepth = depth;
            riftEndsAt = Time.time + riftSeconds.Value;
            ChaosDepthsPlugin.Log($"Rift {how} at depth {depth}: {riftSeconds.Value:0} s, elite chance +{riftEliteBonus.Value:P0}, rarity +{riftRarityBonus.Value:P0}");
            ChaosDepthsPlugin.Notice($"A rift opens! Clear the floor in {Mathf.RoundToInt(riftSeconds.Value / 60f)} min", 4f);
        }

        // Twice a second: keep the layer tint on, and log the collapse once.
        public static void Tick()
        {
            foreach (KeyValuePair<int, Entity> pair in layerEntities)
                if (pair.Value != null && pair.Value.spriteRenderer != null && pair.Value.health != null && pair.Value.health.CurrentHealthValue > 0)
                    pair.Value.spriteRenderer.color = LayerTint;
            if (riftOpen && Time.time >= riftEndsAt)
            {
                riftOpen = false;
                ChaosDepthsPlugin.Log($"Rift at depth {riftDepth}: collapsed");
                ChaosDepthsPlugin.Notice("The rift collapses", 3f);
            }
        }

        // The rift timer, top centre.
        public static void Draw()
        {
            if (!RiftOpen)
                return;
            try
            {
                int u = Mathf.Max(3, Mathf.FloorToInt(Screen.height / 240f));
                int font = Mathf.Max(12, 4 * u);
                float left = RiftSecondsLeft;
                string text = $"RIFT  {Mathf.FloorToInt(left / 60f)}:{Mathf.FloorToInt(left % 60f):00}";
                GUIStyle style = PixelUI.Style(font, left < 30f ? PixelUI.Accent : PixelUI.Text, TextAnchor.MiddleCenter);
                float w = 40 * u, h = 8 * u;
                var box = new Rect((Screen.width - w) / 2f, 3 * u, w, h);
                PixelUI.Fill(box, new Color(0f, 0f, 0f, 0.55f));
                PixelUI.Fill(new Rect(box.x, box.yMax - u, w * Mathf.Clamp01(left / Mathf.Max(1f, riftSeconds.Value)), u), left < 30f ? PixelUI.Accent : PixelUI.Dim);
                GUI.Label(box, text, style);
            }
            catch
            {
            }
        }
    }
}
