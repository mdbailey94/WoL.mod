using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;

namespace WoLSlingshotDash
{
    // Reads whether the player is still holding the dash button. First the way the game's own
    // hold skills do it (the "Skill<slot>" input action), then any Rewired action with "dash" in
    // its name as a fallback. What was found is logged once; without either, the dash just
    // behaves normally.
    public static class DashButton
    {
        private static int dashActionId = -2; // -2 = not looked up yet, -1 = none found
        private static readonly Dictionary<int, bool> slotActionExists = new Dictionary<int, bool>();

        public static bool Available(Player player, int skillSlot)
        {
            if (player == null || player.inputDevice == null)
                return false;
            return SlotActionExists(skillSlot) || (DashActionId() >= 0 && player.inputDevice.rewiredPlayer != null);
        }

        public static bool Held(Player player, int skillSlot)
        {
            try
            {
                ChaosInputDevice device = player != null ? player.inputDevice : null;
                if (device == null)
                    return false;
                if (SlotActionExists(skillSlot) && device.GetButton(SlotAction(skillSlot)))
                    return true;
                int id = DashActionId();
                return id >= 0 && device.rewiredPlayer != null && device.rewiredPlayer.GetButton(id);
            }
            catch
            {
                return false;
            }
        }

        private static string SlotAction(int skillSlot) => "Skill" + skillSlot;

        private static bool SlotActionExists(int skillSlot)
        {
            bool exists;
            if (slotActionExists.TryGetValue(skillSlot, out exists) || !ReInput.isReady)
                return exists;
            exists = ReInput.mapping.GetAction(SlotAction(skillSlot)) != null;
            slotActionExists[skillSlot] = exists;
            SlingshotDashPlugin.Log($"Dash button: skill slot {skillSlot} action '{SlotAction(skillSlot)}' {(exists ? "found" : "not found")}");
            return exists;
        }

        private static int DashActionId()
        {
            if (dashActionId != -2 || !ReInput.isReady)
                return dashActionId;

            IList<InputAction> actions = ReInput.mapping.Actions;
            string all = string.Join(", ", actions.Where(a => a != null).Select(a => $"'{a.name}'#{a.id}").ToArray());
            InputAction dash = actions.FirstOrDefault(a => a != null && !string.IsNullOrEmpty(a.name)
                && a.name.IndexOf("dash", StringComparison.OrdinalIgnoreCase) >= 0);
            dashActionId = dash != null ? dash.id : -1;
            SlingshotDashPlugin.Log(dash != null
                ? $"Dash button action: '{dash.name}' (#{dash.id}). All actions: {all}"
                : $"No dash action found by name. All actions: {all}");
            return dashActionId;
        }
    }
}
