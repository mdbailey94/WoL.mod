using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLCloakUpgrades
{
    // Permanent cloak upgrades bought with chaos gems at the wardrobe. While the wardrobe is open,
    // a pixel-art panel beside it shows the highlighted cloak's tier (four pips), what the next
    // tier does and costs, and your gems; press U (keyboard) or Y / Triangle (controller) to buy it.
    // Tiers are kept per cloak in this mod's config file, so they last across runs and restarts.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CloakUpgradesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.cloakupgrades";
        public const string PluginName = "Cloak Upgrades";
        public const string PluginVersion = "0.1.0";

        private const float MessageTime = 1.6f;

        private static CloakUpgradesPlugin instance;
        private static BepInEx.Logging.ManualLogSource log;
        private static WardrobeUI openWardrobe;

        private ConfigEntry<bool> modEnabled;
        private readonly Dictionary<string, ConfigEntry<int>> tiers = new Dictionary<string, ConfigEntry<int>>();
        private string message;
        private Color messageColor;
        private float messageUntil;

        public static void Log(string text) => log?.LogInfo(text);

        private void Awake()
        {
            instance = this;
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Turn cloak upgrades on or off (off: cloaks have their normal stats, nothing is lost).");
            CloakScaling.TierOf = id => modEnabled.Value ? Tier(id) : 0;
            var harmony = new Harmony(PluginGuid);
            CloakScaling.Install(harmony);
            harmony.CreateClassProcessor(typeof(WardrobePatch)).Patch();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        // The wardrobe screen tells us when it's open and which cloak is highlighted.
        [HarmonyPatch(typeof(WardrobeUI), nameof(WardrobeUI.Update))]
        private static class WardrobePatch
        {
            private static void Postfix(WardrobeUI __instance)
            {
                if (__instance.isActive)
                    openWardrobe = __instance;
                else if (openWardrobe == __instance)
                    openWardrobe = null;
            }
        }

        private int Tier(string outfitID)
        {
            ConfigEntry<int> entry = TierEntry(outfitID);
            return entry != null ? Mathf.Clamp(entry.Value, 0, CloakTiers.Max) : 0;
        }

        private ConfigEntry<int> TierEntry(string outfitID)
        {
            if (string.IsNullOrEmpty(outfitID))
                return null;
            ConfigEntry<int> entry;
            if (!tiers.TryGetValue(outfitID, out entry))
            {
                entry = Config.Bind("Tiers", outfitID, 0,
                    new ConfigDescription("Upgrade tier of this cloak (0-4), bought at the wardrobe.",
                        new AcceptableValueRange<int>(0, CloakTiers.Max)));
                tiers[outfitID] = entry;
            }
            return entry;
        }

        private static Outfit Highlighted(WardrobeUI ui)
        {
            if (ui == null || ui.availableOutfitList == null)
                return null;
            int index = ui.currentOutfitIndex;
            return index >= 0 && index < ui.availableOutfitList.Count ? ui.availableOutfitList[index] : null;
        }

        private static int Gems() => Player.platWallet != null ? Player.platWallet.balance : 0;

        private void Update()
        {
            if (!modEnabled.Value || openWardrobe == null || !openWardrobe.isActive)
                return;
            if (UpgradeInput.Pressed())
                TryUpgrade(openWardrobe);
        }

        private void TryUpgrade(WardrobeUI ui)
        {
            Outfit outfit = Highlighted(ui);
            if (outfit == null || !outfit.unlocked)
            {
                Say("Unlock this cloak first", PixelUI.Dim);
                return;
            }
            int tier = Tier(outfit.outfitID);
            if (tier >= CloakTiers.Max)
            {
                Say("Fully upgraded", PixelUI.Dim);
                return;
            }
            int cost = CloakTiers.Cost(tier);
            if (Player.platWallet == null || Player.platWallet.balance < cost || !Player.platWallet.Withdraw(cost))
            {
                Say("Not enough chaos gems", new Color32(0xe0, 0x5a, 0x4a, 0xff));
                return;
            }

            // Take the cloak's bonuses off whoever is wearing it, raise the tier, put them back.
            List<Player> wearers = Wearers(outfit.outfitID);
            foreach (Player p in wearers)
                SetMods(p, false);
            TierEntry(outfit.outfitID).Value = tier + 1;
            CloakScaling.Scale(outfit);
            foreach (Player p in wearers)
                SetMods(p, true);
            try
            {
                ui.LoadInfo(outfit);
            }
            catch
            {
            }
            try
            {
                GameDataManager.Save(true, false);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't save the game after the upgrade: {e.Message}");
            }
            Logger.LogInfo($"{outfit.outfitID} upgraded to tier {tier + 1} for {cost} gems ({Gems()} left)");
            Say($"TIER {tier + 1}!", PixelUI.Accent);
        }

        private static List<Player> Wearers(string outfitID)
        {
            var list = new List<Player>();
            Player[] active = GameController.activePlayers;
            if (active == null)
                return list;
            foreach (Player p in active)
            {
                if (p != null && p.equippedOutfit != null && p.equippedOutfit.outfitID == outfitID)
                    list.Add(p);
            }
            return list;
        }

        private void SetMods(Player p, bool on)
        {
            try
            {
                p.equippedOutfit.SetMods(on, false);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't refresh the worn cloak: {e.Message}");
            }
        }

        private void Say(string text, Color color)
        {
            message = text;
            messageColor = color;
            messageUntil = Time.unscaledTime + MessageTime;
        }

        // ---- Drawing ----

        private void OnGUI()
        {
            if (!modEnabled.Value || openWardrobe == null || !openWardrobe.isActive)
                return;
            Outfit outfit = Highlighted(openWardrobe);
            if (outfit == null)
                return;

            int u = PixelUI.Unit;
            GUIStyle title = PixelUI.Style(6 * u, PixelUI.Border, TextAnchor.MiddleCenter);
            GUIStyle body = PixelUI.Style(4 * u, PixelUI.Text, TextAnchor.MiddleCenter);
            GUIStyle hint = PixelUI.Style(4 * u, PixelUI.Dim, TextAnchor.MiddleCenter);
            GUIStyle accent = PixelUI.Style(4 * u, PixelUI.Accent, TextAnchor.MiddleCenter);

            int tier = Tier(outfit.outfitID);
            bool maxed = tier >= CloakTiers.Max;
            int cost = CloakTiers.Cost(tier);
            int gems = Gems();
            string now = CloakTiers.Describe(tier);
            string next = maxed ? "Fully upgraded" : "Next: " + CloakTiers.Describe(tier + 1);
            string price = maxed ? "" : $"Upgrade for {cost} gems  (you have {gems})";
            string action = !outfit.unlocked ? "Unlock this cloak to upgrade it"
                : maxed ? "" : "Press U  /  Y  to upgrade";
            bool showMessage = Time.unscaledTime < messageUntil && !string.IsNullOrEmpty(message);

            var lines = new List<KeyValuePair<string, GUIStyle>>
            {
                new KeyValuePair<string, GUIStyle>(now, body),
                new KeyValuePair<string, GUIStyle>(next, maxed ? accent : hint),
            };
            if (price.Length > 0)
                lines.Add(new KeyValuePair<string, GUIStyle>(price, gems >= cost ? body : hint));
            if (showMessage)
                lines.Add(new KeyValuePair<string, GUIStyle>(message, PixelUI.Style(4 * u, messageColor, TextAnchor.MiddleCenter)));
            else if (action.Length > 0)
                lines.Add(new KeyValuePair<string, GUIStyle>(action, accent));

            float lineHeight = body.CalcSize(new GUIContent("Ag")).y;
            float textWidth = 0f;
            foreach (KeyValuePair<string, GUIStyle> line in lines)
                textWidth = Mathf.Max(textWidth, line.Value.CalcSize(new GUIContent(line.Key)).x);
            const int pips = CloakTiers.Max;
            int pipSize = 6 * u, pipGap = 2 * u;
            float pipRow = pips * pipSize + (pips - 1) * pipGap;
            float titleHeight = title.CalcSize(new GUIContent("CLOAK UPGRADE")).y;
            float width = Mathf.Max(textWidth, pipRow, title.CalcSize(new GUIContent("CLOAK UPGRADE")).x) + 16 * u;
            float height = 5 * u + titleHeight + 3 * u + pipSize + 4 * u + lines.Count * lineHeight + 5 * u;

            // Along the bottom of the screen, clear of the wardrobe's own cloak wheel and info box.
            var panel = new Rect(Mathf.Round((Screen.width - width) / 2f), Mathf.Round(Screen.height - height - 8 * u),
                                 Mathf.Round(width), Mathf.Round(height));
            PixelUI.Panel(panel, u);
            float y = panel.y + 5 * u;
            GUI.Label(new Rect(panel.x, y, panel.width, titleHeight), "CLOAK UPGRADE", title);
            y += titleHeight + 3 * u;
            float pipX = panel.x + (panel.width - pipRow) / 2f;
            for (int i = 0; i < pips; i++)
                PixelUI.Pip(new Rect(pipX + i * (pipSize + pipGap), y, pipSize, pipSize), i < tier, u);
            y += pipSize + 4 * u;
            foreach (KeyValuePair<string, GUIStyle> line in lines)
            {
                GUI.Label(new Rect(panel.x, y, panel.width, lineHeight), line.Key, line.Value);
                y += lineHeight;
            }
        }
    }
}
