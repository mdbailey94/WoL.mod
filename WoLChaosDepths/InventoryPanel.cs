using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

namespace WoLChaosDepths
{
    // M0 technical test: one custom screen, controller first. Opens over a paused game and shows
    // the character (level, experience bar), the three equipped slots and the inventory. Move with
    // the stick or the arrow keys, Confirm (A / Enter) equips or unequips, holding Confirm (or
    // Delete) salvages the item into Chaos Dust, Cancel (B / Esc) or the open key closes.
    //
    // Drawn with IMGUI in the pixel style of the Ascension altar prompt (PixelUI), in whole pixel
    // units so it stays crisp at any resolution.
    public partial class InventoryPanel
    {
        private const float RepeatFirst = 0.3f, RepeatNext = 0.11f, SalvageHold = 0.7f;
        private float nextDepthRepeat;
        private static readonly int[] DustByRarity = { 1, 3, 8, 20 };
        // The list is a filtered, sorted view of the inventory (task sheet #7): a tab per item
        // type and a sort order, equipped items first.
        private static readonly string[] TabNames = { "All", "Amulet", "Ring", "Charm" };
        private static readonly string[] SortNames = { "Rarity", "Level", "Slot", "Newest" };
        private int tab, sortMode;
        private readonly List<ItemData> view = new List<ItemData>();
        private int rows = 4;   // rows that fit, set while drawing
        private float nextTabRepeat;
        // In the Hub, Up from the top of the list moves the cursor onto the depth line, where
        // left / right pick the depth; Down goes back to the list. (A held Y + left / right
        // never reached the depth in testing, so the depth line got its own place instead.)
        private bool depthFocus;
        private static readonly Color Gain = new Color32(0x6f, 0xe0, 0x7a, 0xff), Loss = new Color32(0xff, 0x6a, 0x6a, 0xff), EquippedMark = new Color32(0x6f, 0xe0, 0x7a, 0xff);

        public bool Open { get; private set; }
        private Player player;
        private CharacterSave save;
        private int selected;
        private int scroll;
        private float nextRepeat;
        private float confirmHeldSince = -1f;
        private bool confirmConsumed;
        private float openedAt;
        // Both wizards can have their inventory open at once (0.17.0, Matt): one panel each, the
        // game paused while any is open and unpaused when the last one closes.
        private static int openCount;
        private static bool pausedByUs;
        public static int OpenCount => openCount;
        public static bool PausedByPanels => openCount > 0 && pausedByUs;
        public int ClosedFrame { get; private set; } = -1;
        private readonly Action<Player, CharacterSave> onChanged;

        public InventoryPanel(Action<Player, CharacterSave> onChanged)
        {
            this.onChanged = onChanged;
        }

        // ---- Opening and closing ----

        public void Show(Player player, CharacterSave save)
        {
            this.player = player;
            this.save = save;
            selected = 0;
            scroll = 0;
            confirmHeldSince = -1f;
            confirmConsumed = true; // the press that opened it isn't an equip
            openedAt = Time.unscaledTime;
            if (Open)
                return;
            Open = true;
            openCount++;
            releaseWaitUntil = -1f;
            Rebuild();
            try
            {
                if (!TimeScaleController.paused)
                {
                    TimeScaleController.Pause(true);
                    pausedByUs = true;
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"pause: {e.Message}");
                Time.timeScale = 0f;
                pausedByUs = true;
            }
            try
            {
                GameController.PauseAllPlayers(true);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"pause players: {e.Message}");
            }
            ChaosDepthsPlugin.Log($"Inventory opened by player {player.playerID + 1}: {save.inventory.Count} item(s)");
        }

        public void Close()
        {
            if (!Open)
                return;
            Open = false;
            ClosedFrame = Time.frameCount;
            openCount = Mathf.Max(0, openCount - 1);
            ChaosDepthsPlugin.Log($"Inventory closed (player {(player != null ? player.playerID + 1 : 0)}{(openCount > 0 ? $", {openCount} still open" : "")})");
            // The game stays paused while the other wizard's inventory is still open.
            if (openCount > 0)
                return;
            try
            {
                if (pausedByUs)
                    TimeScaleController.Pause(false);
            }
            catch
            {
                Time.timeScale = 1f;
            }
            pausedByUs = false;
            // The wizards stay frozen until the button that closed the panel is let go: the game
            // reads skills as held buttons, so up to 0.17.0 closing with B cast B's arcana.
            releaseWaitUntil = Time.unscaledTime + 1f;
            TickRelease();
        }

        private static float releaseWaitUntil = -1f;

