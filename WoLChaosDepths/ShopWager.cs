using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace WoLChaosDepths
{
    // Shopkeeper's Wager (Gambler tier 2, Matt 2026-11-03): a wizard with the talent presses
    // Interact at the ARCANA shopkeeper and is asked whether to take the bet (A / F yes, B / Esc
    // no; asked again on the next Interact after a no). Taking it flips a coin over their head.
    // Heads: that shop's arcana are half price. Tails: they cost double. Relic and potion shops
    // aren't part of it. Once a shop's coin has landed, Interact talks to the shopkeeper as usual.
    // One flip per shop per floor, shared in co-op; stacks with High Roller and Double Down. The flip
    // is WoL Art's fx_wager_flip (spin, then a starred gold heads or a violet tails); without it,
    // the Double or Nothing and Double Down coins. The shopkeeper reacts (a groan for heads,
    // an evil laugh for tails). Only in a run, while talents are on.
    public static class ShopWager
    {
        public const string Key = "ShopkeepersWager";
        private const string ModId = "ChaosDepths_TALENT_ShopkeepersWager";
        private const float ScanEvery = 0.25f, NpcReach = 8f;
        private const float Heads = -0.5f, Tails = 1f;
        // The question ignores the press that opened it, and the shopkeeper ignores the press
        // that answered it (it would open their usual chat).
        private const float AnswerDelay = 0.25f, QuietAfter = 0.4f;

        // Each shop's flip on this floor (by its shopkeeper; a stall without one is its own shop):
        // true heads, false tails. A shop mid-flip is in 'flipping' until the coin lands.
        private static readonly Dictionary<int, bool> outcome = new Dictionary<int, bool>();
        private static readonly HashSet<int> flipping = new HashSet<int>();
        private static readonly Dictionary<int, float> applied = new Dictionary<int, float>();
        // The shopkeepers who can offer the bet (plain Npcs selling for gold), and their shop.
        private static readonly Dictionary<Npc, int> keeperShop = new Dictionary<Npc, int>();
        private static readonly HashSet<string> said = new HashSet<string>();
        private static float nextScan, quietUntil;

        // The open question, if any.
        private class Question
        {
            public Player player;
            public Npc keeper;
            public int shop;
            public float opened;
        }

        private static Question asking;
        private static GUIStyle wrapStyle;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths Shop Wager");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
            GameController.levelLoadEventHandlers += (next, previous) =>
            {
                outcome.Clear();
                flipping.Clear();
                applied.Clear();
                keeperShop.Clear();
                asking = null;
                releasing = null;
            };
            new Harmony("mdbailey94.wol.chaosdepths.shopwager").Patch(AccessTools.Method(typeof(Npc), nameof(Npc.Interacted)),
                prefix: new HarmonyMethod(typeof(ShopWager), nameof(InteractedPrefix)));
        }

        private class Runner : MonoBehaviour
        {
            private void Update()
            {
                Guard(Tick);
                Guard(Answer);
                Guard(Release);
            }

            private void OnGUI()
            {
                if (asking != null)
                    Guard(DrawQuestion);
            }
        }

        private static void Guard(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                if (said.Add("error/" + e.Message))
                    ChaosDepthsPlugin.LogWarning($"Shop Wager: failed ({e.Message})");
            }
        }

        // Interact at a shopkeeper: a wizard with the talent is asked about the bet instead of
        // the usual chat, while that shop hasn't flipped yet.
        private static bool InteractedPrefix(Npc __instance)
        {
            try
            {
                int shop;
                if (!keeperShop.TryGetValue(__instance, out shop))
                    return true;
                if (Time.unscaledTime < quietUntil)
                    return false;
                Player p = __instance.player;
                if (p == null || asking != null || DialogManager.dialogInProgress || !Talents.Enabled || !ChaosDepthsPlugin.InTrialsNow()
                    || Talents.Rank(p, Key) <= 0 || outcome.ContainsKey(shop) || flipping.Contains(shop))
                    return true;
                asking = new Question { player = p, keeper = __instance, shop = shop, opened = Time.unscaledTime };
                p.fsm.ChangeState("Paused");
                __instance.FacePlayer();
                Emote(__instance, EmoteType.Hmm);
                ChaosDepthsPlugin.Log($"Shop Wager: player {p.playerID + 1} asked about the bet ({__instance.name})");
                return false;
            }
            catch (Exception e)
            {
                if (said.Add("error/prefix/" + e.Message))
                    ChaosDepthsPlugin.LogWarning($"Shop Wager: interact failed ({e.Message})");
                return true;
            }
        }

        // The answer: Confirm / Interact (A, F) takes the bet, Cancel (B, Esc) walks away.
        private static void Answer()
        {
            Question q = asking;
            if (q == null)
                return;
            if (q.player == null || q.keeper == null || !Talents.Enabled)
            {
                Close(q, false);
                return;
            }
            if (Time.unscaledTime - q.opened < AnswerDelay)
                return;
            ChaosInputDevice input = q.player.inputDevice;
            bool yes = Input.GetKeyDown(KeyCode.Y) || (input != null && (input.GetButtonDown("Confirm") || input.GetButtonDown("Interact")));
            bool no = Input.GetKeyDown(KeyCode.N) || (input != null && input.GetButtonDown("Cancel"));
            if (yes)
                Close(q, true);
            else if (no)
                Close(q, false);
        }

        // The wizard stays paused until the answering button is let go (and at least QuietAfter):
        // on a pad A is both Confirm and the dash, so freeing them on the press dashed (Matt
        // 2026-11-03).
        private static Player releasing;
        private static float releaseAfter;

        private static void Release()
        {
            Player p = releasing;
            if (p == null)
                return;
            ChaosInputDevice input = p.inputDevice;
            bool held = Input.GetKey(KeyCode.Y) || Input.GetKey(KeyCode.N)
                || (input != null && (input.GetButton("Confirm") || input.GetButton("Interact") || input.GetButton("Cancel")));
            if (Time.unscaledTime < releaseAfter || (held && Time.unscaledTime < releaseAfter + 2f))
                return;
            releasing = null;
            if (p.fsm != null && p.fsm.currentStateName != null && p.fsm.currentStateName.Contains("Paused"))
                p.fsm.ChangeState(p.fsm.defaultStateName);
        }

        private static void Close(Question q, bool take)
        {
            asking = null;
            quietUntil = Time.unscaledTime + QuietAfter;
            releasing = q.player;
            releaseAfter = Time.unscaledTime + QuietAfter;
            if (take)
                Flip(q.player, q.shop, q.keeper);
            else if (q.player != null)
            {
                Emote(q.keeper, EmoteType.No);
                ChaosDepthsPlugin.Log($"Shop Wager: player {q.player.playerID + 1} turned the bet down");
            }
        }

        // A box at the bottom of the screen, like the game's own dialog.
        private static void DrawQuestion()
        {
            int u = Mathf.Max(1, Mathf.FloorToInt(Screen.height / 360f));
            float w = Mathf.Min(Screen.width - 16 * u, 280 * u), h = 50 * u;
            var box = new Rect((Screen.width - w) / 2f, Screen.height - h - 24 * u, w, h);
            PixelUI.Panel(box, u);
            int font = Mathf.Max(10, 6 * u);
            float x = box.x + 6 * u, inner = box.width - 12 * u;
            GUI.Label(new Rect(x, box.y + 5 * u, inner, 9 * u), "SHOPKEEPER'S WAGER", PixelUI.Style(font, PixelUI.Accent));
            if (wrapStyle == null || wrapStyle.fontSize != font)
                wrapStyle = new GUIStyle(PixelUI.Style(font, PixelUI.Text)) { wordWrap = true };
            GUI.Label(new Rect(x, box.y + 14 * u, inner, 20 * u),
                "\"Feeling lucky, wizard? Heads, every arcana I sell is half price. Tails, they all cost double.\"", wrapStyle);
            GUI.Label(new Rect(x, box.yMax - 12 * u, inner, 9 * u), "A / F: TAKE THE BET        B / ESC: NO THANKS", PixelUI.Style(font, PixelUI.Dim));
        }

        private static void Tick()
        {
            bool active = Talents.Enabled && ChaosDepthsPlugin.InTrialsNow();
            if ((!active && applied.Count == 0) || Time.unscaledTime < nextScan)
                return;
            nextScan = Time.unscaledTime + ScanEvery;

            keeperShop.Clear();
            Npc[] npcs = null;
            // The arcana shop only (Matt 2026-11-03): relics and potions keep their prices.
            foreach (SkillStoreItem item in UnityEngine.Object.FindObjectsOfType<SkillStoreItem>())
            {
                if (item == null || item.usePlatinumCost || item.costStat == null)
                    continue;
                Npc keeper = item.parentNpc;
                if (keeper == null)
                {
                    if (npcs == null)
                        npcs = UnityEngine.Object.FindObjectsOfType<Npc>();
                    keeper = Nearest(npcs, item.transform.position);
                }
                int shop = keeper != null ? keeper.GetInstanceID() : item.GetInstanceID();
                // Only a plain shopkeeper asks (the game's NPCs with their own interact, like the
                // Shuffler, keep it).
                if (keeper != null && keeper.GetType() == typeof(Npc))
                    keeperShop[keeper] = shop;

                bool heads;
                float want = active && outcome.TryGetValue(shop, out heads) ? (heads ? Heads : Tails) : 0f;
                float had;
                int id = item.GetInstanceID();
                if (applied.TryGetValue(id, out had) && Mathf.Approximately(had, want))
                    continue;
                item.costStat.RemoveMod(ModId);
                if (!Mathf.Approximately(want, 0f))
                    item.costStat.AddMod(new NumVarStatMod(ModId, want, 10, VarStatModType.Multiplicative, false));
                applied[id] = want;
                item.UpdatePrice();   // the shown price follows the stat
            }
            if (!active)
                applied.Clear();
        }

        private static Npc Nearest(Npc[] npcs, Vector3 spot)
        {
            Npc best = null;
            float bestDistance = NpcReach;
            foreach (Npc npc in npcs)
            {
                if (npc == null)
                    continue;
                float d = Vector2.Distance(npc.transform.position, spot);
                if (d <= bestDistance)
                {
                    best = npc;
                    bestDistance = d;
                }
            }
            return best;
        }

        // The deal: the coin goes up over the wizard; the shop's prices change when it lands.
        private static void Flip(Player p, int shop, Npc keeper)
        {
            flipping.Add(shop);
            bool heads = UnityEngine.Random.value < 0.5f;
            ChaosDepthsPlugin.Notice("SHOPKEEPER'S WAGER: THE COIN IS IN THE AIR...", 1.2f);
            Action land = () => Land(p, shop, keeper, heads);
            if (!CoinFlip.Spawn(p, heads, land))
                land();
        }

        private static void Land(Player p, int shop, Npc keeper, bool heads)
        {
            flipping.Remove(shop);
            outcome[shop] = heads;
            nextScan = 0f;   // the prices change this frame
            Emote(keeper, heads ? EmoteType.Urgh : EmoteType.EvilLaugh);
            ChaosDepthsPlugin.Notice(heads ? "SHOPKEEPER'S WAGER: HEADS! HALF PRICE" : "SHOPKEEPER'S WAGER: TAILS! DOUBLE PRICE", 2.5f);
            ChaosDepthsPlugin.Log($"Shop Wager: player {(p != null ? p.playerID + 1 : 0)} flipped {(heads ? "heads, this shop is half price" : "tails, this shop costs double")}" +
                (keeper != null ? $" ({keeper.name})" : ""));
        }

        private static void Emote(Npc keeper, EmoteType emote)
        {
            try
            {
                if (keeper != null)
                    keeper.PlayEmote(emote);
            }
            catch
            {
            }
        }

        // The coin: tossed up from over the wizard's head on an arc, spinning (the gold strip),
        // then held where it landed: heads gold with a pop, tails turning cursed violet. Fades out.
        private class CoinFlip : MonoBehaviour
        {
            private const float Toss = 1.1f, Height = 1.6f, Lift = 1.9f, Hold = 1.5f, FadeTime = 0.4f;
            private const float SpinFps = 18f, TurnFps = 10f;
            private static Material material;
            private Player owner;
            private SpriteRenderer r;
            private Sprite[] spin, turn;
            private bool heads, landed;
            private Action land;
            private float t;

            public static bool Spawn(Player owner, bool heads, Action land)
            {
                SpriteRenderer body = owner.spriteRenderer;
                if (body == null)
                    return false;
                float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
                Vector2 centre = new Vector2(0.5f, 0.5f);
                Sprite[] spin, turn;
                // WoL Art's own strip (Log - Art 10:59): frames 0-3 spin, 4 heads (gold, a white
                // star), 5 tails (violet).
                Sprite[] wager = ItemArt.Frames("fx_wager_flip", 6, centre, ppu);
                if (wager != null && wager.Length >= 6)
                {
                    spin = new[] { wager[0], wager[1], wager[2], wager[3] };
                    turn = new[] { heads ? wager[4] : wager[5] };
                }
                else
                {
                    // Without it: Double or Nothing's gold coin spins, and tails turns violet
                    // through the last frames of Double Down's coin.
                    spin = ItemArt.Frames("fx_ks_DoubleOrNothing", 6, centre, ppu);
                    if (spin == null || spin.Length < 6)
                        return false;
                    Sprite[] curse = ItemArt.Frames("fx_pact_DoubleDown", 6, centre, ppu);
                    turn = heads || curse == null || curse.Length < 6 ? new[] { spin[0] } : new[] { curse[3], curse[4], curse[5] };
                }
                var go = new GameObject("ChaosDepths Shop Wager Coin");
                var flip = go.AddComponent<CoinFlip>();
                flip.owner = owner;
                flip.heads = heads;
                flip.land = land;
                flip.spin = spin;
                flip.turn = turn;
                flip.r = go.AddComponent<SpriteRenderer>();
                if (material == null)
                    material = new Material(Shader.Find("Sprites/Default"));
                flip.r.sharedMaterial = material;
                flip.r.sprite = spin[0];
                flip.Follow(Lift);
                return true;
            }

            private void Follow(float height)
            {
                SpriteRenderer body = owner.spriteRenderer;
                transform.position = owner.transform.position + new Vector3(0f, height, 0f);
                if (body != null)
                {
                    r.sortingLayerID = body.sortingLayerID;
                    r.sortingOrder = body.sortingOrder + 20;
                }
            }

            private void Update()
            {
                if (owner == null)
                {
                    Destroy(gameObject);
                    return;
                }
                t += Time.deltaTime;
                if (t < Toss)
                {
                    float u = t / Toss;
                    Follow(Lift + Height * 4f * u * (1f - u));
                    r.sprite = spin[(int)(t * SpinFps) % spin.Length];
                    return;
                }
                if (!landed)
                {
                    landed = true;
                    land();
                }
                float since = t - Toss;
                Follow(Lift);
                r.sprite = turn[Mathf.Min(turn.Length - 1, (int)(since * TurnFps))];
                if (heads)
                {
                    float pop = 1f + 0.4f * Mathf.Max(0f, 1f - since / 0.25f);
                    transform.localScale = new Vector3(pop, pop, 1f);
                }
                Color c = r.color;
                c.a = Mathf.Clamp01((Hold - since) / FadeTime);
                r.color = c;
                if (since >= Hold)
                    Destroy(gameObject);
            }

            // The prices change even if the coin goes early (a level load, the wizard gone).
            private void OnDestroy()
            {
                if (!landed)
                {
                    landed = true;
                    land();
                }
            }
        }
    }
}
