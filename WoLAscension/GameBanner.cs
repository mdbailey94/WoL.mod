using System;
using UnityEngine;

namespace WoLAscension
{
    // Shows a message with the game's own on-screen notice (GameUI's broadcast notice line, the
    // one the game uses for its own short announcements). Queuing into the arcana/relic unlock
    // banner didn't show anything, so this is used instead. The icon is kept for callers but the
    // notice line is text only.
    public static class GameBanner
    {
        private const float Seconds = 3.5f;
        private static bool loggedError;

        public static bool Show(string header, string info, Sprite icon)
        {
            try
            {
                string message = string.IsNullOrEmpty(info) ? header : header + " - " + info;
                GameUI.BroadcastNoticeMessage(message, Seconds);
                return true;
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    AscensionPlugin.Log($"Couldn't show the game's notice: {e.Message}");
                }
                return false;
            }
        }
    }
}
