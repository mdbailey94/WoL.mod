using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace WoLChaosDepths
{
    // Phase 1a (Roadmap): cloak talent trees. A wizard earns 1 talent point per level; points are
    // spent per cloak (each cloak's own, no carry-over: Matt, 2026-11-01), so swapping cloak swaps
    // build, and a respec in the house is free. Every tree has 3 tiers: tier 2 opens with 3 points
    // in tier 1, tier 3 with 3 points in tier 2, and tier 3 takes exactly one keystone.
    //
    // This file is the trees as data, the rules and the save model only. What each node does in
    // a run is read through Rank() by the effects (WoL Modder's part). Behind [Talents] Enabled,
    // off by default until Phase 1 is played.
    public static class Talents
    {
        public class Node
        {
            public string Key;      // saved, never renamed
            public string Name;
            public int Tier;        // 1-3; tier 3 nodes are keystones (pick one)
            public int MaxPoints;
            public string Effect;   // per point, as the Talent Trees tab words it
        }

        public class Tree
        {
            public string Key;
            public string Name;
            public Node[] Nodes;
        }

        // TREE 1, APPROVED (Talent Trees tab, 2026-11-01): the starter cloaks, and any cloak
        // without a tree of its own.
        public static readonly Tree Apprentice = new Tree
        {
            Key = "Apprentice",
            Name = "Apprentice",
            Nodes = new[]
            {
                new Node { Key = "Sturdy", Name = "Sturdy", Tier = 1, MaxPoints = 2, Effect = "+4% max health" },
                new Node { Key = "Keen", Name = "Keen", Tier = 1, MaxPoints = 2, Effect = "+3% damage (all arcana)" },
                new Node { Key = "QuickFeet", Name = "Quick Feet", Tier = 1, MaxPoints = 1, Effect = "+3% move speed" },
                new Node { Key = "SecondWind", Name = "Second Wind", Tier = 2, MaxPoints = 1,
                    Effect = "After taking damage, regain 2% max health over 3 s (6 s cooldown)" },
                new Node { Key = "SteadyHands", Name = "Steady Hands", Tier = 2, MaxPoints = 1, Effect = "-5% all arcana cooldowns" },
                new Node { Key = "PocketChange", Name = "Pocket Change", Tier = 2, MaxPoints = 1, Effect = "+15% gold found in runs" },
                new Node { Key = "Graduate", Name = "Graduate", Tier = 3, MaxPoints = 1, Effect = "+1 relic slot at the start of each run" },
                new Node { Key = "Survivor", Name = "Survivor", Tier = 3, MaxPoints = 1,
                    Effect = "Once per stage (floor), a killing blow leaves you at 1 health instead" },
            }
        };

        public const int PointsToOpenNextTier = 3;
        private static ConfigEntry<bool> enabled;

        public static bool Enabled => enabled != null && enabled.Value;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Talents", "Enabled", false,
                "Phase 1 preview: the character panel's TALENTS tab and cloak talent trees (1 point per wizard level, " +
                "spent per cloak, free respec in the house). Off: the tab stays locked and no talent does anything.");
        }

        // The tree a cloak uses. Only the Apprentice tree exists so far; elemental cloaks get
        // their own as they're approved.
        public static Tree TreeFor(string cloak) => Apprentice;

        public static Node Find(Tree tree, string key)
        {
            foreach (Node node in tree.Nodes)
                if (node.Key == key)
                    return node;
            return null;
        }

        // ---- The save ----

        // This cloak's points in a node (0 if none).
        public static int Points(CharacterSave save, string cloak, string node)
        {
            if (save?.talents == null || string.IsNullOrEmpty(cloak))
                return 0;
            foreach (TalentData t in save.talents)
                if (t.cloak == cloak && t.node == node)
                    return t.points;
            return 0;
        }

        public static int Spent(CharacterSave save, string cloak, int tier = 0)
        {
            int spent = 0;
            if (save?.talents == null)
                return 0;
            foreach (TalentData t in save.talents)
            {
                if (t.cloak != cloak)
                    continue;
                Node node = Find(TreeFor(cloak), t.node);
                if (node != null && (tier == 0 || node.Tier == tier))
                    spent += t.points;
            }
            return spent;
        }

        // 1 point per wizard level (level 1 has 1), less what this cloak has spent.
        public static int Unspent(CharacterSave save, string cloak) =>
            save == null ? 0 : Math.Max(0, save.level - Spent(save, cloak));

        // The rank the effects read: 0 when talents are off or the node isn't learned.
        public static int Rank(CharacterSave save, string cloak, string node) =>
            Enabled ? Points(save, cloak, node) : 0;

        // The same for a wizard: its character's points in its current cloak (TalentEffects).
        public static int Rank(Player player, string node)
        {
            if (!Enabled || player == null || ChaosDepthsPlugin.Instance == null)
                return 0;
            try
            {
                return Points(ChaosDepthsPlugin.Instance.SaveOf(player), CloakOf(player), node);
            }
            catch
            {
                return 0;
            }
        }

        // The cloak a wizard wears: the game's outfit key ("Chaos" for the Normal cloak, which
        // counts as its own cloak here).
        public static string CloakOf(Player player)
        {
            string id = player != null ? player.outfitID : null;
            return string.IsNullOrEmpty(id) ? Outfit.normalID : id;
        }

        // A cloak's name for the panel.
        public static string CloakName(string cloak)
        {
            try
            {
                Outfit outfit;
                if (Outfit.OutfitDict != null && Outfit.OutfitDict.TryGetValue(cloak, out outfit) && outfit != null)
                {
                    string name = TextManager.GetOutfitName(outfit.outfitID);
                    if (!string.IsNullOrEmpty(name))
                        return name;
                }
            }
            catch
            {
            }
            return cloak == Outfit.normalID ? "Normal" : cloak;
        }

        public static bool TierOpen(CharacterSave save, string cloak, int tier) =>
            tier <= 1 || Spent(save, cloak, tier - 1) >= PointsToOpenNextTier;

        // Why a point can't go in this node now, or null if it can.
        public static string CantLearn(CharacterSave save, string cloak, Node node)
        {
            if (node == null)
                return "no such talent";
            if (Points(save, cloak, node.Key) >= node.MaxPoints)
                return "maxed";
            if (Unspent(save, cloak) <= 0)
                return "no points left";
            if (!TierOpen(save, cloak, node.Tier))
                return $"needs {PointsToOpenNextTier} in tier {node.Tier - 1}";
            if (node.Tier == 3 && Spent(save, cloak, 3) > 0)
                return "one keystone only";
            return null;
        }

        public static bool Learn(CharacterSave save, string cloak, Node node)
        {
            if (CantLearn(save, cloak, node) != null)
                return false;
            foreach (TalentData t in save.talents)
            {
                if (t.cloak == cloak && t.node == node.Key)
                {
                    t.points++;
                    return true;
                }
            }
            save.talents.Add(new TalentData { cloak = cloak, node = node.Key, points = 1 });
            return true;
        }

        // Free respec (in the house): every point this cloak spent comes back.
        public static int Respec(CharacterSave save, string cloak)
        {
            int refunded = Spent(save, cloak);
            save.talents.RemoveAll(t => t.cloak == cloak);
            return refunded;
        }
    }
}
