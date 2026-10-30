using System;
using Rewired;
using UnityEngine;

namespace WoLCloakUpgrades
{
    // The upgrade button: U on the keyboard, Y / Triangle on a controller (read straight from
    // Rewired, like the Ascension prompt; pads Rewired doesn't name use XInput's order, Y = 3).
    public static class UpgradeInput
    {
        private const int XInputY = 3;
        private static readonly string[] Names = { "y", "triangle" };

        public static bool Pressed()
        {
            if (Input.GetKeyDown(KeyCode.U))
                return true;
            if (!ReInput.isReady)
                return false;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (joystick == null || joystick.Buttons == null)
                    continue;
                bool generic = Generic(joystick);
                for (int i = 0; i < joystick.Buttons.Count; i++)
                {
                    Rewired.Controller.Button button = joystick.Buttons[i];
                    if (button == null || !button.justPressed)
                        continue;
                    if (generic ? i == XInputY
                        : !string.IsNullOrEmpty(button.name) && Array.IndexOf(Names, button.name.Trim().ToLowerInvariant()) >= 0)
                        return true;
                }
            }
            return false;
        }

        private static bool Generic(Joystick joystick)
        {
            foreach (Rewired.Controller.Button button in joystick.Buttons)
            {
                if (button != null && !string.IsNullOrEmpty(button.name)
                    && !button.name.StartsWith("Button ", StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }
    }
}
