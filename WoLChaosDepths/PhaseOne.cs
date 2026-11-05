using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;

namespace WoLChaosDepths
{
    // Phase 1's master switch: the Phase One plugin's [General] Enabled (WoLPhaseOne, listed in
    // the Mod Menu as "Phase One"). Talents, ArcanaLevels and LoadoutSets each run only while it's
    // on (and their own Enabled key is). Without the Phase One plugin installed, Phase 1 is off.
    public static class PhaseOne
    {
        private const string Guid = "mdbailey94.wol.phaseone";
        private static ConfigEntry<bool> entry;
        private static bool looked;

        public static bool On
        {
            get
            {
                if (entry == null && !looked)
                {
                    PluginInfo info;
                    if (Chainloader.PluginInfos != null && Chainloader.PluginInfos.TryGetValue(Guid, out info))
                    {
                        var plugin = info.Instance as BepInEx.BaseUnityPlugin;
                        if (plugin != null)
                        {
                            // Binding the existing key returns that plugin's own entry, so the Mod
                            // Menu's change reaches here at once.
                            entry = plugin.Config.Bind("General", "Enabled", true, "");
                            looked = true;
                        }
                    }
                    else if (Chainloader.PluginInfos != null && Chainloader.PluginInfos.Count > 0 && UnityEngine.Time.frameCount > 1)
                        looked = true;   // the chainloader is done and Phase One isn't there
                }
                return entry != null && entry.Value;
            }
        }
    }
}
