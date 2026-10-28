using System;
using HarmonyLib;

namespace WoLRollingGale
{
    // The game only lists an arcana on the spellbook's Signature page (and keeps it in the
    // signature slot across restarts) once its signature is unlocked (SkillState.signatureUnlocked,
    // saved per arcana). In vanilla that takes finding the arcana's "Signature" drop in a run: the
    // game offers one for every arcana with hasSignatureVariant whose signature is still locked,
    // so that route works for this arcana too. With SignatureUnlocksWithArcana on, the signature
    // is unlocked along with the arcana instead: whenever the game copies the saved unlocks onto a
    // wizard's arcana (GameData.PullSkillData: on load, right after an arcana is unlocked in the
    // shop, and when Unlock Mod Content switches it on), an unlocked arcana gets its signature
    // unlocked too, in the save as well.
    public static class SignatureUnlock
    {
        private static string skillID;
        private static Func<bool> enabled;
        private static Action<string> log;
        private static bool loggedError;

        public static void Install(Harmony harmony, string id, Func<bool> isEnabled, Action<string> logger)
        {
            skillID = id;
            enabled = isEnabled;
            log = logger;
            harmony.CreateClassProcessor(typeof(PullPatch)).Patch();
        }

        [HarmonyPatch(typeof(GameData), nameof(GameData.PullSkillData))]
        private static class PullPatch
        {
            private static void Postfix(GameData __instance)
            {
                try
                {
                    Apply(__instance);
                }
                catch (Exception e)
                {
                    if (loggedError)
                        return;
                    loggedError = true;
                    log?.Invoke($"couldn't unlock the signature along with the arcana: {e.Message}");
                }
            }
        }

        private static void Apply(GameData data)
        {
            if (enabled == null || !enabled())
                return;
            Player[] players = GameController.activePlayers;
            if (players == null)
                return;
            foreach (Player player in players)
            {
                Player.SkillState state;
                if (player == null || player.skillsDict == null || !player.skillsDict.TryGetValue(skillID, out state)
                    || state == null || !state.isUnlocked || state.signatureUnlocked)
                    continue;
                state.signatureUnlocked = true;
                if (data != null && data.skillDataDictionary != null)
                {
                    GameData.StoredSkillData stored;
                    if (!data.skillDataDictionary.TryGetValue(skillID, out stored) || stored == null)
                    {
                        stored = new GameData.StoredSkillData();
                        data.skillDataDictionary[skillID] = stored;
                    }
                    stored.signatureUnlocked = true;
                }
                log?.Invoke("signature unlocked along with the arcana (it's on the spellbook's Signature page now)");
            }
        }
    }
}
