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
        // XInput pads (Xbox and most PC controllers) that Rewired doesn't recognise get generic
        // names ("Axis 0".."Axis 5") but always the same order: left X/Y, right X/Y, triggers.
        private const int XInputRightStickY = 3;

        // Set from config: a specific axis index to use instead of auto-detecting (-1 = auto).
        public static int OverrideIndex = -1;

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

                Rewired.Controller.Axis match = FindAxis(joystick);
                LogJoystick(joystick, match);

                if (match != null && Math.Abs(match.value) > Math.Abs(strongest))
                    strongest = match.value;
            }
            return strongest;
        }

        private static Rewired.Controller.Axis FindAxis(Joystick joystick)
        {
            IList<Rewired.Controller.Axis> axes = joystick.Axes;
            if (OverrideIndex >= 0)
                return OverrideIndex < axes.Count ? axes[OverrideIndex] : null;

            foreach (Rewired.Controller.Axis axis in axes)
            {
                if (axis != null && IsRightStickVertical(axis.name))
                    return axis;
            }

            bool genericNames = axes.All(a => a == null || string.IsNullOrEmpty(a.name)
                || a.name.StartsWith("Axis ", StringComparison.OrdinalIgnoreCase));
            bool xinput = joystick.name != null && joystick.name.IndexOf("xinput", StringComparison.OrdinalIgnoreCase) >= 0;
            if ((xinput || genericNames) && axes.Count > XInputRightStickY)
                return axes[XInputRightStickY];
            return null;
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
