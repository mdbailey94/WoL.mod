using System;
using System.Reflection;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace WoLChaosDepths
{
    // The Ascension mod's level and its table (WoLAscension.AscensionLevels), read by reflection
    // so Chaos Depths runs without it (#27: the approved 1-10 table, with depths gone). The
    // run's level while a run is on (it is locked at the portal), else the altar's.
    public static class AscensionLink
    {
        private static bool looked;
        private static object instance;
        private static FieldInfo levelField, runLevelField;
        private static Func<int, float> eliteChance, twoAffixChance, riftChance, experience;
        private static Func<int, bool, int> bossAffixes;
        private static Func<int, bool> elitesInPairs, elitesDropRare, realmBossLegendary;
        private static Func<int, int> itemLevelBonus;

        private static void Find()
        {
            if (looked)
                return;
            looked = true;
            try
            {
                PluginInfo info;
                if (!Chainloader.PluginInfos.TryGetValue("mdbailey94.wol.ascension", out info) || info.Instance == null)
                    return;
                instance = info.Instance;
                Type plugin = instance.GetType();
                const BindingFlags any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
                levelField = plugin.GetField("level", any);
                runLevelField = plugin.GetField("runLevel", any);
                Type table = plugin.Assembly.GetType("WoLAscension.AscensionLevels");
                if (table == null)
                    return;
                eliteChance = Make<Func<int, float>>(table, "EliteChance");
                twoAffixChance = Make<Func<int, float>>(table, "TwoAffixChance");
                riftChance = Make<Func<int, float>>(table, "RiftChance");
                experience = Make<Func<int, float>>(table, "Experience");
                bossAffixes = Make<Func<int, bool, int>>(table, "BossAffixes");
                elitesInPairs = Make<Func<int, bool>>(table, "ElitesInPairs");
                elitesDropRare = Make<Func<int, bool>>(table, "ElitesDropRare");
                realmBossLegendary = Make<Func<int, bool>>(table, "RealmBossDropsLegendary");
                itemLevelBonus = Make<Func<int, int>>(table, "ItemLevelBonus");
                ChaosDepthsPlugin.Log($"Ascension: reading its level table ({(eliteChance != null ? "found" : "this Ascension is older than 0.7.0, so only its level is used")})");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogWarning($"Ascension link: {e.Message}");
            }
        }

        private static T Make<T>(Type type, string name) where T : class
        {
            try
            {
                MethodInfo m = type.GetMethod(name, BindingFlags.Public | BindingFlags.Static);
                return m != null ? Delegate.CreateDelegate(typeof(T), m) as T : null;
            }
            catch
            {
                return null;
            }
        }

        // The level in effect: the run's while one is on, else the one set at the altar.
        public static int Level
        {
            get
            {
                Find();
                try
                {
                    if (instance == null)
                        return 0;
                    object run = runLevelField != null ? runLevelField.GetValue(runLevelField.IsStatic ? null : instance) : null;
                    if (run is int && (int)run >= 0)
                        return (int)run;
                    var entry = levelField != null ? levelField.GetValue(levelField.IsStatic ? null : instance) as ConfigEntry<int> : null;
                    return entry != null ? Math.Max(0, entry.Value) : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        // A dive is complete: Ascension unlocks the next level if this was the highest (#32).
        public static void ReportRunBeaten()
        {
            Find();
            try
            {
                MethodInfo m = instance != null ? instance.GetType().GetMethod("ReportRunBeaten", BindingFlags.Public | BindingFlags.Static) : null;
                if (m != null)
                    m.Invoke(null, null);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogWarning($"Ascension link (run beaten): {e.Message}");
            }
        }

        // Each reads the level first (which finds the table on first use).
        public static float EliteChance { get { int l = Level; return eliteChance != null ? eliteChance(l) : 0f; } }
        public static float TwoAffixChance { get { int l = Level; return twoAffixChance != null ? twoAffixChance(l) : 0f; } }
        public static float RiftChance { get { int l = Level; return riftChance != null ? riftChance(l) : 0f; } }
        public static float Experience { get { int l = Level; return experience != null ? experience(l) : 1f; } }
        public static int BossAffixes(bool realmBoss) { int l = Level; return bossAffixes != null ? bossAffixes(l, realmBoss) : 0; }
        public static bool ElitesInPairs { get { int l = Level; return elitesInPairs != null && elitesInPairs(l); } }
        public static bool ElitesDropRare { get { int l = Level; return elitesDropRare != null && elitesDropRare(l); } }
        public static bool RealmBossDropsLegendary { get { int l = Level; return realmBossLegendary != null && realmBossLegendary(l); } }
        public static int ItemLevelBonus { get { int l = Level; return itemLevelBonus != null ? itemLevelBonus(l) : 0; } }
    }
}
