using System;
using System.Collections.Generic;
using Rewired;

namespace WoLExtendedStats
{
    // Reads the right stick's vertical axis straight from Rewired, by element name. The game's
    // aim vector isn't usable for this: it can fall back to the left stick or the last direction.
    public static class RightStick
    {
        public static float Vertical(global::Player player)
        {
            try
            {
                ChaosInputDevice device = player.inputDevice;
                if (device == null || device.inputScheme != ChaosInputDevice.InputScheme.Gamepad
                    || device.rewiredPlayer == null)
                    return 0f;
                return Vertical(device.rewiredPlayer.controllers.Joysticks);
            }
            catch
            {
                return 0f;
            }
        }

        public static float Vertical(IList<Joystick> joysticks)
        {
            if (joysticks == null)
                return 0f;

            float strongest = 0f;
            foreach (Joystick joystick in joysticks)
            {
                if (joystick == null || joystick.Axes == null)
                    continue;
                foreach (Rewired.Controller.Axis axis in joystick.Axes)
                {
                    if (axis != null && IsRightStickVertical(axis.name) && Math.Abs(axis.value) > Math.Abs(strongest))
                        strongest = axis.value;
                }
            }
            return strongest;
        }

        // Rewired names recognised pads' axes like "Right Stick Y" (Xbox, PlayStation, Switch Pro).
        private static bool IsRightStickVertical(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("right stick") && (lower.EndsWith(" y") || lower.Contains("vertical"));
        }
    }
}
