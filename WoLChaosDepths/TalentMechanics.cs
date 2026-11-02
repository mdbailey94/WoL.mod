using BepInEx.Logging;

namespace WoLChaosDepths
{
    /// <summary>
    /// The Windrunner and Mender trees' mechanic nodes and keystones (WoL Player's file; the other
    /// trees' mechanics are in TalentEffects). Installed from TalentEffects.Install. Ranks come
    /// from Talents.Rank, which is 0 while [Talents] Enabled is off.
    /// </summary>
    public static class TalentMechanics
    {
        private static ManualLogSource log;

        public static void Install(ManualLogSource logger)
        {
            log = logger;
        }
    }
}
