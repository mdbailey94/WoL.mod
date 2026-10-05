using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;

namespace WoLAscension
{
    // Right stick horizontal, read straight from Rewired. Same detection as Extended Stats'
    // scrolling: named axes first, then XInput's fixed order (axis 2 = right stick X) when the
    // pad has generic "Axis N" names.
    public static class RightStickX
    {
        private const int XInputRightStickX = 2;

        public static float Value(global::Player player)
        {
            try
            {
                ChaosInputDevice device = player.inputDevice;
                if (device == null || device.rewiredPlayer == null
                    || device.inputScheme != ChaosInputDevice.InputScheme.Gamepad)
                    return 0f;

                float strongest = 0f;
                foreach (Joystick joystick in device.rewiredPlayer.controllers.Joysticks)
                {
                    Rewired.Controller.Axis axis = joystick != null && joystick.Axes != null ? FindAxis(joystick) : null;
                    if (axis != null && Math.Abs(axis.value) > Math.Abs(strongest))
                        strongest = axis.value;
                }
                return strongest;
            }
            catch
            {
                return 0f;
            }
        }

        private static Rewired.Controller.Axis FindAxis(Joystick joystick)
        {
            IList<Rewired.Controller.Axis> axes = joystick.Axes;
            foreach (Rewired.Controller.Axis axis in axes)
            {
                if (axis == null || string.IsNullOrEmpty(axis.name))
                    continue;
                string name = axis.name.Trim().ToLowerInvariant();
                bool right = name.Contains("right") || name.StartsWith("rs");
                bool horizontal = name.EndsWith(" x") || name.Contains("horizontal");
                if (right && horizontal && !name.Contains("trigger"))
                    return axis;
            }

            bool generic = axes.All(a => a == null || string.IsNullOrEmpty(a.name)
                || a.name.StartsWith("Axis ", StringComparison.OrdinalIgnoreCase));
            bool xinput = joystick.name != null && joystick.name.IndexOf("xinput", StringComparison.OrdinalIgnoreCase) >= 0;
            return (generic || xinput) && axes.Count > XInputRightStickX ? axes[XInputRightStickX] : null;
        }
    }
}
