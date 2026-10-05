using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;

namespace WoLSlingshotDash
{
    // Reads whether the player is still holding the dash button, through the game's Rewired
    // actions. The action is found by name (anything containing "dash") and logged once, so a
    // log shows what was picked; without one, the dash just behaves normally.
    public static class DashButton
    {
        private static int actionId = -2; // -2 = not looked up yet, -1 = none found

        public static bool Available(Player player) => ActionId() >= 0 && RewiredPlayer(player) != null;

        public static bool Held(Player player)
        {
            try
            {
                Rewired.Player rewired = RewiredPlayer(player);
                int id = ActionId();
                return rewired != null && id >= 0 && rewired.GetButton(id);
            }
            catch
            {
                return false;
            }
        }

        private static Rewired.Player RewiredPlayer(Player player)
        {
            ChaosInputDevice device = player != null ? player.inputDevice : null;
            return device != null ? device.rewiredPlayer : null;
        }

        private static int ActionId()
        {
            if (actionId != -2 || !ReInput.isReady)
                return actionId;

            IList<InputAction> actions = ReInput.mapping.Actions;
            string all = string.Join(", ", actions.Where(a => a != null).Select(a => $"'{a.name}'#{a.id}").ToArray());
            InputAction dash = actions.FirstOrDefault(a => a != null && !string.IsNullOrEmpty(a.name)
                && a.name.IndexOf("dash", StringComparison.OrdinalIgnoreCase) >= 0);
            actionId = dash != null ? dash.id : -1;
            SlingshotDashPlugin.Log(dash != null
                ? $"Dash button action: '{dash.name}' (#{dash.id}). All actions: {all}"
                : $"No dash action found, Slingshot will dash without charging. All actions: {all}");
            return actionId;
        }
    }
}
