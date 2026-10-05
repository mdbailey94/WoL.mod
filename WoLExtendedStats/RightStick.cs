using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;

namespace WoLExtendedStats
{
    // Reads the right stick's vertical axis straight from Rewired, by element name. The game's
    // aim vector isn't usable for this: it can fall back to the left stick or the last direction.
    public static class RightStick
    {
        // Each joystick's axis list and the player's input scheme are logged once, so a log
        // shows what this controller calls its sticks if scrolling doesn't respond.
        private static readonly HashSet<string> loggedJoysticks = new HashSet<string>();
        private static readonly HashSet<string> loggedSchemes = new HashSet<string>();

        public static float Vertical(global::Player player)
        {
            try
            {
                ChaosInputDevice device = player.inputDevice;
                if (device == null || device.rewiredPlayer == null)
                    return 0f;
                if (device.inputScheme != ChaosInputDevice.InputScheme.Gamepad)
                {
                    if (loggedSchemes.Add($"{player.playerID}:{device.inputScheme}"))
                        ExtendedStatsPlugin.LogInfo($"Player {player.playerID + 1} input scheme is {device.inputScheme}; " +
                                                    "right-stick scrolling only works in gamepad mode (Page Up/Down still work)");
                    return 0f;
                }
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

                Rewired.Controller.Axis match = null;
                foreach (Rewired.Controller.Axis axis in joystick.Axes)
                {
                    if (axis != null && IsRightStickVertical(axis.name))
                    {
                        match = axis;
                        break;
                    }
                }
                LogJoystick(joystick, match);

                if (match != null && Math.Abs(match.value) > Math.Abs(strongest))
                    strongest = match.value;
            }
            return strongest;
        }

        private static void LogJoystick(Joystick joystick, Rewired.Controller.Axis match)
        {
            string key = joystick.name + "#" + joystick.id;
            if (!loggedJoysticks.Add(key))
                return;
            string axes = string.Join(", ", joystick.Axes.Where(a => a != null).Select(a => "'" + a.name + "'").ToArray());
            ExtendedStatsPlugin.LogInfo($"Controller '{joystick.name}' axes: {axes} -> scrolling with " +
                                        (match != null ? $"'{match.name}'" : "NONE (no right-stick vertical axis recognised)"));
        }

        // Rewired names recognised pads' axes like "Right Stick Y" or "Right Stick Vertical";
        // some pads use short forms like "RS Y" or "Right Y".
        private static bool IsRightStickVertical(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string lower = name.Trim().ToLowerInvariant();
            bool right = lower.Contains("right") || lower.StartsWith("rs") || lower.StartsWith("r stick");
            bool vertical = lower.EndsWith(" y") || lower.EndsWith("y-axis") || lower.EndsWith("y axis")
                || lower.Contains("vertical") || lower.EndsWith("_y");
            bool notTrigger = !lower.Contains("trigger");
            return right && vertical && notTrigger;
        }
    }
}
