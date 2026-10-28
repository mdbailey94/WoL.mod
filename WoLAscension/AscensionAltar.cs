using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WoLAscension
{
    // The Ascension altar: a carved stone totem with a brazier on top, standing beside the Chaos
    // Trials portal in the plaza. Walk up and the game's own button prompt appears over it; interact
    // to raise the level by one (after 10 it goes back to off). The flame in the brazier grows from
    // a small cold blue one to a big white-hot one with the level, ten rune notches up the column
    // light one by one, the number glows on the plinth, and the whole thing throws a glow and embers
    // in the flame's colour. The game's on-screen notice says what the level adds. Runs then start
    // at that level.
    public class AscensionAltar : MonoBehaviour
    {
        private const float Range = 2.4f;         // from the middle of the altar
        private const string InteractAction = "Interact";

        public static Vector2 Offset = new Vector2(-3.5f, 0f);
        // Glow, embers and the point light (the totem, flame and runes are always drawn).
        public static ConfigEntry<bool> Effects;

        private static readonly Dictionary<int, Sprite> flameSprites = new Dictionary<int, Sprite>();
        private static readonly Dictionary<int, Sprite> numberSprites = new Dictionary<int, Sprite>();
        private const int FlameFrames = 4;
        private const float FlameFps = 9f;
        // How the flame grows with the level (a small ember when off).
        private const float FlameSizeOff = 0.45f;
        private const float FlameSizeBase = 0.6f;
        private const float FlameSizePerLevel = 0.1f;
        // The halo behind the flame: size in units and alpha, both growing with the level.
        private const float HaloSizeBase = 1.4f;
        private const float HaloSizePerLevel = 0.16f;
        private const float HaloAlphaBase = 0.16f;
        private const float HaloAlphaPerLevel = 0.025f;
        // The rune ring on the ground around the base.
        private const float RingAlphaBase = 0.35f;
        private const float RingAlphaPerLevel = 0.04f;
        // The point light at the flame.
        private const float LightRangeBase = 3.5f;
        private const float LightRangePerLevel = 0.35f;
        private const float LightIntensityBase = 0.8f;
        private const float LightIntensityPerLevel = 0.22f;
        // Embers drifting up from the flame: particles a second, growing with the level.
        private const float EmbersBase = 1.5f;
        private const float EmbersPerLevel = 0.5f;
        private const float EmberCheckInterval = 2f;
        // The flare when the level changes: the flame swells by this much and settles over this long.
        private const float FlareSize = 0.6f;
        private const float FlareSeconds = 0.45f;
        private static Sprite pedestalSprite;
        private static Sprite pipSprite;
        private static Sprite haloSprite;
        private static Sprite ringSprite;
        private static float pixelsPerUnit = 16f;

        private ConfigEntry<int> level;
        private ManualLogSource log;
        private SpriteRenderer pedestal;
        private SpriteRenderer crystal;   // the flame
        private SpriteRenderer number;
        private SpriteRenderer halo;
        private SpriteRenderer ring;
        private readonly SpriteRenderer[] runes = new SpriteRenderer[AscensionLevels.Max];
        private Light light;
        private ParticleEffect embers;
        private Vector3 crystalHome;
        private float flare;
        private float nextEmberCheck;
        private bool loggedLight;
        private OverheadPrompt prompt;
        private bool promptShown;
        private bool greeted;
        private bool loggedNear;
        private static bool loggedKeys;
        private static bool loggedSwallow;

        // The wizard standing at the altar with the prompt up (null when nobody is); the input
        // patches below read it.
        public static Player Engaged { get; private set; }

        // Finds the portal into the trials in this scene and puts an altar beside it (null if this
        // scene has no such portal).
        // Where the portal into the trials was last seen (scene name and position), remembered when
        // a run starts through it, for scenes where it can't be found by looking.
        public static ConfigEntry<string> RememberedPortal;
        private static readonly HashSet<string> loggedScenes = new HashSet<string>();

        public static AscensionAltar SpawnBesideTrialsPortal(ConfigEntry<int> level, ManualLogSource log)
        {
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            bool firstLook = loggedScenes.Add(scene);
            Vector2? spot = null;
            string why = null;
            NextLevelLoader blank = null;

            // Every level loader in the scene, hidden ones too (the trials portal may be switched
            // off until something opens it, or get its destination later).
            foreach (NextLevelLoader loader in Resources.FindObjectsOfTypeAll<NextLevelLoader>())
            {
                if (loader == null || loader.gameObject.scene.name != scene)
                    continue;
                if (firstLook)
                    log.LogInfo($"Altar: level loader '{loader.name}' to '{loader.nextLevelName}' at {loader.transform.position} " +
                        $"(active: {loader.gameObject.activeInHierarchy})");
                if (spot == null && IsTrialScene(loader.nextLevelName))
                {
                    spot = loader.transform.position;
                    why = $"beside the portal to '{loader.nextLevelName}'";
                }
                // The plaza's trials portal has no destination until you step in (the game picks the
                // floor then): the active loader with none.
                else if (blank == null && string.IsNullOrEmpty(loader.nextLevelName) && loader.gameObject.activeInHierarchy)
                    blank = loader;
            }
            if (spot == null && blank != null)
            {
                spot = blank.transform.position;
                why = "beside the trials portal";
            }
            // Otherwise where a run was last started from, if it was this scene.
            if (spot == null && RememberedPortal != null && !string.IsNullOrEmpty(RememberedPortal.Value))
            {
                string[] parts = RememberedPortal.Value.Split('|');
                float x, y;
                if (parts.Length == 3 && parts[0] == scene && float.TryParse(parts[1], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out x)
                    && float.TryParse(parts[2], System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out y))
                {
                    spot = new Vector2(x, y);
                    why = "where the last run's portal was";
                }
            }
            if (spot == null)
            {
                if (firstLook)
                    log.LogInfo($"Altar: no trials portal found in '{scene}'");
                return null;
            }
            try
            {
                ReadPixelSize();
                var go = new GameObject("AscensionAltar");
                go.transform.position = ClearOfNpcs(spot.Value + Offset, log);
                AscensionAltar altar = go.AddComponent<AscensionAltar>();
                altar.level = level;
                altar.log = log;
                altar.Build();
                log.LogInfo($"Ascension altar placed in '{scene}' at {go.transform.position}, {why} ({pixelsPerUnit} px per unit)");
                return altar;
            }
            catch (Exception e)
            {
                log.LogError($"Couldn't place the Ascension altar: {e.Message}");
                return null;
            }
        }

        // The plaza's NPCs stand around the portal too (Mah-Linda, who sells the trials, is right
        // where the default spot lands), and standing at one of them while at the altar means
        // Interact opens their conversation instead. So the altar keeps this far from any NPC,
        // trying spots further down and to the sides of the chosen one.
        private const float NpcClearance = 2.6f;
        private static readonly Vector2[] NudgeTries =
        {
            new Vector2(0f, -2f), new Vector2(0f, -2.5f), new Vector2(0f, -3f), new Vector2(-1.5f, -2.5f),
            new Vector2(1.5f, -2.5f), new Vector2(-2f, 0f), new Vector2(0f, 2f),
        };

        private static Vector2 ClearOfNpcs(Vector2 place, ManualLogSource log)
        {
            try
            {
                var npcs = new List<Npc>();
                foreach (Npc npc in FindObjectsOfType<Npc>())
                {
                    if (npc != null && npc.gameObject.activeInHierarchy)
                        npcs.Add(npc);
                }
                Npc blocking = Nearest(npcs, place);
                if (blocking == null)
                    return place;
                foreach (Vector2 nudge in NudgeTries)
                {
                    Vector2 candidate = place + nudge;
                    if (Nearest(npcs, candidate) == null)
                    {
                        log?.LogInfo($"Ascension altar: moved by {nudge} to keep clear of '{blocking.name}'");
                        return candidate;
                    }
                }
                log?.LogInfo($"Ascension altar: '{blocking.name}' stands close by; set [Altar] LeftOfPortalX/Y to move the altar");
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: couldn't check for NPCs ({e.Message})");
            }
            return place;
        }

        // The NPC within clearance of the spot, closest first; null if none.
        private static Npc Nearest(List<Npc> npcs, Vector2 place)
        {
            Npc best = null;
            float bestDistance = NpcClearance;
            foreach (Npc npc in npcs)
            {
                float d = Vector2.Distance(npc.transform.position, place);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = npc;
                }
            }
            return best;
        }

        // Called when a run starts through a portal: remember where it was.
        public static void RememberPortal(NextLevelLoader portal)
        {
            if (RememberedPortal == null || portal == null)
                return;
            Vector3 p = portal.transform.position;
            RememberedPortal.Value = portal.gameObject.scene.name + "|"
                + p.x.ToString(System.Globalization.CultureInfo.InvariantCulture) + "|"
                + p.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool IsTrialScene(string name)
        {
            try
            {
                return !string.IsNullOrEmpty(name) && (NextLevelLoader.InTierScene(name) || NextLevelLoader.InBossScene(name));
            }
            catch
            {
                return false;
            }
        }

        // Pixel size of the game's sprites, from the wizard's, so the altar matches.
        private static void ReadPixelSize()
        {
            try
            {
                Player[] players = GameController.activePlayers;
                if (players != null && players.Length > 0 && players[0] != null && players[0].spriteRenderer != null
                    && players[0].spriteRenderer.sprite != null)
                    pixelsPerUnit = players[0].spriteRenderer.sprite.pixelsPerUnit;
            }
            catch
            {
            }
        }

        private bool EffectsOn => Effects == null || Effects.Value;

        private void Build()
        {
            pedestal = gameObject.AddComponent<SpriteRenderer>();
            pedestal.sprite = PedestalSprite();

            // The flame sits on the brazier's rim, so it shows above it.
            crystalHome = new Vector3(0f, FlameRow / pixelsPerUnit, 0f);
            crystal = Child("Flame", crystalHome);
            number = Child("Number", new Vector3(0f, NumberRow / pixelsPerUnit, 0f));
            halo = Child("Halo", crystalHome + new Vector3(0f, 0.45f, 0f));
            halo.sprite = HaloSprite();
            ring = Child("Ring", new Vector3(0f, 2f / pixelsPerUnit, 0f));
            ring.sprite = RingSprite();
            for (int i = 0; i < runes.Length; i++)
            {
                runes[i] = Child("Rune" + i, new Vector3(RuneX(i) / pixelsPerUnit, RuneY(i) / pixelsPerUnit, 0f));
                runes[i].sprite = PipSprite();
            }
            AddLight();
            ShowLevel();
            AddCollider();
        }

        private SpriteRenderer Child(string name, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPosition;
            return go.AddComponent<SpriteRenderer>();
        }

        // A point light at the flame, set up like the game's own (its item and torch lights are
        // point lights on sprites), so the ground around the altar catches the flame's colour.
        private void AddLight()
        {
            try
            {
                light = halo.gameObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = LightRangeBase;
                light.intensity = 0f;
                // Match whatever point light the scene already has, so it draws on the same things.
                foreach (Light other in FindObjectsOfType<Light>())
                {
                    if (other == null || other == light || other.type != LightType.Point)
                        continue;
                    light.cullingMask = other.cullingMask;
                    light.renderMode = other.renderMode;
                    light.shadows = other.shadows;
                    if (!loggedLight)
                    {
                        loggedLight = true;
                        log?.LogInfo($"Ascension altar: light set up like '{other.name}' (range {other.range:0.#}, intensity {other.intensity:0.##})");
                    }
                    break;
                }
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: no light ({e.Message})");
                light = null;
            }
        }

        // The flame's size and heat, the runes, the number, the glow and the light, for the
        // current level.
        private void ShowLevel()
        {
            int lvl = Level;
            Color outer, mid, core;
            FlameColours(lvl, out outer, out mid, out core);
            number.sprite = NumberSprite(lvl);
            for (int i = 0; i < runes.Length; i++)
            {
                runes[i].enabled = i < lvl;
                // The newest rune burns brightest.
                runes[i].color = i == lvl - 1 ? core : mid;
            }
            bool on = EffectsOn && lvl > 0;
            halo.enabled = on;
            ring.enabled = EffectsOn;
            halo.color = new Color(outer.r, outer.g, outer.b, HaloAlphaBase + HaloAlphaPerLevel * lvl);
            ring.color = new Color(lvl > 0 ? mid.r : outer.r, lvl > 0 ? mid.g : outer.g, lvl > 0 ? mid.b : outer.b,
                lvl > 0 ? RingAlphaBase + RingAlphaPerLevel * lvl : 0.2f);
            if (light != null)
            {
                light.enabled = on;
                light.color = mid;
                light.range = LightRangeBase + LightRangePerLevel * lvl;
                light.intensity = LightIntensityBase + LightIntensityPerLevel * lvl;
            }
            Embers(lvl);
            Animate(lvl);
        }

        private static float FlameSize(int lvl) => lvl <= 0 ? FlameSizeOff : FlameSizeBase + FlameSizePerLevel * lvl;

        // Everything that moves: the flame's flicker and flare, the halo's breathing, the runes'
        // shimmer. Called every frame.
        private void Animate(int lvl)
        {
            float t = Time.time;
            crystal.sprite = FlameSprite(lvl, Mathf.FloorToInt(t * FlameFps) % FlameFrames);
            float size = FlameSize(lvl) * (1f + FlareSize * flare);
            crystal.transform.localScale = new Vector3(size, size, 1f);

            if (halo.enabled)
            {
                float breathe = 1f + 0.06f * Mathf.Sin(t * 2.3f) + 0.35f * flare;
                float haloSize = (HaloSizeBase + HaloSizePerLevel * lvl) * breathe;
                halo.transform.localScale = new Vector3(haloSize, haloSize * 0.9f, 1f);
                halo.transform.localPosition = crystalHome + new Vector3(0f, 0.45f + 0.2f * FlameSize(lvl), 0f);
            }
            if (ring.enabled && lvl > 0)
            {
                Color c = ring.color;
                c.a = (RingAlphaBase + RingAlphaPerLevel * lvl) * (0.85f + 0.15f * Mathf.Sin(t * 3.1f)) + 0.3f * flare;
                ring.color = c;
            }
            if (light != null && light.enabled)
                light.intensity = (LightIntensityBase + LightIntensityPerLevel * lvl) * (1f + 0.08f * Mathf.Sin(t * 7f) + 0.6f * flare);
            for (int i = 0; i < lvl && i < runes.Length; i++)
            {
                Color c = runes[i].color;
                c.a = 0.8f + 0.2f * Mathf.Sin(t * 2.5f + i * 0.9f);
                runes[i].color = c;
            }
        }

        // Embers drifting up from the flame: the game's sparkle particles, in the flame's colour.
        // Pooled effects can be borrowed by other things, so this is re-checked now and then.
        private void Embers(int lvl)
        {
            nextEmberCheck = Time.time + EmberCheckInterval;
            if (!EffectsOn || lvl <= 0)
            {
                StopEmbers();
                return;
            }
            try
            {
                if (embers != null && embers.playing && embers.followTransform == crystal.transform)
                    return;
                Color outer, mid, core;
                FlameColours(lvl, out outer, out mid, out core);
                embers = PoolManager.GetPoolItem<ParticleEffect>("SparkleAura");
                if (embers == null)
                    return;
                embers.Play(EmbersBase + EmbersPerLevel * lvl, crystal.transform.position,
                    new ParticleSystemOverride { startColor = (Color32)mid }, null, crystal.transform);
            }
            catch (Exception e)
            {
                LogOnce("embers", $"no embers ({e.Message})");
                embers = null;
            }
        }

        private void StopEmbers()
        {
            try
            {
                if (embers != null && embers.followTransform == crystal.transform)
                    embers.Stop();
            }
            catch
            {
            }
            embers = null;
        }

        // The burst when the level changes: a swell of the flame, sparks, dust off the base, a
        // small shake; smoke when the fire goes out.
        private void LevelChangedEffects(int from, int to)
        {
            flare = 1f;
            if (!EffectsOn)
                return;
            Vector3 flamePos = crystal.transform.position + new Vector3(0f, 0.4f, 0f);
            try
            {
                if (to > 0)
                {
                    Color outer, mid, core;
                    FlameColours(to, out outer, out mid, out core);
                    PoolManager.GetPoolItem<ParticleEffect>("SparkleAura").Emit(4 + to, flamePos,
                        new ParticleSystemOverride { startColor = (Color32)core });
                    PoolManager.GetPoolItem<DustEmitter>().EmitCircle(12 + 2 * to, 0.6f, 3f, -1f, transform.position);
                    CameraController.ShakeCamera(0.1f + 0.015f * to);
                }
                else
                {
                    PoolManager.GetPoolItem<ParticleEffect>("SmokeSmallEmitter").Emit(4, flamePos);
                    PoolManager.GetPoolItem<ParticleEffect>("SmokeEmitter").Emit(3, flamePos + Vector3.up * 0.4f);
                }
            }
            catch (Exception e)
            {
                LogOnce("burst", $"no level-change effects ({e.Message})");
            }
        }

        // Solid to walk into, like the game's own props: a box over the base's footprint, on the
        // layer the game uses for obstacles.
        private void AddCollider()
        {
            try
            {
                var go = new GameObject("Base");
                go.transform.SetParent(transform, false);
                go.layer = ObstacleLayer();
                BoxCollider2D box = go.AddComponent<BoxCollider2D>();
                box.size = new Vector2(18f / pixelsPerUnit, 6f / pixelsPerUnit);
                box.offset = new Vector2(0f, 3f / pixelsPerUnit);
                log?.LogInfo($"Ascension altar: solid on layer '{LayerMask.LayerToName(go.layer)}'");
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: couldn't make it solid ({e.Message})");
            }
        }

        private static int ObstacleLayer()
        {
            int named = LayerMask.NameToLayer("Obstacle");
            if (named >= 0)
                return named;
            int obstacles = ChaosCollisions.layerAllWallAndObst & ~ChaosCollisions.layerAllWall;
            int mask = obstacles != 0 ? obstacles : ChaosCollisions.layerAllWallAndObst;
            for (int i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                    return i;
            }
            return 0;
        }

        // Up to the highest level unlocked in this save (#32).
        private int Level => Mathf.Clamp(level.Value, 0, AscensionPlugin.Unlocked);

        private void Update()
        {
            try
            {
                Tick();
            }
            catch (Exception e)
            {
                log?.LogError($"Ascension altar: {e.Message}");
                Engaged = null;
                enabled = false;
            }
        }

        private void Tick()
        {
            flare = Mathf.MoveTowards(flare, 0f, Time.deltaTime / FlareSeconds);
            Animate(Level);
            if (Time.time >= nextEmberCheck)
                Embers(Level);

            Player near = NearestPlayer();
            SortAgainst(near);
            Engaged = near;
            if (near == null)
            {
                HidePrompt();
                greeted = false;
                return;
            }
            ShowPrompt(near);
            if (!greeted)
            {
                greeted = true;
                Announce("Interact to raise");
                if (!loggedNear)
                {
                    loggedNear = true;
                    log?.LogInfo("Ascension altar: a wizard is in reach");
                }
                LogSharedKeys(near);
            }
            // Only from standing or running, as the game's shops do: not mid-dash, mid-attack, in a
            // conversation or paused.
            if (InteractPressed(near) && CanInteract(near))
            {
                int from = Level;
                level.Value = Level >= AscensionPlugin.Unlocked ? 0 : Level + 1;
                ShowLevel();
                LevelChangedEffects(from, Level);
                log?.LogInfo($"Ascension altar: level set to {Level}");
                Announce(null);
                SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(transform.position), null, 24f, -1f,
                    0.8f + 0.06f * Level, false);
            }
        }

        private static bool CanInteract(Player p)
        {
            try
            {
                if (!p.IsAvailable || GameUI.dialogManager != null && GameUI.dialogManager.IsActive)
                    return false;
                string state = p.fsm != null ? p.fsm.currentStateName : null;
                return state == null || state.Contains("Idle") || state.Contains("Run");
            }
            catch
            {
                return true;
            }
        }

        private void Announce(string extra)
        {
            int lvl = Level;
            string header = lvl > 0 ? $"ASCENSION {lvl}" : "ASCENSION OFF";
            string info = AscensionLevels.Summary(lvl);
            int unlocked = AscensionPlugin.Unlocked;
            if (lvl == unlocked && unlocked < AscensionLevels.Max)
                info += $" - Beat it to unlock {unlocked + 1}";
            if (!string.IsNullOrEmpty(extra))
                info = extra + " - " + info;
            GameBanner.Show(header, info, Icon(lvl));
        }

        // The game's Interact button, read two ways (the game's own input, then Rewired's).
        private static bool InteractPressed(Player p)
        {
            ChaosInputDevice device = p.inputDevice;
            if (device == null)
                return false;
            try
            {
                if (device.GetButtonDown(InteractAction))
                    return true;
            }
            catch
            {
            }
            try
            {
                return device.rewiredPlayer != null && device.rewiredPlayer.GetButtonDown(InteractAction);
            }
            catch
            {
                return false;
            }
        }

        // ---- Keeping Interact from also attacking ----

        // On a gamepad (and in some keyboard layouts) Interact sits on a button that is also an
        // attack or dash, so raising the level used to fire a basic attack too. The game's NPCs
        // sidestep this by pausing the wizard for the whole conversation; the altar instead makes
        // the skill buttons read as released on the wizard standing at it for as long as Interact
        // is held. The patches below hook the game's own input reads, so it holds whichever order
        // the wizard and the altar update in.
        public static bool PassInput(ChaosInputDevice device, string buttonName, bool pressedThisFrame, ref bool result)
        {
            Player engaged = Engaged;
            if (engaged == null || device == null || device.player != engaged || buttonName == null || !buttonName.StartsWith("Skill"))
                return true;
            Rewired.Player rewired = device.rewiredPlayer;
            if (rewired == null)
                return true;
            bool wasDown;
            try
            {
                if (!rewired.GetButton(InteractAction) && !rewired.GetButtonDown(InteractAction))
                    return true;
                // The game polls every skill button each frame, so only a button that really was
                // down (one sharing Interact's key) is worth a log line.
                wasDown = pressedThisFrame ? rewired.GetButtonDown(buttonName) : rewired.GetButton(buttonName);
            }
            catch
            {
                return true;
            }
            result = false;
            if (wasDown && !loggedSwallow)
            {
                loggedSwallow = true;
                AscensionPlugin.Log($"Ascension altar: held back a '{buttonName}' press that shares Interact's button");
            }
            return false;
        }

        [HarmonyPatch(typeof(ChaosInputDevice), nameof(ChaosInputDevice.GetButtonDown))]
        public static class ButtonDownPatch
        {
            private static bool Prefix(ChaosInputDevice __instance, string buttonName, ref bool __result)
                => PassInput(__instance, buttonName, true, ref __result);
        }

        [HarmonyPatch(typeof(ChaosInputDevice), nameof(ChaosInputDevice.GetButton))]
        public static class ButtonPatch
        {
            private static bool Prefix(ChaosInputDevice __instance, string buttonName, ref bool __result)
                => PassInput(__instance, buttonName, false, ref __result);
        }

        // Once, for the log: what Interact is bound to on each kind of controller, and which other
        // actions share those buttons (the ones the patches above hold back).
        private void LogSharedKeys(Player p)
        {
            if (loggedKeys)
                return;
            loggedKeys = true;
            try
            {
                Rewired.Player rewired = p.inputDevice != null ? p.inputDevice.rewiredPlayer : null;
                if (rewired == null)
                    return;
                var maps = new List<Rewired.ControllerMap>();
                foreach (Rewired.ControllerType type in new[] { Rewired.ControllerType.Keyboard, Rewired.ControllerType.Mouse, Rewired.ControllerType.Joystick })
                {
                    maps.Clear();
                    rewired.controllers.maps.GetAllMaps(type, maps);
                    var keys = new List<string>();
                    var shared = new List<string>();
                    foreach (Rewired.ControllerMap map in maps)
                    {
                        foreach (Rewired.ActionElementMap element in map.AllMaps)
                        {
                            Rewired.InputAction action = Rewired.ReInput.mapping.GetAction(element.actionId);
                            if (action != null && action.name == InteractAction && !keys.Contains(element.elementIdentifierName))
                                keys.Add(element.elementIdentifierName);
                        }
                    }
                    if (keys.Count == 0)
                        continue;
                    foreach (Rewired.ControllerMap map in maps)
                    {
                        foreach (Rewired.ActionElementMap element in map.AllMaps)
                        {
                            Rewired.InputAction action = Rewired.ReInput.mapping.GetAction(element.actionId);
                            if (action != null && action.name != InteractAction && keys.Contains(element.elementIdentifierName))
                                shared.Add($"{action.name} ({element.elementIdentifierName})");
                        }
                    }
                    log?.LogInfo($"Ascension altar: {type} Interact = {string.Join(", ", keys.ToArray())}" +
                        (shared.Count > 0 ? $", shared with {string.Join(", ", shared.ToArray())}" : ", shared with nothing"));
                }
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: couldn't read the bindings ({e.Message})");
            }
        }

        private Player NearestPlayer()
        {
            Player[] players = GameController.activePlayers;
            if (players == null)
                return null;
            Player best = null;
            float bestDistance = Range;
            foreach (Player p in players)
            {
                if (p == null || !p.gameObject.activeInHierarchy)
                    continue;
                float d = Vector2.Distance(p.transform.position, (Vector2)transform.position + new Vector2(0f, 0.5f));
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = p;
                }
            }
            return best;
        }

        // In front of a wizard standing behind it, behind one standing in front. The halo and
        // ring sit behind the totem, the flame, runes and number in front.
        private void SortAgainst(Player p)
        {
            Player any = p;
            if (any == null)
            {
                Player[] players = GameController.activePlayers;
                any = players != null && players.Length > 0 ? players[0] : null;
            }
            if (any == null || any.spriteRenderer == null)
                return;
            int layer = any.spriteRenderer.sortingLayerID;
            int order = any.spriteRenderer.sortingOrder + (any.transform.position.y > transform.position.y ? 1 : -1);
            Sort(pedestal, layer, order);
            Sort(crystal, layer, order + 1);
            Sort(number, layer, order + 1);
            Sort(halo, layer, order - 1);
            Sort(ring, layer, order - 2);
            foreach (SpriteRenderer rune in runes)
                Sort(rune, layer, order + 1);
        }

        private static void Sort(SpriteRenderer r, int layer, int order)
        {
            if (r == null)
                return;
            r.sortingLayerID = layer;
            r.sortingOrder = order;
        }

        // The game's own "press this button" prompt over the altar.
        private void ShowPrompt(Player p)
        {
            if (promptShown)
                return;
            try
            {
                if (prompt == null)
                {
                    GameObject go = Instantiate(OverheadPrompt.Prefab);
                    prompt = go != null ? go.GetComponent<OverheadPrompt>() : null;
                    if (prompt == null)
                        return;
                    prompt.Initialize(transform, p.inputDevice.inputScheme, (TotemHeight + 8f) / pixelsPerUnit, 0f);
                }
                prompt.DisplayPrompt(InteractAction, false, false);
                promptShown = true;
            }
            catch (Exception e)
            {
                log?.LogInfo($"Ascension altar: no button prompt ({e.Message})");
                promptShown = true;
            }
        }

        private void HidePrompt()
        {
            if (!promptShown)
                return;
            promptShown = false;
            try
            {
                prompt?.HidePrompt("");
            }
            catch
            {
            }
        }

        private void OnDestroy()
        {
            if (Engaged != null)
                Engaged = null;
            StopEmbers();
            if (prompt != null)
                Destroy(prompt.gameObject);
        }

        private readonly HashSet<string> loggedOnce = new HashSet<string>();

        private void LogOnce(string key, string message)
        {
            if (loggedOnce.Add(key))
                log?.LogInfo($"Ascension altar: {message}");
        }

        // ---- Art ----

        // Soft-shaded like the game's own props: no dark outline, just light on top and shade
        // below, with gold on the brazier's rim.
        private static readonly Dictionary<char, Color32> Stone = new Dictionary<char, Color32>
        {
            { 'D', new Color32(0x5a, 0x55, 0x70, 0xff) },
            { 'M', new Color32(0x7c, 0x77, 0x94, 0xff) },
            { 'L', new Color32(0xa2, 0x9d, 0xb8, 0xff) },
            { 's', new Color32(0xc8, 0xc4, 0xd8, 0xff) },
            { 'k', new Color32(0x3f, 0x3b, 0x52, 0xff) }, // carved grooves and rune slots
            { 'G', new Color32(0xd6, 0xac, 0x4e, 0xff) }, // gold rim
            { 'y', new Color32(0x8e, 0x6b, 0x26, 0xff) }, // gold in shade
            { 'h', new Color32(0x45, 0x41, 0x58, 0x90) }, // soft ground shadow
        };

        // 20 x 38: the brazier (rows 0-6), the column with its rune slots (7-27), the plinth that
        // carries the number (28-33), the base (34-36) and the ground shadow (37).
        private static readonly string[] PedestalRows =
        {
            "..GGGGGGGGGGGGGGGG..",
            ".GyLLLLLLLLLLLLLLyG.",
            ".yMMMMMMMMMMMMMMMMy.",
            "..MMMMMMMMMMMMMMMM..",
            "..DMMMMMMMMMMMMMMD..",
            "...DDMMMMMMMMMMDD...",
            "....DDDMMMMMMDDD....",
            ".....sLLLLLLLLs.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkkkkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMkkMMkkMD.....",
            ".....LMMMMMMMMD.....",
            ".....LMkkkkkkMD.....",
            ".....LMMMMMMMMD.....",
            "...sLLLLLLLLLLLLs...",
            "...LMMMMMMMMMMMMD...",
            "...LMMMMMMMMMMMMD...",
            "...LMMMMMMMMMMMMD...",
            "...LMMMMMMMMMMMMD...",
            "...LMMMMMMMMMMMMD...",
            ".sLLLLLLLLLLLLLLLLs.",
            ".LMMMMMMMMMMMMMMMMD.",
            ".DDDDDDDDDDDDDDDDDD.",
            "hhhhhhhhhhhhhhhhhhhh",
        };

        private const float TotemHeight = 38f;
        // Pixel rows (from the bottom) the flame stands on and the number is centred on.
        private const float FlameRow = 36f;
        private const float NumberRow = 6.5f;

        // The rune slots, two columns of five up the column face, lit from the bottom up and left
        // to right: pixel offsets of each 2x2 slot's centre from the totem's bottom middle.
        private static float RuneX(int i) => i % 2 == 0 ? -2f : 2f;
        private static float RuneY(int i) => 14f + 3f * (i / 2);

        private static Sprite PedestalSprite()
        {
            if (pedestalSprite == null)
                pedestalSprite = Make(PedestalRows, c => Stone.ContainsKey(c) ? (Color32?)Stone[c] : null, new Vector2(0.5f, 0f));
            return pedestalSprite;
        }

        // A lit rune: a 2x2 white block, coloured per level.
        private static Sprite PipSprite()
        {
            if (pipSprite == null)
                pipSprite = Make(new[] { "##", "##" }, c => (Color32?)new Color32(255, 255, 255, 255), new Vector2(0.5f, 0.5f));
            return pipSprite;
        }

        // A soft disc that fades out to its edge, drawn behind the flame in its colour.
        private static Sprite HaloSprite()
        {
            if (haloSprite != null)
                return haloSprite;
            const int size = 48;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a * (3f - 2f * a); // smooth falloff
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            haloSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return haloSprite;
        }

        // A flattened ring of light on the ground around the base, with eight rune marks.
        private static Sprite RingSprite()
        {
            if (ringSprite != null)
                return ringSprite;
            const int w = 36, h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = w / 2f, cy = h / 2f, rx = w / 2f - 2f, ry = h / 2f - 2f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (d >= 0.86f && d <= 1f)
                        a = 1f;
                    else if (d > 1f && d <= 1.12f)
                    {
                        // Marks at eight points just outside the ring.
                        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f;
                        float nearest = Mathf.Repeat(angle + 22.5f, 45f) - 22.5f;
                        if (Mathf.Abs(nearest) < 7f)
                            a = 0.9f;
                    }
                    // The front (bottom) of the ring is a little brighter than the back.
                    if (a > 0f && y > cy)
                        a *= 0.7f;
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();
            ringSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), pixelsPerUnit);
            return ringSprite;
        }

        // Four flicker frames of a flame, in outer, middle and core colours (no outline).
        private static readonly string[][] FlameRows =
        {
            new[]
            {
                "....o.....",
                "....oo....",
                "...ooo..o.",
                "...omo..o.",
                "..oomoo.o.",
                "..ommmooo.",
                ".oomcmmoo.",
                ".ommccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
            new[]
            {
                ".....o....",
                "....oo....",
                ".o..ooo...",
                ".o..omo...",
                ".o.oomoo..",
                ".ooommmo..",
                ".oommcmoo.",
                ".ommccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
            new[]
            {
                "..........",
                ".....o....",
                "....ooo...",
                "...oomo...",
                "...ommoo..",
                "..oommmo..",
                "..ommcmoo.",
                ".oomccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
            new[]
            {
                "...o......",
                "...oo.....",
                "..ooo..o..",
                "..omoo.oo.",
                "..oomo.oo.",
                "..ommmooo.",
                ".oommcmoo.",
                ".ommccmmo.",
                ".omccccmo.",
                "oommcccmoo",
                "ommcccccmo",
                "ommcccccmo",
                ".ommcccmo.",
                "..ommmmo..",
            },
        };

        // Outer, middle and core colours at levels 1, 4, 7 and 10.
        private static readonly Color[][] Heat =
        {
            new Color[] { new Color32(0x2c, 0x4c, 0xc8, 0xff), new Color32(0x5c, 0x9c, 0xff, 0xff), new Color32(0xcc, 0xec, 0xff, 0xff) },
            new Color[] { new Color32(0x7a, 0x34, 0xc8, 0xff), new Color32(0xb0, 0x70, 0xff, 0xff), new Color32(0xf0, 0xdc, 0xff, 0xff) },
            new Color[] { new Color32(0xd0, 0x2a, 0x2a, 0xff), new Color32(0xff, 0x6a, 0x3a, 0xff), new Color32(0xff, 0xd8, 0xb0, 0xff) },
            new Color[] { new Color32(0xf0, 0x40, 0x10, 0xff), new Color32(0xff, 0xa0, 0x20, 0xff), new Color32(0xff, 0xf8, 0xd0, 0xff) },
        };

        // Cold blue when low, through to a white-hot orange at 10 (a grey ember when off).
        private static void FlameColours(int lvl, out Color outer, out Color mid, out Color core)
        {
            if (lvl <= 0)
            {
                outer = new Color32(0x5a, 0x58, 0x6e, 0xff);
                mid = new Color32(0x80, 0x7c, 0x96, 0xff);
                core = new Color32(0xa8, 0xa4, 0xbc, 0xff);
                return;
            }
            // Blue, then violet, then red, then orange with a white-hot core.
            float t = (lvl - 1) / (float)(AscensionLevels.Max - 1) * (Heat.Length - 1);
            int a = Mathf.Min(Heat.Length - 2, Mathf.FloorToInt(t));
            float f = t - a;
            outer = Color.Lerp(Heat[a][0], Heat[a + 1][0], f);
            mid = Color.Lerp(Heat[a][1], Heat[a + 1][1], f);
            core = Color.Lerp(Heat[a][2], Heat[a + 1][2], f);
        }

        public static Sprite FlameSprite(int lvl, int frame)
        {
            int key = lvl * 10 + frame;
            Sprite sprite;
            if (flameSprites.TryGetValue(key, out sprite) && sprite != null)
                return sprite;
            Color outer, mid, core;
            FlameColours(lvl, out outer, out mid, out core);
            sprite = Make(FlameRows[frame], c =>
            {
                switch (c)
                {
                    case 'o': return (Color32?)outer;
                    case 'm': return mid;
                    case 'c': return core;
                    default: return null;
                }
            }, new Vector2(0.5f, 0f));
            flameSprites[key] = sprite;
            return sprite;
        }

        // For the banner.
        public static Sprite Icon(int lvl) => FlameSprite(lvl, 0);

        // 3x5 pixel digits for the number on the altar's face.
        private static readonly string[] Digits =
        {
            "###,#.#,#.#,#.#,###", ".#.,##.,.#.,.#.,###", "###,..#,###,#..,###", "###,..#,.##,..#,###",
            "#.#,#.#,###,..#,..#", "###,#..,###,..#,###", "###,#..,###,#.#,###", "###,..#,..#,.#.,.#.",
            "###,#.#,###,#.#,###", "###,#.#,###,..#,###",
        };

        // The level carved into the plinth, glowing in the flame's colour.
        private static Sprite NumberSprite(int lvl)
        {
            Sprite sprite;
            if (numberSprites.TryGetValue(lvl, out sprite) && sprite != null)
                return sprite;
            string text = lvl.ToString();
            var rows = new string[5];
            for (int r = 0; r < 5; r++)
            {
                var line = new System.Text.StringBuilder();
                for (int d = 0; d < text.Length; d++)
                {
                    if (d > 0)
                        line.Append('.');
                    line.Append(Digits[text[d] - '0'].Split(',')[r]);
                }
                rows[r] = line.ToString();
            }
            Color outer, mid, core;
            FlameColours(lvl, out outer, out mid, out core);
            Color glow = lvl <= 0 ? (Color)new Color32(0x4a, 0x46, 0x5e, 0xff) : core;
            sprite = Make(rows, c => c == '#' ? (Color32?)glow : null, new Vector2(0.5f, 0.5f));
            numberSprites[lvl] = sprite;
            return sprite;
        }

        private static Sprite Make(string[] rows, Func<char, Color32?> colour, Vector2 pivot)
        {
            int w = rows[0].Length, h = rows.Length;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color32? c = colour(rows[h - 1 - y][x]);
                    tex.SetPixel(x, y, c.HasValue ? (Color)c.Value : new Color(0f, 0f, 0f, 0f));
                }
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), pivot, pixelsPerUnit);
        }
    }
}
