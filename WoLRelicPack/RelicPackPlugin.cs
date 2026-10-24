using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using LegendAPI;
using UnityEngine;

namespace WoLRelicPack
{
    // Four new relics, added with LegendAPI: Storm Battery, Echo Charm, Glass Prism and
    // Pickpocket's Glove. Each is an ordinary game Item (see the relic classes), so the game
    // activates, stacks and cleans them up like its own relics.
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    [BepInDependency("xyz.yekoc.wizardoflegend.LegendAPI")]
    public class RelicPackPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.relicpack";
        public const string PluginName = "Relic Pack";
        public const string PluginVersion = "0.1.0";

        // Every relic ID (and attack tag) starts with this, so the relics can recognise their own hits.
        public const string IDPrefix = "RelicPack";

        // Shop data: tier is the game's relic tier (0-4, rarer and later in a run as it goes up),
        // price the game's base price (gold price is 25x, plaza platinum price is this + (tier+1) x 4).
        // Vanilla relics are mostly 5-7; gold relics 5.
        private const int StormBatteryTier = 3;        // like Boots of Celerity (dash charges) and other tier-3 combo relics
        private const int StormBatteryPrice = 6;
        private const int EchoCharmTier = 3;           // like the other signature-arcana relics
        private const int EchoCharmPrice = 6;
        private const int GlassPrismTier = 2;          // a big crit boost, but it costs health
        private const int GlassPrismPrice = 6;
        private const int PickpocketsGloveTier = 2;
        private const int PickpocketsGlovePrice = 5;   // gold relics are 5

        private static BepInEx.Logging.ManualLogSource log;
        private static readonly Dictionary<string, float> lastLogged = new Dictionary<string, float>();
        private static readonly HashSet<string> loggedOnce = new HashSet<string>();
        private static readonly string[] RelicIDs =
        {
            StormBattery.staticID, EchoCharm.staticID, GlassPrism.staticID, PickpocketsGlove.staticID
        };

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<KeyboardShortcut> giveKey;

        public static void Log(string message) => log?.LogInfo(message);

        // For things that can happen many times a second: at most once per interval per key.
        public static void LogRateLimited(string key, float interval, string message)
        {
            float last;
            if (lastLogged.TryGetValue(key, out last) && Time.unscaledTime - last < interval)
                return;
            lastLogged[key] = Time.unscaledTime;
            log?.LogInfo(message);
        }

        // An error in code that runs every hit or frame: log it the first time only.
        public static void LogErrorOnce(string key, Exception e)
        {
            if (loggedOnce.Add(key))
                log?.LogError($"{key}: {e} (further errors here are not logged)");
        }

        private void Awake()
        {
            log = Logger;
            modEnabled = Config.Bind("General", "Enabled", true,
                "Offer the Relic Pack relics in relic shops and drops (also in the title screen Mods menu). " +
                "Turning it off doesn't remove them from a run where you already have them.");
            giveKey = Config.Bind("Testing", "GiveRelicsKey", KeyboardShortcut.Empty,
                "For testing: a key that gives player 1 all four Relic Pack relics (e.g. F8). Empty = off.");

            Register(new StormBattery(), "Storm Battery",
                "Dashing charges the battery, up to three times! Your next arcana hit unleashes chain lightning, one bolt per charge!",
                "storm_battery.png", StormBatteryTier, StormBatteryPrice);
            Register(new EchoCharm(), "Echo Charm",
                "Your signature arcana echoes! Everything it hits is struck again a moment later at reduced power!",
                "echo_charm.png", EchoCharmTier, EchoCharmPrice);
            Register(new GlassPrism(), "Glass Prism",
                "Greatly increases critical hit chance, but reduces max health!",
                "glass_prism.png", GlassPrismTier, GlassPrismPrice);
            Register(new PickpocketsGlove(), "Pickpocket's Glove",
                "Gold drops are greater while your health is at half or below!",
                "pickpockets_glove.png", PickpocketsGloveTier, PickpocketsGlovePrice);

            Logger.LogInfo($"{PluginName} {PluginVersion} registered {RelicIDs.Length} relics: {string.Join(", ", RelicIDs)}" +
                (giveKey.Value.MainKey != KeyCode.None ? $" (testing key {giveKey.Value} gives them all)" : string.Empty));
        }

        private void Register(Item item, string displayName, string description, string iconFile, int tier, int price)
        {
            Items.Register(new ItemInfo
            {
                item = item,
                name = displayName,
                tier = tier,
                text = new TextManager.ItemInfo
                {
                    itemID = item.ID,
                    displayName = displayName,
                    description = description
                },
                icon = LoadIcon(iconFile),
                priceMultiplier = price,
                // LegendAPI rerolls a plaza (locked relic) offer when this returns false.
                unlockCondition = () => modEnabled.Value
            });
        }

        private void Update()
        {
            try
            {
                // Switched off: keep them out of this run's relic shops and drops too (the game
                // rebuilds that pool on every level load, well before a shop picks from it).
                if (!modEnabled.Value && GameController.activePlayers != null && GameController.activePlayers.Length > 0)
                {
                    foreach (string id in RelicIDs)
                        LootManager.RemoveIDFromAvailableList(id);
                }
                KeyboardShortcut key = giveKey.Value;
                if (key.MainKey != KeyCode.None && key.IsDown())
                    GiveAll();
            }
            catch (Exception e)
            {
                LogErrorOnce("Relic Pack update", e);
            }
        }

        // Testing helper: all four relics to player 1 (only once a level has loaded, when LegendAPI
        // has added them to the game's relic list).
        private void GiveAll()
        {
            Player player = null;
            if (GameController.activePlayers != null)
            {
                foreach (Player p in GameController.activePlayers)
                {
                    if (p != null && p.inventory != null)
                    {
                        player = p;
                        break;
                    }
                }
            }
            if (player == null)
            {
                Logger.LogInfo("Testing key: no wizard loaded yet");
                return;
            }
            foreach (string id in RelicIDs)
            {
                if (!LootManager.completeItemDict.ContainsKey(id))
                    Logger.LogInfo($"Testing key: {id} isn't in the game's relic list yet (load a level first)");
                else if (player.inventory.ContainsItem(id))
                    Logger.LogInfo($"Testing key: player {player.playerID + 1} already has {id}");
                else if (player.inventory.AddItem(id, true, true))
                    Logger.LogInfo($"Testing key: gave {id} to player {player.playerID + 1}");
                else
                    Logger.LogInfo($"Testing key: the game refused to add {id}");
            }
        }

        private Sprite LoadIcon(string fileName)
        {
            string resourceName = $"{nameof(WoLRelicPack)}.Assets.{fileName}";
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Logger.LogWarning($"Icon resource {resourceName} not found, using the default icon");
                    return null;
                }
                byte[] data = new byte[stream.Length];
                stream.Read(data, 0, data.Length);
                var texture = new Texture2D(2, 2) { filterMode = FilterMode.Point };
                texture.LoadImage(data);
                return GameIcon.Create(texture, Logger);
            }
        }
    }
}
