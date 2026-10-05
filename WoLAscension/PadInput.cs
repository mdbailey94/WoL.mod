using System;
using System.Collections.Generic;
using System.Linq;
using Rewired;
using UnityEngine;

namespace WoLAscension
{
    // Reads any connected controller directly through Rewired, so the prompt works while the
    // game's own input is locked. Handles pads Rewired recognises (named elements) and XInput pads
    // it doesn't (generic "Axis N"/"Button N" names in XInput's fixed order).
    public static class PadInput
    {
        private const int XInputLeftStickX = 0;
        private const int XInputRightStickX = 2;
        private const int XInputA = 0;
        private const float StickThreshold = 0.6f;
        private const float RepeatSeconds = 0.25f;

        private static readonly string[] ConfirmNames = { "a", "cross" };
        private static float nextRepeat;

        // -1 / +1 when any stick, the d-pad or arrow keys go left/right (edge + hold repeat), else 0.
        public static int Horizontal()
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftBracket))
                return -1;
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightBracket))
                return 1;
            if (GameDirection(InputDirection.Left))
                return -1;
            if (GameDirection(InputDirection.Right))
                return 1;

            float stick = StrongestStickX();
            if (Mathf.Abs(stick) < StickThreshold)
            {
                nextRepeat = 0f;
                return 0;
            }
            if (Time.unscaledTime < nextRepeat)
                return 0;
            nextRepeat = Time.unscaledTime + RepeatSeconds;
            return stick > 0f ? 1 : -1;
        }

        public static bool Confirm()
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
                return true;
            if (!ReInput.isReady)
                return false;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (joystick == null || joystick.Buttons == null)
                    continue;
                bool generic = GenericButtons(joystick);
                for (int i = 0; i < joystick.Buttons.Count; i++)
                {
                    Rewired.Controller.Button button = joystick.Buttons[i];
                    if (button == null || !button.justPressed)
                        continue;
                    if (generic ? i == XInputA
                        : !string.IsNullOrEmpty(button.name) && Array.IndexOf(ConfirmNames, button.name.Trim().ToLowerInvariant()) >= 0)
                        return true;
                }
            }
            return false;
        }

        private static bool GameDirection(InputDirection direction)
        {
            try
            {
                return InputController.GetAnyDeviceDirection(direction, true);
            }
            catch
            {
                return false;
            }
        }

        private static float StrongestStickX()
        {
            if (!ReInput.isReady)
                return 0f;
            float strongest = 0f;
            foreach (Joystick joystick in ReInput.controllers.Joysticks)
            {
                if (joystick == null || joystick.Axes == null)
                    continue;
                foreach (Rewired.Controller.Axis axis in HorizontalAxes(joystick))
                {
                    if (Math.Abs(axis.value) > Math.Abs(strongest))
                        strongest = axis.value;
                }
            }
            return strongest;
        }

        private static IEnumerable<Rewired.Controller.Axis> HorizontalAxes(Joystick joystick)
        {
            IList<Rewired.Controller.Axis> axes = joystick.Axes;
            bool generic = axes.All(a => a == null || string.IsNullOrEmpty(a.name)
                || a.name.StartsWith("Axis ", StringComparison.OrdinalIgnoreCase));
            if (generic)
            {
                if (axes.Count > XInputLeftStickX && axes[XInputLeftStickX] != null)
                    yield return axes[XInputLeftStickX];
                if (axes.Count > XInputRightStickX && axes[XInputRightStickX] != null)
                    yield return axes[XInputRightStickX];
                yield break;
            }

            foreach (Rewired.Controller.Axis axis in axes)
            {
                if (axis == null || string.IsNullOrEmpty(axis.name))
                    continue;
                string name = axis.name.Trim().ToLowerInvariant();
                bool stick = name.Contains("stick") || name.StartsWith("ls") || name.StartsWith("rs");
                bool horizontal = name.EndsWith(" x") || name.Contains("horizontal");
                if (stick && horizontal)
                    yield return axis;
            }
        }

        private static bool GenericButtons(Joystick joystick)
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