        // Every frame from the plugin: lets the wizards move once no skill button is held (or
        // after a second, so a stuck button can't keep them frozen).
        public static void TickRelease()
        {
            if (releaseWaitUntil < 0f || openCount > 0)
                return;
            if (Time.unscaledTime < releaseWaitUntil && SkillButtonHeld())
                return;
            releaseWaitUntil = -1f;
            try
            {
                GameController.PauseAllPlayers(false);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"unpause players: {e.Message}");
            }
        }

        private static bool SkillButtonHeld()
        {
            try
            {
                Player[] players = GameController.activePlayers;
                if (players == null)
                    return false;
                foreach (Player p in players)
                {
                    Rewired.Player input = p != null && p.inputDevice != null ? p.inputDevice.rewiredPlayer : null;
                    if (input == null)
                        continue;
                    for (int i = 0; i < 6; i++)
                        if (input.GetButton("Skill" + i))
                            return true;
                }
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"release check: {e.Message}");
            }
            return false;
        }

        // ---- Input (unscaled time: the game is paused) ----

        public void Update()
        {
            if (!Open)
                return;
            if (player == null || !player.gameObject.activeInHierarchy)
            {
                Close();
                return;
            }
            // Only the owner's inputs drive the panel when two wizards play (#30).
            ChaosDepthsPlugin.PanelInputs(player, out bool useKeys, out bool usePad);
            Rewired.Player pad = usePad && player.inputDevice != null ? player.inputDevice.rewiredPlayer : null;
            bool Key(KeyCode k) => useKeys && Input.GetKey(k);
            bool KeyDown(KeyCode k) => useKeys && Input.GetKeyDown(k);

            // B (Circle) and the pad's open button are read off the pad itself (0.16.1): the
            // game's Cancel action doesn't come through while the panel has the game paused, so a
            // controller couldn't close it. The open button is ignored for a moment after opening.
            bool padClose = PadButton(pad, 1, BNames) || (Time.unscaledTime - openedAt > 0.25f && PadOpenButton(pad));
            if (padClose || Pressed(pad, "Cancel") || KeyDown(KeyCode.Escape) || (useKeys && ChaosDepthsPlugin.OpenKeyPressed()))
            {
                Close();
                return;
            }

            Rebuild();

            // Left and right: the item-type tab (All / Amulet / Ring / Charm). In the Hub, with
            // Interact (Y) held, or Q / E on the keyboard: the depth the next dive starts at.
            float horizontal = 0f;
            if (Key(KeyCode.LeftArrow) || Key(KeyCode.A))
                horizontal = -1f;
            else if (Key(KeyCode.RightArrow) || Key(KeyCode.D))
                horizontal = 1f;
            else if (pad != null)
            {
                try
                {
                    horizontal = pad.GetAxis("MoveHorizontal");
                }
                catch
                {
                }
            }
            bool depthKeys = Key(KeyCode.Q) || Key(KeyCode.E);
            bool inHub = Depth.Enabled && !ChaosDepthsPlugin.InTrialsNow();
            if (!inHub)
                depthFocus = false;
            float depthMove = depthKeys ? (Key(KeyCode.E) ? 1f : -1f) : depthFocus ? horizontal : 0f;
            if (Mathf.Abs(depthMove) > 0.5f && inHub)
            {
                if (Time.unscaledTime >= nextDepthRepeat)
                {
                    bool first = nextDepthRepeat == 0f;
                    nextDepthRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext);
                    int before = save.depthChosen;
                    save.depthChosen = Mathf.Clamp(save.depthChosen + (depthMove > 0f ? 1 : -1), 1, Mathf.Max(1, save.depthReached));
                    if (save.depthChosen != before)
                    {
                        ChaosDepthsPlugin.Log($"Next dive starts at depth {save.depthChosen} (waypoint {save.depthReached})");
                        onChanged(player, save);
                    }
                }
            }
            else
                nextDepthRepeat = 0f;
            if (Mathf.Abs(horizontal) > 0.5f && !depthFocus && !depthKeys)
            {
                if (Time.unscaledTime >= nextTabRepeat)
                {
                    bool first = nextTabRepeat == 0f;
                    nextTabRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext * 2f);
                    tab = (tab + (horizontal > 0f ? 1 : TabNames.Length - 1)) % TabNames.Length;
                    selected = 0;
                    scroll = 0;
                    Rebuild();
                }
            }
            else
                nextTabRepeat = 0f;

            // Y (Triangle), Interact or Tab: the next sort order. Y is read off the pad itself,
            // since the game doesn't put Interact on it.
            if (PadY(pad) || Pressed(pad, "Interact") || KeyDown(KeyCode.Tab))
            {
                sortMode = (sortMode + 1) % SortNames.Length;
                selected = 0;
                scroll = 0;
                Rebuild();
            }

            // Up and down with repeat.
            float vertical = 0f;
            if (Key(KeyCode.DownArrow) || Key(KeyCode.S))
                vertical = -1f;
            else if (Key(KeyCode.UpArrow) || Key(KeyCode.W))
                vertical = 1f;
            else if (pad != null)
            {
                try
                {
                    vertical = pad.GetAxis("MoveVertical");
                }
                catch
                {
                }
            }
            int count = Mathf.Max(1, view.Count);
            if (Mathf.Abs(vertical) > 0.5f)
            {
                if (Time.unscaledTime >= nextRepeat)
                {
                    bool first = nextRepeat == 0f;
                    nextRepeat = Time.unscaledTime + (first ? RepeatFirst : RepeatNext);
                    bool inHubNow = Depth.Enabled && !ChaosDepthsPlugin.InTrialsNow();
                    if (depthFocus)
                    {
                        if (vertical < 0f)
                            depthFocus = false;   // Down: back to the list
                        return;
                    }
                    if (vertical > 0f && selected == 0 && inHubNow)
                    {
                        depthFocus = true;        // Up from the top: onto the depth line
                        return;
                    }
                    selected = (selected + (vertical < 0f ? 1 : count - 1)) % count;
                    if (selected < scroll)
                        scroll = selected;
                    if (selected >= scroll + rows)
                        scroll = selected - rows + 1;
                }
            }
            else
                nextRepeat = 0f;

            // Confirm: tap equips, hold salvages.
            bool confirmDown = Held(pad, "Confirm") || Key(KeyCode.Return) || Key(KeyCode.Space);
            if (confirmDown && depthFocus)
            {
                confirmConsumed = true;   // Confirm on the depth line does nothing
            }
            else if (confirmDown)
            {
                if (confirmHeldSince < 0f)
                    confirmHeldSince = Time.unscaledTime;
                else if (!confirmConsumed && Time.unscaledTime - confirmHeldSince >= SalvageHold)
                {
                    confirmConsumed = true;
                    Salvage();
                }
            }
            else
            {
                if (confirmHeldSince >= 0f && !confirmConsumed)
                    ToggleEquip();
                confirmHeldSince = -1f;
                confirmConsumed = false;
            }
            if (KeyDown(KeyCode.Delete))
                Salvage();
            // X (Square) or G: drop the item on the floor (0.17.0).
            if (PadButton(pad, 2, XNames) || KeyDown(KeyCode.G))
                Drop();
        }

        private static readonly string[] XNames = { "x", "x button", "square" };

        // Drops the selected item on the floor at the wizard's feet, a little ahead (taken off
        // first if it's worn). Anyone can pick it up, so it can be handed to the other wizard; left
        // there, it comes back when the floor is left (Pickups).
        private void Drop()
        {
            ItemData item = Selected;
            if (item == null)
                return;
            if (!Pickups.Physical)
            {
                ChaosDepthsPlugin.Notice("Drops are off ([Loot] PhysicalDrops)");
                return;
            }
            int slot = SlotOf(item);
            bool worn = IsEquipped(item);
            if (worn && slot >= 0)
                save.equipped[slot] = "";
            save.inventory.Remove(item);
            Rebuild();
            onChanged(player, save);
            Vector2 ahead = Entity.GetFacingDirectionVector(player.facingDirection);
            Vector3 at = player.transform.position + (Vector3)(ahead * 0.9f);
            Pickups.Spawn(null, item, at, player);
            ChaosDepthsPlugin.Notice($"Dropped: {item.rarity} {item.name}", 2f);
            ChaosDepthsPlugin.Log($"Player {player.playerID + 1} dropped {item.rarity} {item.name}{(worn ? " (taken off first)" : "")}");
        }

        private static readonly string[] YNames = { "y", "y button", "triangle" };
        private static readonly string[] BNames = { "b", "b button", "circle" };

        private static bool PadY(Rewired.Player pad) => PadButton(pad, 3, YNames);

        // A button just pressed on one of this player's pads (XInput index, or its name).
        private static bool PadButton(Rewired.Player pad, int index, string[] names)
        {
            try
            {
                if (pad != null)
                    foreach (Rewired.Joystick joystick in pad.controllers.Joysticks)
                        if (ChaosDepthsPlugin.PadButtonJustPressed(joystick, index, names))
                            return true;
            }
            catch
            {
            }
            return false;
        }

        // The pad button that opens the inventory ([Panel] ControllerButton) closes it too.
        private static bool PadOpenButton(Rewired.Player pad)
        {
            try
            {
                if (pad != null)
                    foreach (Rewired.Joystick joystick in pad.controllers.Joysticks)
                        if (ChaosDepthsPlugin.OpenButtonJustPressed(joystick))
                            return true;
            }
            catch
            {
            }
            return false;
        }

        private static bool Pressed(Rewired.Player pad, string action)
        {
            try
            {
                return pad != null && pad.GetButtonDown(action);
            }
            catch
            {
                return false;
            }
        }

        private static bool Held(Rewired.Player pad, string action)
        {
            try
            {
                return pad != null && pad.GetButton(action);
            }
            catch
            {
                return false;
            }
        }

        // ---- Actions ----

        private ItemData Selected => view.Count == 0 ? null : view[Mathf.Clamp(selected, 0, view.Count - 1)];

        // The list as shown: the tab's items, equipped ones first, then by the sort order.
        private void Rebuild()
        {
            ItemData keep = Selected;
            view.Clear();
            foreach (ItemData item in save.inventory)
                if (tab == 0 || item.slot == TabNames[tab])
                    view.Add(item);
            var order = new Dictionary<ItemData, int>();
            for (int i = 0; i < save.inventory.Count; i++)
                order[save.inventory[i]] = i;
            view.Sort((a, b) =>
            {
                int c = IsEquipped(b).CompareTo(IsEquipped(a));
                if (c != 0)
                    return c;
                switch (sortMode)
                {
                    case 1: // Level
                        c = b.itemLevel.CompareTo(a.itemLevel);
                        if (c == 0) c = LootTable.RarityIndex(b.rarity).CompareTo(LootTable.RarityIndex(a.rarity));
                        break;
                    case 2: // Slot
                        c = SlotOf(a).CompareTo(SlotOf(b));
                        if (c == 0) c = LootTable.RarityIndex(b.rarity).CompareTo(LootTable.RarityIndex(a.rarity));
                        if (c == 0) c = b.itemLevel.CompareTo(a.itemLevel);
                        break;
                    case 3: // Newest
                        c = order[b].CompareTo(order[a]);
                        break;
                    default: // Rarity
                        c = LootTable.RarityIndex(b.rarity).CompareTo(LootTable.RarityIndex(a.rarity));
                        if (c == 0) c = SlotOf(a).CompareTo(SlotOf(b));
                        if (c == 0) c = b.itemLevel.CompareTo(a.itemLevel);
                        break;
                }
                return c != 0 ? c : order[a].CompareTo(order[b]);
            });
            if (keep != null)
            {
                int at = view.IndexOf(keep);
                if (at >= 0)
                    selected = at;
            }
            selected = Mathf.Clamp(selected, 0, Mathf.Max(0, view.Count - 1));
            if (selected < scroll)
                scroll = selected;
            if (selected >= scroll + rows)
                scroll = Mathf.Max(0, selected - rows + 1);
        }

        private int SlotOf(ItemData item) => Array.IndexOf(LootTable.Slots, item.slot);

        private bool IsEquipped(ItemData item)
        {
            int slot = SlotOf(item);
            return slot >= 0 && save.equipped[slot] == item.id;
        }

        private void ToggleEquip()
        {
            ItemData item = Selected;
            if (item == null)
                return;
            int slot = SlotOf(item);
            if (slot < 0)
                return;
            save.equipped[slot] = IsEquipped(item) ? "" : item.id;
            ChaosDepthsPlugin.Log($"{(IsEquipped(item) ? "Equipped" : "Unequipped")} {item.rarity} {item.name}");
            Rebuild();
            onChanged(player, save);
        }

        private void Salvage()
        {
            ItemData item = Selected;
            if (item == null)
                return;
            if (IsEquipped(item))
            {
                ChaosDepthsPlugin.Log($"Salvage refused: {item.rarity} {item.name} is equipped");
                ChaosDepthsPlugin.Notice("Unequip it before salvaging");
                return;
            }
            int rarity = Mathf.Clamp(LootTable.RarityIndex(item.rarity), 0, DustByRarity.Length - 1);
            save.dust += DustByRarity[rarity];
            save.inventory.Remove(item);
            Rebuild();
            ChaosDepthsPlugin.Log($"Salvaged {item.rarity} {item.name} for {DustByRarity[rarity]} dust ({save.dust} in all)");
            onChanged(player, save);
        }

        // ---- Drawing ----

        // The screen behind is dimmed once, by the first panel drawn (two would darken it twice).
        private bool DimScreen = true;

        public void Draw(bool dimScreen = true)
        {
            if (!Open || save == null)
                return;
            DimScreen = dimScreen;
            // WoL Art's pieces on the mockup's layout (InventoryPanelArt.cs, #16), unless they
            // can't be had or [Panel] Look says Coded.
            if (DrawnLook && DrawArt())
                return;
            Matrix4x4 previousMatrix = GUI.matrix;
            Color previousColor = GUI.color;
            GUI.depth = -100;
            try
            {
                // Sized to fill most of the screen's height (about 92%), so the text is big enough
                // to read: 5 units and a 20 px font at 720p, 8 and 32 px at 1080p.
                int unitsTall = Depth.Enabled ? 124 : 118;
                int u = Mathf.Max(3, Mathf.FloorToInt(Screen.height * 0.92f / unitsTall));
                // Two open at once (0.17.0): each fits its half of the screen.
                if (OpenCount > 1)
                    u = Mathf.Max(2, Mathf.Min(u, Mathf.FloorToInt(Screen.width * 0.48f / 176f)));
                int font = Mathf.Max(12, 4 * u);
                float w = 176 * u, h = unitsTall * u;
                // Two players: on the opening player's side of the screen (player 1 left, player 2 right).
                int activePlayers = 0;
                if (GameController.activePlayers != null)
                    foreach (Player p in GameController.activePlayers)
                        if (p != null && p.gameObject.activeInHierarchy)
                            activePlayers++;
                float left = (Screen.width - w) / 2f;
                if (activePlayers > 1 && player != null)
                    left = player.playerID == 0 ? 4f * u : Screen.width - w - 4f * u;
                left = Mathf.Clamp(left, 0f, Mathf.Max(0f, Screen.width - w));
                var panel = new Rect(left, (Screen.height - h) / 2f, w, h);
                if (DimScreen)
                    PixelUI.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.45f));
                PixelUI.Panel(panel, u);

                GUIStyle text = PixelUI.Style(font, PixelUI.Text);
                GUIStyle dim = PixelUI.Style(font, PixelUI.Dim);
                GUIStyle right = PixelUI.Style(font, PixelUI.Dim, TextAnchor.UpperRight);
                float x = panel.x + 5 * u, y = panel.y + 4 * u, lineH = 5 * u;

                // Header: name, level, experience bar, dust.
                Label(new Rect(x, y, w, lineH), $"{save.name}   Level {save.level}", text);
                Label(new Rect(x, y - 3 * u, w - 10 * u, lineH), "CHAOS DEPTHS", PixelUI.Style(Mathf.Max(10, 3 * u), PixelUI.Accent, TextAnchor.UpperCenter));
                Label(new Rect(x, y, w - 10 * u, lineH), $"Chaos Dust {save.dust}   Kills {save.kills}", right);
                y += lineH;
                bool inHubNow = Depth.Enabled && !ChaosDepthsPlugin.InTrialsNow();
                if (Depth.Enabled)
                {
                    string depthText = !inHubNow
                        ? $"Depth {Depth.Current}   Waypoint {save.depthReached}"
                        : depthFocus
                            ? $"Next dive: depth < {save.depthChosen} >   Waypoint {save.depthReached}   (left / right; down to the list)"
                            : $"Next dive: depth {save.depthChosen}   Waypoint {save.depthReached}   (up from the list, or Q / E)";
                    if (depthFocus)
                    {
                        PixelUI.Fill(new Rect(x - u, y, w - 8 * u, lineH), new Color(1f, 1f, 1f, 0.12f));
                        PixelUI.Fill(new Rect(x - u, y, u, lineH), PixelUI.Border);
                    }
                    Label(new Rect(x, y, w, lineH), depthText, depthFocus ? text : dim);
                    y += lineH;
                }
                int need = ChaosDepthsPlugin.XpToNext(save.level);
                float barW = w - 10 * u;
                PixelUI.Fill(new Rect(x, y + u, barW, 2 * u), PixelUI.PipEmpty);
                PixelUI.Fill(new Rect(x, y + u, barW * Mathf.Clamp01(save.xp / (float)Mathf.Max(1, need)), 2 * u), PixelUI.Accent);
                Label(new Rect(x, y + 3 * u, w, lineH), $"{save.xp} / {need} xp", dim);
                y += lineH + 3 * u;
                PixelUI.Fill(new Rect(x, y, barW, u), PixelUI.InnerLine);
                y += 2 * u;

                // Tab row (WoL Art's mockup): < ALL amulet ring charm >, the chosen tab lighter
                // with a gold underline; the sort order on the right.
                float tabH = 8 * u;
                float tx = x + 4 * u;
                PixelUI.Arrow(x, y + tabH * 0.5f, 5, false, PixelUI.Dim, u);
                for (int i = 0; i < TabNames.Length; i++)
                {
                    float tw = i == 0 ? 12 * u : 9 * u;
                    var tr = new Rect(tx, y, tw, tabH - u);
                    PixelUI.Fill(tr, i == tab ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.05f));
                    if (i == tab)
                        PixelUI.Fill(new Rect(tr.x, tr.yMax, tr.width, u), PixelUI.Border);
                    if (i == 0)
                        Label(new Rect(tr.x, tr.y + u, tr.width, lineH), "ALL", PixelUI.Style(font, i == tab ? PixelUI.Text : PixelUI.Dim, TextAnchor.UpperCenter));
                    else
                    {
                        float ip = 5 * u;
                        Icon(new ItemData { slot = TabNames[i], rarity = i == tab ? "Rare" : "Common" }, new Rect(tr.x + (tw - ip) / 2f, tr.y + (tr.height - ip) / 2f, ip, ip));
                    }
                    tx += tw + u;
                }
                PixelUI.Arrow(tx + u, y + tabH * 0.5f, 5, true, PixelUI.Dim, u);
                Label(new Rect(x, y, barW, lineH), $"SORT: {SortNames[sortMode].ToUpperInvariant()}", PixelUI.Style(font, PixelUI.Text, TextAnchor.UpperRight));
                Label(new Rect(x, y + lineH - u, barW, lineH), "(Y / Tab) CHANGE", right);
                y += tabH + u;

                // Left: the equipped slots, each an icon in its rarity frame with the slot's name.
                // Right: the list, two lines a row (name, then the level), equipped first.
                float leftW = 50 * u, listX = x + leftW + 3 * u, listW = barW - leftW - 3 * u;
                float detailY = panel.yMax - 32 * u;
                float top = y;
                Label(new Rect(x, y, leftW, lineH), "EQUIPPED", dim);
                y += lineH;
                float slotPx = 12 * u;
                for (int i = 0; i < LootTable.Slots.Length; i++)
                {
                    ItemData item = StatMods.FindItem(save, save.equipped[i]);
                    var box = new Rect(x, y, slotPx, slotPx);
                    Frame(box, item != null ? RarityColour(item) : PixelUI.InnerLine, u);
                    if (item != null)
                        Icon(item, new Rect(box.x + 2 * u, box.y + 2 * u, slotPx - 4 * u, slotPx - 4 * u));
                    else
                        Icon(new ItemData { slot = LootTable.Slots[i], rarity = "Common" }, new Rect(box.x + 2 * u, box.y + 2 * u, slotPx - 4 * u, slotPx - 4 * u), 0.25f);
                    Label(new Rect(box.xMax + 2 * u, y + u, leftW - slotPx - 2 * u, lineH), item != null ? Fit(item.name, Rarity(item, font), leftW - slotPx - 2 * u) : LootTable.Slots[i].ToUpperInvariant(), item != null ? Rarity(item, font) : dim);
                    Label(new Rect(box.xMax + 2 * u, y + u + lineH - u, leftW - slotPx - 2 * u, lineH), item != null ? $"LV {item.itemLevel}" : "EMPTY", dim);
                    y += slotPx + 2 * u;
                }
                // What it all adds up to, in the room left above the detail box.
                List<string> totals = Totals(save);
                float totalsBottom = detailY - 3 * u;
                if (totals.Count > 0 && y + 2 * u + 2 * lineH <= totalsBottom)
                {
                    PixelUI.Fill(new Rect(x, y, leftW, u), PixelUI.InnerLine);
                    y += 2 * u;
                    Label(new Rect(x, y, leftW, lineH), "TOTALS", dim);
                    y += lineH;
                    int room = Mathf.Max(1, Mathf.FloorToInt((totalsBottom - y) / lineH));
                    for (int i = 0; i < totals.Count && i < room; i++)
                    {
                        string line = totals[i];
                        if (i == room - 1 && totals.Count > room)
                            line = string.Join(", ", totals.GetRange(i, totals.Count - i).ToArray());
                        Label(new Rect(x + u, y, leftW - u, lineH), Fit(line, text, leftW - u), text);
                        y += lineH;
                    }
                }

                y = top;
                float rowH = 11 * u;
                rows = Mathf.Max(1, Mathf.FloorToInt((detailY - 3 * u - y - lineH) / rowH));
                if (selected >= scroll + rows)
                    scroll = Mathf.Max(0, selected - rows + 1);
                Label(new Rect(listX, y, listW, lineH), $"{TabNames[tab].ToUpperInvariant()} ({view.Count})", dim);
                if (view.Count > rows)
                    Label(new Rect(listX, y, listW - u, lineH), $"{scroll + 1}-{Mathf.Min(scroll + rows, view.Count)} of {view.Count}", right);
                y += lineH;
                if (view.Count == 0)
                    Label(new Rect(listX, y, listW, lineH), save.inventory.Count == 0 ? "Nothing yet. Items drop from enemies in the trials." : "Nothing of this type.", dim);
                for (int row = 0; row < rows; row++)
                {
                    int index = scroll + row;
                    if (index >= view.Count)
                        break;
                    ItemData item = view[index];
                    var line = new Rect(listX, y, listW, rowH - u);
                    if (index == selected)
                    {
                        PixelUI.Fill(line, new Color(1f, 1f, 1f, 0.12f));
                        PixelUI.Fill(new Rect(line.x, line.y, u, line.height), PixelUI.Border);
                    }
                    float rowIcon = 9 * u;
                    var box = new Rect(listX + 2 * u, y + (rowH - u - rowIcon) / 2f, rowIcon, rowIcon);
                    Frame(box, RarityColour(item), u);
                    Icon(item, new Rect(box.x + u, box.y + u, rowIcon - 2 * u, rowIcon - 2 * u));
                    float nameX = box.xMax + 2 * u, nameW = listW - (nameX - listX) - 5 * u;
                    Label(new Rect(nameX, y + u, nameW, lineH), Fit(item.name, Rarity(item, font), nameW), Rarity(item, font));
                    Label(new Rect(nameX, y + u + lineH - u, nameW, lineH), $"LV {item.itemLevel}   {item.rarity}", dim);
                    if (IsEquipped(item))
                        Label(new Rect(listX, y + u, listW - 2 * u, lineH), "E", PixelUI.Style(font, EquippedMark, TextAnchor.UpperRight));
                    y += rowH;
                }

                // The detail box: the chosen item's name and type, its description over up to two
                // lines, and what equipping it changes against the worn item (gains green,
                // losses red); the salvage hold bar under it.
                PixelUI.Fill(new Rect(x, detailY - 2 * u, barW, u), PixelUI.InnerLine);
                ItemData chosen = Selected;
                if (chosen != null)
                {
                    GUIStyle chosenStyle = Rarity(chosen, font);
                    float ty = detailY;
                    float textBottom = panel.yMax - 10 * u;
                    string kind = (string.IsNullOrEmpty(chosen.unique) ? chosen.rarity : "Unique") + " " + chosen.slot;
                    Label(new Rect(x, ty, barW, lineH), Fit($"{chosen.name}   ({kind}, item level {chosen.itemLevel})", chosenStyle, barW), chosenStyle);
                    ty += lineH;
                    foreach (string line in Wrap(LootTable.Describe(chosen), text, barW, 2))
                    {
                        Label(new Rect(x, ty, barW, lineH), line, text);
                        ty += lineH;
                    }
                    int slot = SlotOf(chosen);
                    ItemData worn = slot >= 0 ? StatMods.FindItem(save, save.equipped[slot]) : null;
                    if (ty + lineH <= textBottom)
                    {
                        if (worn == null)
                            Label(new Rect(x, ty, barW, lineH), "VS EQUIPPED:  slot empty, all of it is a gain", dim);
                        else if (worn == chosen)
                            Label(new Rect(x, ty, barW, lineH), "EQUIPPED", dim);
                        else
                        {
                            float cx = x;
                            Label(new Rect(cx, ty, barW, lineH), "VS EQUIPPED: ", dim);
                            cx += dim.CalcSize(new GUIContent("VS EQUIPPED: ")).x;
                            foreach (KeyValuePair<string, bool> part in Compare(chosen, worn))
                            {
                                GUIStyle st = PixelUI.Style(font, part.Value ? Gain : Loss);
                                float pw = st.CalcSize(new GUIContent(part.Key + "   ")).x;
                                if (cx + pw > x + barW)
                                    break;
                                Label(new Rect(cx, ty, pw, lineH), part.Key, st);
                                cx += pw;
                            }
                        }
                    }
                    if (confirmHeldSince >= 0f && !confirmConsumed)
                    {
                        float held = Mathf.Clamp01((Time.unscaledTime - confirmHeldSince) / SalvageHold);
                        PixelUI.Fill(new Rect(x, panel.yMax - 10 * u + u, barW * held, u), PixelUI.Accent);
                    }
                }
                Label(new Rect(x, panel.yMax - 8 * u, barW, lineH),
                    inHubNow ? "(A) EQUIP   (HOLD A) SALVAGE   (X) DROP   (B) BACK   < > TAB   (Y) SORT   UP: DEPTH" : "(A) EQUIP   (HOLD A) SALVAGE   (X / G) DROP   (B) BACK   < > TAB   (Y / Tab) SORT", dim);
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"inventory draw: {e.Message}");
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
        }

        // A label with a one-pixel dark shadow, so the game's thin font reads on the panel.
        // The item's picture (ItemArt), point-sampled so its pixels stay crisp; alpha < 1 draws
        // it faint (an empty slot's silhouette).
        private static void Icon(ItemData item, Rect rect, float alpha = 1f)
        {
            try
            {
                Texture2D tex = ItemArt.Texture(item);
                if (tex == null)
                    return;
                Color was = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, alpha);
                GUI.DrawTexture(rect, tex, ScaleMode.StretchToFill, true);
                GUI.color = was;
            }
            catch
            {
            }
        }

        // A one-unit frame round a rect.
        private static void Frame(Rect r, Color colour, int u)
        {
            PixelUI.Fill(new Rect(r.x, r.y, r.width, u), colour);
            PixelUI.Fill(new Rect(r.x, r.yMax - u, r.width, u), colour);
            PixelUI.Fill(new Rect(r.x, r.y, u, r.height), colour);
            PixelUI.Fill(new Rect(r.xMax - u, r.y, u, r.height), colour);
        }

        private static Color RarityColour(ItemData item)
        {
            switch (item != null ? item.rarity : null)
            {
                case "Magic": return new Color32(0x6f, 0xa8, 0xff, 0xff);
                case "Rare": return new Color32(0xff, 0xe0, 0x66, 0xff);
                case "Legendary": return new Color32(0xff, 0x8a, 0x3d, 0xff);
                default: return PixelUI.Dim;
            }
        }

        // What equipping `chosen` in place of `worn` changes: one part per affix (and the unique
        // power), "+12% dmg" / "-4% crit", true = a gain.
        private static List<KeyValuePair<string, bool>> Compare(ItemData chosen, ItemData worn)
        {
            var parts = new List<KeyValuePair<string, bool>>();
            var keys = new List<string>();
            foreach (AffixData a in chosen.affixes) if (!keys.Contains(a.key)) keys.Add(a.key);
            foreach (AffixData a in worn.affixes) if (!keys.Contains(a.key)) keys.Add(a.key);
            foreach (string key in keys)
            {
                float mine = 0f, theirs = 0f;
                foreach (AffixData a in chosen.affixes) if (a.key == key) mine += a.value;
                foreach (AffixData a in worn.affixes) if (a.key == key) theirs += a.value;
                float diff = mine - theirs;
                LootTable.AffixDef def = LootTable.Affix(key);
                bool percent = def == null || def.percent;
                // Nothing to show for a difference that rounds to nothing.
                if (percent ? Mathf.RoundToInt(diff * 100f) == 0 : Mathf.Abs(diff) < 0.005f)
                    continue;
                string value = percent ? Mathf.RoundToInt(diff * 100f) + "%" : diff.ToString("0.##");
                if (diff > 0f) value = "+" + value;
                // Lower is better for an affix that rolls negative (cooldowns, damage taken).
                bool lowerIsBetter = key == "DamageTaken" || key == "Cooldown" || (def != null && def.max < 0f);
                bool gain = lowerIsBetter ? diff < 0f : diff > 0f;
                parts.Add(new KeyValuePair<string, bool>(value + " " + Short(key), gain));
            }
            string power = Uniques.PowerText(chosen), wornPower = Uniques.PowerText(worn);
            if (power != null && wornPower == null)
                parts.Insert(0, new KeyValuePair<string, bool>("+ " + chosen.name + "'s power", true));
            else if (power == null && wornPower != null)
                parts.Add(new KeyValuePair<string, bool>("- " + worn.name + "'s power", false));
            if (parts.Count == 0)
                parts.Add(new KeyValuePair<string, bool>("no change", true));
            return parts;
        }

        // The equipped items' bonuses added up, as a few short lines.
        private static List<string> Totals(CharacterSave save)
        {
            var sums = new Dictionary<string, float>();
            var order = new List<string>();
            foreach (string id in save.equipped)
            {
                ItemData item = StatMods.FindItem(save, id);
                if (item == null)
                    continue;
                foreach (AffixData affix in item.affixes)
                {
                    if (!sums.ContainsKey(affix.key))
                    {
                        sums[affix.key] = 0f;
                        order.Add(affix.key);
                    }
                    sums[affix.key] += affix.value;
                }
            }
            var lines = new List<string>();
            string line = "";
            foreach (string key in order)
            {
                LootTable.AffixDef def = LootTable.Affix(key);
                bool percent = def == null || def.percent;
                float v = sums[key];
                string value = percent ? (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100f) + "%" : v.ToString("+0.##;-0.##");
                string part = value + " " + Short(key);
                if (line.Length > 0 && line.Length + part.Length + 2 > 22)
                {
                    lines.Add(line);
                    line = "";
                }
                line += (line.Length > 0 ? ", " : "") + part;
            }
            if (line.Length > 0)
                lines.Add(line);
            return lines;
        }

        // Short names for the totals, so they fit the column.
        private static string Short(string key)
        {
            switch (key)
            {
                case "Damage": return "dmg";
                case "CritChance": return "crit";
                case "CritDamage": return "crit dmg";
                case "Cooldown": return "cooldown";
                case "Health": return "health";
                case "MoveSpeed": return "speed";
                case "Healing": return "healing";
                case "DamageTaken": return "dmg taken";
                case "Evade": return "evade";
                default: return key.ToLowerInvariant();
            }
        }

        // Breaks text into lines no wider than the width, at spaces (a word longer than the
        // width is cut), up to maxLines; the last line is shortened with "..." if more remains.
        private static List<string> Wrap(string text, GUIStyle style, float width, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || maxLines <= 0)
                return lines;
            string[] words = text.Split(' ');
            string current = "";
            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i];
                string candidate = current.Length == 0 ? word : current + " " + word;
                if (style.CalcSize(new GUIContent(candidate)).x <= width)
                {
                    current = candidate;
                    continue;
                }
                if (current.Length > 0)
                {
                    lines.Add(current);
                    current = "";
                    if (lines.Count == maxLines)
                    {
                        // Out of lines: the last one carries the rest, shortened.
                        string rest = string.Join(" ", words, i, words.Length - i);
                        lines[lines.Count - 1] = Fit(lines[lines.Count - 1] + " " + rest, style, width);
                        return lines;
                    }
                }
                current = word;
                if (style.CalcSize(new GUIContent(current)).x > width)
                    current = Fit(current, style, width);
            }
            if (current.Length > 0)
                lines.Add(current);
            return lines;
        }

        // Shortens text to fit the width, ending in "...".
        private static string Fit(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width)
                return text;
            int lo = 0, hi = text.Length;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (style.CalcSize(new GUIContent(text.Substring(0, mid) + "...")).x <= width)
                    lo = mid;
                else
                    hi = mid - 1;
            }
            return text.Substring(0, lo).TrimEnd(' ', ',', ':') + "...";
        }

        private static void Label(Rect rect, string text, GUIStyle style)
        {
            Color color = style.normal.textColor;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1f, rect.y + 1f, rect.width, rect.height), text, style);
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        private static GUIStyle Rarity(ItemData item, int font, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            Color color = PixelUI.Text;
            if (item != null)
            {
                switch (item.rarity)
                {
                    case "Magic": color = new Color32(0x6f, 0xa8, 0xff, 0xff); break;
                    case "Rare": color = new Color32(0xff, 0xe0, 0x66, 0xff); break;
                    case "Legendary": color = new Color32(0xff, 0x8a, 0x3d, 0xff); break;
                }
            }
            return PixelUI.Style(font, color, anchor);
        }

        private static IEnumerable<string> Wrap(string text, int width)
        {
            string line = "";
            foreach (string word in text.Split(' '))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line;
                    line = word;
                }
                else
                    line = line.Length == 0 ? word : line + " " + word;
            }
            if (line.Length > 0)
                yield return line;
        }
    }
}
