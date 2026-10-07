using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace WoLCloakUpgrades
{
    // Permanent cloak upgrades bought with chaos gems at the wardrobe, all in the wardrobe's own
    // screen: the highlighted cloak's info box gains its upgrade tier and the price of the next one
    // (the box's text shrinks to fit), pressing Y (controller) or U (keyboard) asks with the game's
    // own yes/no box, and a purchase is announced by the game's on-screen notice. Tiers are kept per cloak in this
    // mod's config file, so they last across runs and restarts.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CloakUpgradesPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.cloakupgrades";
        public const string PluginName = "Cloak Upgrades";
        public const string PluginVersion = "0.3.2";

        // Ignore presses right after the wardrobe opens.
        private const float OpenGrace = 0.35f;
        // Without the game's yes/no box: press again within this long to confirm.
        private const float SecondPressWindow = 2.5f;
        private const float ConfirmTimeout = 30f;

        private static CloakUpgradesPlugin instance;
        private static BepInEx.Logging.ManualLogSource log;
        private static WardrobeUI openWardrobe;
        private static float openedAt;

        private ConfigEntry<bool> modEnabled;
        private readonly Dictionary<string, ConfigEntry<int>> tiers = new Dictionary<string, ConfigEntry<int>>();

        // A purchase waiting for the yes/no answer (or, without the box, a second press).
        private Outfit pending;
        private bool pendingUsesBox;
        private float pendingSince;

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
            harmony.CreateClassProcessor(typeof(WardrobeUpdatePatch)).Patch();
            harmony.CreateClassProcessor(typeof(WardrobeInfoPatch)).Patch();
            Logger.LogInfo($"{PluginName} {PluginVersion} loaded");
        }

        // Knows when the wardrobe is open, and holds its own input back while the yes/no box is up
        // (so answering doesn't also equip a cloak or close the wardrobe).
        [HarmonyPatch(typeof(WardrobeUI), nameof(WardrobeUI.Update))]
        private static class WardrobeUpdatePatch
        {
            private static bool Prefix() => instance == null || instance.pending == null || !instance.pendingUsesBox;

            private static void Postfix(WardrobeUI __instance)
            {
                if (__instance.isActive)
                {
                    if (openWardrobe != __instance)
                        openedAt = Time.unscaledTime;
                    openWardrobe = __instance;
                }
                else if (openWardrobe == __instance)
                    openWardrobe = null;
            }
        }

        // The wardrobe's info box for the highlighted cloak: add its tier and the next upgrade.
        [HarmonyPatch(typeof(WardrobeUI), nameof(WardrobeUI.LoadInfo))]
        private static class WardrobeInfoPatch
        {
            private static void Postfix(WardrobeUI __instance, Outfit givenOutfit)
            {
                try
                {
                    instance?.AddUpgradeInfo(__instance, givenOutfit);
                }
                catch (Exception e)
                {
                    log?.LogWarning($"Couldn't add the upgrade to the wardrobe's info: {e.Message}");
                }
            }
        }

        private void AddUpgradeInfo(WardrobeUI ui, Outfit outfit)
        {
            if (!modEnabled.Value || outfit == null || ui.wrRef == null || ui.wrRef.infoDescText == null)
                return;
            UnityEngine.UI.Text text = ui.wrRef.infoDescText;
            FitToBox(text);
            string description = text.text.TrimEnd();
            // A cloak with a long list of stats: keep the upgrade to one line.
            bool compact = description.Split('\n').Length > LongDescriptionLines;
            text.text = description + "\n" + UpgradeLines(outfit, OnController(ui), compact);
        }

        // Let the info box shrink its text to fit instead of running off the screen (down to a
        // third of its size for the longest cloaks, only as much as it needs).
        private static void FitToBox(UnityEngine.UI.Text text)
        {
            if (text.resizeTextForBestFit)
                return;
            int size = text.fontSize;
            text.resizeTextMaxSize = size;
            text.resizeTextMinSize = Mathf.Max(5, size / 3);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
        }

        private static bool OnController(WardrobeUI ui)
        {
            try
            {
                return ui.player != null && ui.player.inputDevice != null
                    && ui.player.inputDevice.inputScheme == ChaosInputDevice.InputScheme.Gamepad;
            }
            catch
            {
                return false;
            }
        }

        private const int LongDescriptionLines = 4;

        // Kept short: one line for the tier, one for the button and price (one line in all when
        // the cloak's own description is long).
        private string UpgradeLines(Outfit outfit, bool controller, bool compact)
        {
            if (!outfit.unlocked)
                return "Unlock to upgrade";
            int tier = Tier(outfit.outfitID);
            string now = $"Tier {tier}/{CloakTiers.Max}: {CloakTiers.Short(tier)}";
            if (tier >= CloakTiers.Max)
                return now + " (max)";
            string button = controller ? "Y" : "U";
            string gap = compact ? " - " : "\n";
            if (pending == outfit && !pendingUsesBox)
                return now + gap + $"Press {button} again to confirm";
            return now + gap + (compact ? $"{button}: {CloakTiers.Cost(tier)} gems" : $"Press {button} to upgrade: {CloakTiers.Cost(tier)} gems");
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
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                CancelPending();
            }
        }

        private void Tick()
        {
            WardrobeUI ui = openWardrobe;
            if (!modEnabled.Value || ui == null || !ui.isActive)
            {
                CancelPending();
                return;
            }
            if (pending != null && pendingUsesBox)
            {
                WaitForAnswer(ui);
                return;
            }
            if (pending != null && Time.unscaledTime - pendingSince > SecondPressWindow)
            {
                CancelPending();
                Refresh(ui, Highlighted(ui));
            }
            if (Time.unscaledTime - openedAt < OpenGrace || !UpgradeInput.Pressed())
                return;

            Outfit outfit = Highlighted(ui);
            if (outfit == null || !outfit.unlocked || Tier(outfit.outfitID) >= CloakTiers.Max)
                return;
            int cost = CloakTiers.Cost(Tier(outfit.outfitID));
            if (Gems() < cost)
            {
                SoundManager.PlayAudio("StandardHeavySwing", 0.6f, false, -1f, -1f);
                return;
            }
            if (pending == outfit)
            {
                // The second press, when the game's yes/no box isn't available.
                pending = null;
                Buy(ui, outfit);
                return;
            }
            Ask(ui, outfit, cost);
        }

        // The game's own yes/no box; if it can't be used, the info box asks for a second press.
        private void Ask(WardrobeUI ui, Outfit outfit, int cost)
        {
            pending = outfit;
            pendingSince = Time.unscaledTime;
            pendingUsesBox = false;
            try
            {
                ConfirmHandlerUI.RequestConfirmation(0);
                if (ConfirmHandlerUI.RequestInProgress)
                {
                    pendingUsesBox = true;
                    if (ConfirmHandlerUI.titleText != null)
                        ConfirmHandlerUI.titleText.text = $"Upgrade to tier {Tier(outfit.outfitID) + 1} for {cost} gems?";
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.LogInfo($"The game's yes/no box isn't available, asking for a second press: {e.Message}");
            }
            Refresh(ui, outfit);
        }

        private void WaitForAnswer(WardrobeUI ui)
        {
            bool yes = false;
            try
            {
                yes = ConfirmHandlerUI.RequestConfirmation(0);
                if (!yes && ConfirmHandlerUI.RequestInProgress && Time.unscaledTime - pendingSince < ConfirmTimeout)
                    return;
            }
            catch (Exception e)
            {
                Logger.LogWarning($"The yes/no box failed: {e.Message}");
            }
            if (!yes && ConfirmHandlerUI.RequestInProgress)
            {
                try
                {
                    ConfirmHandlerUI.ForceQuitConfirmation();
                }
                catch
                {
                }
            }
            Outfit outfit = pending;
            pending = null;
            pendingUsesBox = false;
            if (yes && outfit != null && Gems() >= CloakTiers.Cost(Tier(outfit.outfitID)))
                Buy(ui, outfit);
        }

        private void CancelPending()
        {
            if (pending == null)
                return;
            if (pendingUsesBox)
            {
                try
                {
                    if (ConfirmHandlerUI.RequestInProgress)
                        ConfirmHandlerUI.ForceQuitConfirmation();
                }
                catch
                {
                }
            }
            pending = null;
            pendingUsesBox = false;
        }

        private void Buy(WardrobeUI ui, Outfit outfit)
        {
            int tier = Tier(outfit.outfitID);
            int cost = CloakTiers.Cost(tier);
            if (tier >= CloakTiers.Max || Player.platWallet == null || !Player.platWallet.Withdraw(cost))
                return;

            // Take the cloak's bonuses off whoever is wearing it, raise the tier, put them back.
            List<Player> wearers = Wearers(outfit.outfitID);
            foreach (Player p in wearers)
                SetMods(p, false);
            TierEntry(outfit.outfitID).Value = tier + 1;
            CloakScaling.Scale(outfit);
            foreach (Player p in wearers)
                SetMods(p, true);
            Refresh(ui, outfit);
            try
            {
                GameDataManager.Save(true, false);
            }
            catch (Exception e)
            {
                Logger.LogWarning($"Couldn't save the game after the upgrade: {e.Message}");
            }
            Logger.LogInfo($"{outfit.outfitID} upgraded to tier {tier + 1} for {cost} gems ({Gems()} left)");
            GameBanner.Show("CLOAK UPGRADED", $"Tier {tier + 1}: {CloakTiers.Short(tier + 1)}", Picture(ui));
        }

        private static void Refresh(WardrobeUI ui, Outfit outfit)
        {
            if (ui == null || outfit == null)
                return;
            try
            {
                ui.LoadInfo(outfit);
            }
            catch
            {
            }
        }

        // The wardrobe's picture of the wizard in the cloak, for the banner.
        private static Sprite Picture(WardrobeUI ui)
        {
            try
            {
                return ui.wrRef != null && ui.wrRef.playerImage != null ? ui.wrRef.playerImage.sprite : null;
            }
            catch
            {
                return null;
            }
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
    }
}
