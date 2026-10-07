using System;
using UnityEngine;

namespace WoLAscension
{
    // Shows a message in the game's own notice banner (the one that announces a new arcana or
    // relic): queued like the game's own notices, with our header, line of text and icon.
    public static class GameBanner
    {
        private static bool loggedError;

        public static bool Show(string header, string info, Sprite icon)
        {
            try
            {
                UnlockNotifier notifier = UnlockNotifier.Instance;
                if (notifier == null || notifier.noticeQueue == null)
                    return false;
                // Only the latest of ours waits its turn (pressing the altar quickly shouldn't queue
                // up a string of banners).
                var keep = new System.Collections.Generic.List<UnlockNotifier.NoticeVars>();
                foreach (UnlockNotifier.NoticeVars vars in notifier.noticeQueue)
                {
                    if (vars == null || vars.noticeID == null || !vars.noticeID.StartsWith("Ascension_"))
                        keep.Add(vars);
                }
                notifier.noticeQueue.Clear();
                foreach (UnlockNotifier.NoticeVars vars in keep)
                    notifier.noticeQueue.Enqueue(vars);
                notifier.noticeQueue.Enqueue(new UnlockNotifier.NoticeVars("Ascension_" + header, header, info, icon,
                    UnlockNotifier.NoticeType.Relic));
                return true;
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    AscensionPlugin.Log($"Couldn't show the game's banner: {e.Message}");
                }
                return false;
            }
        }
    }
}
