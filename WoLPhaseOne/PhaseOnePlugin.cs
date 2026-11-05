using BepInEx;
using BepInEx.Configuration;

namespace WoLPhaseOne
{
    // The Mod Menu's "Phase One" switch (Matt, 2026-11-02: "leave a 'phase one' option in the mod
    // menu to turn it back off"). It does nothing itself: Chaos Depths reads this plugin's
    // [General] Enabled and runs its Phase 1 features (cloak talent trees, arcana levels,
    // loadouts) only while it's on. The Mod Menu lists every mdbailey94.wol.* plugin with its
    // [General] Enabled, so this shows up there by its name, and a change there reaches Chaos
    // Depths at once (same config entry).
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class PhaseOnePlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.phaseone";
        public const string PluginName = "Phase One";
        public const string PluginVersion = "1.0.0";

        private ConfigEntry<bool> enabled;

        private void Awake()
        {
            enabled = Config.Bind("General", "Enabled", true,
                "Chaos Depths' Phase 1 features: cloak talent trees (the TALENTS tab), arcana levels and loadouts. " +
                "Off: the TALENTS tab locks again, no talent or arcana level does anything, and loadouts can't be worn. " +
                "Points, levels and sets stay saved, so turning it back on brings them back.");
            enabled.SettingChanged += (s, e) => Logger.LogInfo($"Phase One: {(enabled.Value ? "on" : "off")}");
            Logger.LogInfo($"{PluginName} {PluginVersion} registered: Phase 1 is {(enabled.Value ? "on" : "off")}");
        }
    }
}
