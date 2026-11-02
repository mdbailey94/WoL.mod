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

        // The ELEMENTAL trees, APPROVED by Matt 2026-11-02 ("Go ahead", Log - Player 16:45 / 17:00),
        // each on the Apprentice template. Since the archetype trees below (17:12) they're a parts
        // bin: not mapped to any cloak by default, but usable through [Talents] CloakTrees. Nodes marked "(coming soon)" are in the
        // tree but don't act yet; the rest act through TalentEffects / StatMods.
        private const string Soon = " (coming soon)";

        public static readonly Tree Fire = new Tree
        {
            Key = "Fire",
            Name = "Fire",
            Nodes = new[]
            {
                new Node { Key = "Kindling", Name = "Kindling", Tier = 1, MaxPoints = 2, Effect = "+4% fire arcana damage" },
                new Node { Key = "HotBlood", Name = "Hot Blood", Tier = 1, MaxPoints = 2, Effect = "+4% max health" },
                new Node { Key = "Flicker", Name = "Flicker", Tier = 1, MaxPoints = 1, Effect = "+3% move speed" },
                new Node { Key = "Wildfire", Name = "Wildfire", Tier = 2, MaxPoints = 1, Effect = "Burning enemies take 20% more from all sources" + Soon },
                new Node { Key = "EmberDash", Name = "Ember Dash", Tier = 2, MaxPoints = 1, Effect = "Dashing leaves a short fire trail" + Soon },
                new Node { Key = "HeatSink", Name = "Heat Sink", Tier = 2, MaxPoints = 1, Effect = "-6% fire arcana cooldowns" },
                new Node { Key = "Inferno", Name = "Inferno", Tier = 3, MaxPoints = 1, Effect = "Burns spread to enemies within 2 u when a burning enemy dies" + Soon },
                new Node { Key = "Phoenix", Name = "Phoenix", Tier = 3, MaxPoints = 1,
                    Effect = "Once per floor, a killing blow leaves you burning at 30% health instead, with a 3 s fire aura" + Soon },
            }
        };

        public static readonly Tree Air = new Tree
        {
            Key = "Air",
            Name = "Air",
            Nodes = new[]
            {
                new Node { Key = "Tailwind", Name = "Tailwind", Tier = 1, MaxPoints = 2, Effect = "+3% move speed" },
                new Node { Key = "SharpGust", Name = "Sharp Gust", Tier = 1, MaxPoints = 2, Effect = "+4% air arcana damage" },
                new Node { Key = "LightStep", Name = "Light Step", Tier = 1, MaxPoints = 1, Effect = "+5% dash distance" + Soon },
                new Node { Key = "Slipstream", Name = "Slipstream", Tier = 2, MaxPoints = 1, Effect = "Dashing through an enemy takes 1 s off all cooldowns" + Soon },
                new Node { Key = "Updraft", Name = "Updraft", Tier = 2, MaxPoints = 1, Effect = "+25% knockback" },
                new Node { Key = "SecondBreath", Name = "Second Breath", Tier = 2, MaxPoints = 1, Effect = "-6% air arcana cooldowns" },
                new Node { Key = "CycloneHeart", Name = "Cyclone Heart", Tier = 3, MaxPoints = 1, Effect = "Every 3rd dash throws a small whirlwind" + Soon },
                new Node { Key = "Featherfall", Name = "Featherfall", Tier = 3, MaxPoints = 1,
                    Effect = "You can't be slowed, and take 30% less from traps and hazards" + Soon },
            }
        };

        public static readonly Tree Earth = new Tree
        {
            Key = "Earth",
            Name = "Earth",
            Nodes = new[]
            {
                new Node { Key = "Bedrock", Name = "Bedrock", Tier = 1, MaxPoints = 2, Effect = "+5% max health" },
                new Node { Key = "HeavyHands", Name = "Heavy Hands", Tier = 1, MaxPoints = 2, Effect = "+4% earth arcana damage" },
                new Node { Key = "Grounded", Name = "Grounded", Tier = 1, MaxPoints = 1, Effect = "-5% damage taken while standing still" + Soon },
                new Node { Key = "Stoneskin", Name = "Stoneskin", Tier = 2, MaxPoints = 1, Effect = "The first hit in each room deals 50% less" + Soon },
                new Node { Key = "Tremor", Name = "Tremor", Tier = 2, MaxPoints = 1, Effect = "Earth hits slow enemies 25% for 1 s" + Soon },
                new Node { Key = "Quarry", Name = "Quarry", Tier = 2, MaxPoints = 1, Effect = "-6% earth arcana cooldowns" },
                new Node { Key = "MountainsWrath", Name = "Mountain's Wrath", Tier = 3, MaxPoints = 1,
                    Effect = "The signature charges from damage taken as well as dealt" + Soon },
                new Node { Key = "Avalanche", Name = "Avalanche", Tier = 3, MaxPoints = 1, Effect = "Signature hits stun for 1 s" + Soon },
            }
        };

        public static readonly Tree Lightning = new Tree
        {
            Key = "Lightning",
            Name = "Lightning",
            Nodes = new[]
            {
                new Node { Key = "Static", Name = "Static", Tier = 1, MaxPoints = 2, Effect = "+4% lightning arcana damage" },
                new Node { Key = "Quickened", Name = "Quickened", Tier = 1, MaxPoints = 2, Effect = "+3% move speed" },
                new Node { Key = "Conductor", Name = "Conductor", Tier = 1, MaxPoints = 1, Effect = "+5% critical hit chance" },
                new Node { Key = "ChainSpark", Name = "Chain Spark", Tier = 2, MaxPoints = 1, Effect = "Shocked enemies take 15% more" + Soon },
                new Node { Key = "Overload", Name = "Overload", Tier = 2, MaxPoints = 1, Effect = "Critical hits shock" + Soon },
                new Node { Key = "Capacitor", Name = "Capacitor", Tier = 2, MaxPoints = 1, Effect = "-6% lightning arcana cooldowns" },
                new Node { Key = "StormCaller", Name = "Storm Caller", Tier = 3, MaxPoints = 1, Effect = "Every 5th hit calls down a free bolt" + Soon },
                new Node { Key = "LiveWire", Name = "Live Wire", Tier = 3, MaxPoints = 1, Effect = "Dashing shocks the enemies you pass" + Soon },
            }
        };

        public static readonly Tree Water = new Tree
        {
            Key = "Water",
            Name = "Water / Ice",
            Nodes = new[]
            {
                new Node { Key = "ColdSnap", Name = "Cold Snap", Tier = 1, MaxPoints = 2, Effect = "+4% water arcana damage" },
                new Node { Key = "Tide", Name = "Tide", Tier = 1, MaxPoints = 2, Effect = "+4% max health" },
                new Node { Key = "Glide", Name = "Glide", Tier = 1, MaxPoints = 1, Effect = "+3% move speed" },
                new Node { Key = "Shatter", Name = "Shatter", Tier = 2, MaxPoints = 1, Effect = "Frozen enemies take 25% more" + Soon },
                new Node { Key = "Undertow", Name = "Undertow", Tier = 2, MaxPoints = 1, Effect = "Water hits pull enemies 0.5 u inward" + Soon },
                new Node { Key = "Riptide", Name = "Riptide", Tier = 2, MaxPoints = 1, Effect = "-6% water arcana cooldowns" },
                new Node { Key = "DeepFreeze", Name = "Deep Freeze", Tier = 3, MaxPoints = 1, Effect = "Freezes last 50% longer" + Soon },
                new Node { Key = "Tsunami", Name = "Tsunami", Tier = 3, MaxPoints = 1, Effect = "The signature casts twice at 50% power" + Soon },
            }
        };

        // TREE SET 2, the STAT-ARCHETYPE trees, APPROVED by Matt 2026-11-02 ("Great lets do it",
        // Log - Player 17:05 + the 17:10 v2 changes): each cloak's tree grows the stats it already
        // has. These are the default mapping; the elemental trees above stay as a parts bin.
        public static readonly Tree Bulwark = new Tree
        {
            Key = "Bulwark",
            Name = "Bulwark",
            Nodes = new[]
            {
                new Node { Key = "IronHide", Name = "Iron Hide", Tier = 1, MaxPoints = 2, Effect = "+2% armor" },
                new Node { Key = "ThickSkin", Name = "Thick Skin", Tier = 1, MaxPoints = 2, Effect = "+4% max health" },
                new Node { Key = "Mending", Name = "Mending", Tier = 1, MaxPoints = 1, Effect = "+5% healing received" },
                new Node { Key = "StandFirm", Name = "Stand Firm", Tier = 2, MaxPoints = 1, Effect = "-15% damage taken while standing still or casting" },
                new Node { Key = "Retaliate", Name = "Retaliate", Tier = 2, MaxPoints = 1, Effect = "Being hit adds 10% signature charge (3 s cooldown)" },
                new Node { Key = "ShieldWall", Name = "Shield Wall", Tier = 2, MaxPoints = 1, Effect = "The first hit in each room deals 50% less" + Soon },
                new Node { Key = "Juggernaut", Name = "Juggernaut", Tier = 3, MaxPoints = 1, Effect = "Can't be knocked back or staggered, +10% armor" },
                new Node { Key = "LastStand", Name = "Last Stand", Tier = 3, MaxPoints = 1, Effect = "Below 30% health: -30% damage taken, +20% damage" },
            }
        };

        public static readonly Tree Duelist = new Tree
        {
            Key = "Duelist",
            Name = "Duelist",
            Nodes = new[]
            {
                new Node { Key = "KeenEye", Name = "Keen Eye", Tier = 1, MaxPoints = 2, Effect = "+2% critical hit chance" },
                new Node { Key = "DeepCuts", Name = "Deep Cuts", Tier = 1, MaxPoints = 2, Effect = "+8% critical damage" },
                new Node { Key = "Nimble", Name = "Nimble", Tier = 1, MaxPoints = 1, Effect = "+2% evade" },
                new Node { Key = "Momentum", Name = "Momentum", Tier = 2, MaxPoints = 1, Effect = "A crit takes 0.3 s off all cooldowns (at most twice a second)" },
                new Node { Key = "Exploit", Name = "Exploit", Tier = 2, MaxPoints = 1, Effect = "Crits on burning, frozen or shocked enemies deal 25% more" },
                new Node { Key = "Riposte", Name = "Riposte", Tier = 2, MaxPoints = 1, Effect = "After evading a hit, your next hit crits" },
                new Node { Key = "Assassin", Name = "Assassin", Tier = 3, MaxPoints = 1, Effect = "The first hit on an enemy at full health always crits, for 50% more" },
                new Node { Key = "Flurry", Name = "Flurry", Tier = 3, MaxPoints = 1, Effect = "Each crit gives +3% crit chance for 4 s, stacking to 5" },
            }
        };

        public static readonly Tree Arcanist = new Tree
        {
            Key = "Arcanist",
            Name = "Arcanist",
            Nodes = new[]
            {
                new Node { Key = "Focus", Name = "Focus", Tier = 1, MaxPoints = 2, Effect = "-3% cooldowns" },
                new Node { Key = "Overflow", Name = "Overflow", Tier = 1, MaxPoints = 2, Effect = "+5% signature charge" },
                new Node { Key = "Potency", Name = "Potency", Tier = 1, MaxPoints = 1, Effect = "+3% damage" },
                new Node { Key = "Echo", Name = "Echo", Tier = 2, MaxPoints = 1, Effect = "10% chance a cast doesn't start its cooldown" },
                new Node { Key = "Surge", Name = "Surge", Tier = 2, MaxPoints = 1, Effect = "Casting your signature resets the dash and gives +15% damage for 4 s" },
                new Node { Key = "Channel", Name = "Channel", Tier = 2, MaxPoints = 1, Effect = "After standing still 1 s, cooldowns recover 10% faster" + Soon },
                new Node { Key = "Archmage", Name = "Archmage", Tier = 3, MaxPoints = 1, Effect = "Every 4th standard cast has no cooldown" },
                new Node { Key = "Cataclysm", Name = "Cataclysm", Tier = 3, MaxPoints = 1, Effect = "Your signature deals 40% more" },
            }
        };

        public static readonly Tree Windrunner = new Tree
        {
            Key = "Windrunner",
            Name = "Windrunner",
            Nodes = new[]
            {
                new Node { Key = "Fleet", Name = "Fleet", Tier = 1, MaxPoints = 2, Effect = "+3% move speed" },
                new Node { Key = "QuickStart", Name = "Quick Start", Tier = 1, MaxPoints = 2, Effect = "The sprint builds up 15% faster" + Soon },
                new Node { Key = "LongStride", Name = "Long Stride", Tier = 1, MaxPoints = 1, Effect = "+5% dash distance" + Soon },
                new Node { Key = "Tailspin", Name = "Tailspin", Tier = 2, MaxPoints = 1, Effect = "Dashing through an enemy knocks it aside and slows it 30% for 1.5 s" + Soon },
                new Node { Key = "HitAndRun", Name = "Hit and Run", Tier = 2, MaxPoints = 1, Effect = "+15% damage for 2 s after a dash" + Soon },
                new Node { Key = "Evasive", Name = "Evasive", Tier = 2, MaxPoints = 1, Effect = "+5% evade while sprinting" + Soon },
                new Node { Key = "Afterimage", Name = "Afterimage", Tier = 3, MaxPoints = 1, Effect = "A dash leaves a decoy that enemies attack for 1.5 s" + Soon },
                new Node { Key = "Velocity", Name = "Velocity", Tier = 3, MaxPoints = 1, Effect = "+1% damage for every 1% move speed above base" + Soon },
            }
        };

        public static readonly Tree Mender = new Tree
        {
            Key = "Mender",
            Name = "Mender",
            Nodes = new[]
            {
                new Node { Key = "Vitality", Name = "Vitality", Tier = 1, MaxPoints = 2, Effect = "+4% max health" },
                new Node { Key = "Remedy", Name = "Remedy", Tier = 1, MaxPoints = 2, Effect = "+8% healing" },
                new Node { Key = "LuckyHeal", Name = "Lucky Heal", Tier = 1, MaxPoints = 1, Effect = "+3% heal critical chance" },
                new Node { Key = "Regrowth", Name = "Regrowth", Tier = 2, MaxPoints = 1, Effect = "In a cleared room, regenerate 1% max health a second, up to 50%" + Soon },
                new Node { Key = "Leech", Name = "Leech", Tier = 2, MaxPoints = 1, Effect = "Kills heal 1% max health" + Soon },
                new Node { Key = "Overheal", Name = "Overheal", Tier = 2, MaxPoints = 1, Effect = "Healing past max becomes a 5 s shield, up to 10% max health" + Soon },
                new Node { Key = "Sanctuary", Name = "Sanctuary", Tier = 3, MaxPoints = 1,
                    Effect = "Casting your signature drops a 4 s healing circle (3% max health a second to wizards inside)" + Soon },
                new Node { Key = "Lifebloom", Name = "Lifebloom", Tier = 3, MaxPoints = 1, Effect = "Your healing also heals the other wizard for 50% (co-op)" + Soon },
            }
        };

        public static readonly Tree Gambler = new Tree
        {
            Key = "Gambler",
            Name = "Gambler",
            Nodes = new[]
            {
                new Node { Key = "Fortune", Name = "Fortune", Tier = 1, MaxPoints = 2, Effect = "+8% gold" },
                new Node { Key = "GemSense", Name = "Gem Sense", Tier = 1, MaxPoints = 2, Effect = "+5% chance of an extra chaos gem from bosses" + Soon },
                new Node { Key = "Nerve", Name = "Nerve", Tier = 1, MaxPoints = 1, Effect = "+3% damage" },
                new Node { Key = "HighRoller", Name = "High Roller", Tier = 2, MaxPoints = 1, Effect = "Relics cost 10% less" + Soon },
                new Node { Key = "HotStreak", Name = "Hot Streak", Tier = 2, MaxPoints = 1, Effect = "Each kill without taking damage: +2% damage, up to +20%, lost when hit" },
                new Node { Key = "Jackpot", Name = "Jackpot", Tier = 2, MaxPoints = 1, Effect = "Elites drop 25% more gold" + Soon },
                new Node { Key = "AllIn", Name = "All In", Tier = 3, MaxPoints = 1, Effect = "+25% damage, -25% max health" },
                new Node { Key = "DoubleOrNothing", Name = "Double or Nothing", Tier = 3, MaxPoints = 1,
                    Effect = "Each floor cleared without being hit doubles the next floor's gold (up to 4x); a hit resets it" },
            }
        };

        public static readonly Tree[] Trees = { Apprentice, Bulwark, Duelist, Arcanist, Windrunner, Mender, Gambler, Fire, Air, Earth, Lightning, Water };

        public const int PointsToOpenNextTier = 3;
        private static ConfigEntry<bool> enabled;
        private static ConfigEntry<string> cloakTrees;
        private static string parsedFrom;
        private static readonly Dictionary<string, Tree> treeOfCloak = new Dictionary<string, Tree>();

        public static bool Enabled => enabled != null && enabled.Value;

        public static void Bind(ConfigFile config)
        {
            enabled = config.Bind("Talents", "Enabled", false,
                "Phase 1 preview: the character panel's TALENTS tab and cloak talent trees (1 point per wizard level, " +
                "spent per cloak, free respec in the house). Off: the tab stays locked and no talent does anything.");
            // Matt's stat-archetype mapping (approved 2026-11-02, Log - Player 17:05): each cloak
            // to the tree of its strongest stat. Hope, Patience and Normal keep Apprentice.
            cloakTrees = config.Bind("Talents", "CloakTrees",
                "Vigor=Bulwark, Grit=Bulwark, Rule=Bulwark, Awe=Duelist, Switch=Duelist, DuelistCloak=Duelist, " +
                "Tempo=Arcanist, Fury=Arcanist, Pace=Windrunner, Venture=Windrunner, Savile=Windrunner, Pink=Mender, " +
                "Greed=Gambler, Level=Gambler, Fall=Gambler, Pride=Gambler",
                "Which talent tree each cloak uses: Cloak=Tree pairs. Trees: Bulwark, Duelist, Arcanist, Windrunner, Mender, " +
                "Gambler, Apprentice, and the elemental Fire, Air, Earth, Lightning, Water. " +
                "Cloaks not listed use the Apprentice tree. Points are kept per cloak, so moving a cloak to another tree " +
                "leaves its old points unused until it moves back (or a respec).");
        }

        // The tree a cloak uses ([Talents] CloakTrees, else Apprentice).
        public static Tree TreeFor(string cloak)
        {
            Parse();
            Tree tree;
            return cloak != null && treeOfCloak.TryGetValue(cloak, out tree) ? tree : Apprentice;
        }

        private static void Parse()
        {
            string value = cloakTrees != null ? cloakTrees.Value ?? "" : "";
            if (value == parsedFrom)
                return;
            parsedFrom = value;
            treeOfCloak.Clear();
            foreach (string pair in value.Split(','))
            {
                string[] parts = pair.Split('=');
                if (parts.Length != 2)
                    continue;
                string cloak = parts[0].Trim(), name = parts[1].Trim();
                foreach (Tree tree in Trees)
                    if (string.Equals(tree.Key, name, StringComparison.OrdinalIgnoreCase))
                        treeOfCloak[cloak] = tree;
            }
        }

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

        // The rank the effects read: 0 when talents are off, the node isn't learned or isn't in
        // the cloak's tree.
        public static int Rank(CharacterSave save, string cloak, string node) =>
            Enabled && Find(TreeFor(cloak), node) != null ? Points(save, cloak, node) : 0;

        // The same for a wizard: its character's points in its current cloak (TalentEffects).
        public static int Rank(Player player, string node)
        {
            if (!Enabled || player == null || ChaosDepthsPlugin.Instance == null)
                return 0;
            try
            {
                // Only nodes of the cloak's own tree count (points left from another tree don't).
                string cloak = CloakOf(player);
                if (Find(TreeFor(cloak), node) == null)
                    return 0;
                return Points(ChaosDepthsPlugin.Instance.SaveOf(player), cloak, node);
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
