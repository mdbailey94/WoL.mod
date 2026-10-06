using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace WoLSlingshotDash
{
    // The HUD's arcana icons only show the normal dash's cooldown, so while a slingshot recharges
    // this lays a darkening (draining from the top) and a seconds countdown over its icon. It's
    // our own overlay on top of the game's icon; the game's own display is left alone. Driven
    // from the plugin's LateUpdate, looking the cooldown up by player and arcana ID.
    public static class SlingshotHud
    {
        private const float SearchInterval = 1f;

        private static CooldownUI[] huds = new CooldownUI[0];
        private static float nextSearch;
        private static bool loggedError;
        private static bool loggedShown;
        private static float unmatchedSince = -1f;
        private static bool loggedUnmatched;

        public static void Refresh()
        {
            try
            {
                if (Time.unscaledTime >= nextSearch)
                {
                    nextSearch = Time.unscaledTime + SearchInterval;
                    huds = UnityEngine.Object.FindObjectsOfType<CooldownUI>();
                }
                bool shownAny = false;
                var seen = new List<string>();
                foreach (CooldownUI hud in huds)
                {
                    if (hud == null || hud.cooldownEntries == null)
                        continue;
                    foreach (CooldownEntry entry in hud.cooldownEntries)
                    {
                        if (entry == null || entry.skillIcon == null)
                            continue;
                        Player.SkillState state = entry.skillState;
                        if (state != null)
                            seen.Add(state.skillID);
                        SlingshotOverlay overlay = entry.skillIcon.GetComponentInChildren<SlingshotOverlay>(true);
                        float remaining, total;
                        Player owner = state != null ? state.parent : hud.player;
                        if (state == null || !ChargedDashState.TryGetCooldown(owner, state.skillID, out remaining, out total))
                        {
                            if (overlay != null)
                                overlay.Hide();
                            continue;
                        }
                        if (overlay == null)
                            overlay = SlingshotOverlay.Create(entry);
                        // While the normal dash is also cooling down, the game shows its own number.
                        overlay.Show(entry.skillIcon.sprite, remaining, total, entry.remainingCD <= 0f);
                        shownAny = true;
                        if (!loggedShown)
                        {
                            loggedShown = true;
                            SlingshotDashPlugin.Log($"HUD: showing the slingshot cooldown on {state.skillID}");
                        }
                    }
                }

                // If a slingshot is recharging but no icon shows it for a while, say what the HUD has.
                if (shownAny || !ChargedDashState.AnyCooldownRunning())
                {
                    unmatchedSince = -1f;
                }
                else if (unmatchedSince < 0f)
                {
                    unmatchedSince = Time.unscaledTime;
                }
                else if (!loggedUnmatched && Time.unscaledTime - unmatchedSince > 2f)
                {
                    loggedUnmatched = true;
                    SlingshotDashPlugin.Log($"HUD: a slingshot is recharging but no icon matched. " +
                        $"{huds.Length} cooldown HUD(s), arcana on them: {string.Join(", ", seen.ToArray())}");
                }
            }
            catch (Exception e)
            {
                if (!loggedError)
                {
                    loggedError = true;
                    SlingshotDashPlugin.Log($"Slingshot HUD overlay failed: {e}");
                }
            }
        }
    }

    public class SlingshotOverlay : MonoBehaviour
    {
        private Image shade;
        private Text label;

        public static SlingshotOverlay Create(CooldownEntry entry)
        {
            var root = new GameObject("SlingshotCooldown", typeof(RectTransform));
            root.transform.SetParent(entry.skillIcon.transform, false);
            Stretch(root.GetComponent<RectTransform>());
            SlingshotOverlay overlay = root.AddComponent<SlingshotOverlay>();

            // A dark copy of the icon, filled from the bottom: it drains as the slingshot recharges.
            var shadeObject = new GameObject("Shade", typeof(RectTransform));
            shadeObject.transform.SetParent(root.transform, false);
            Stretch(shadeObject.GetComponent<RectTransform>());
            overlay.shade = shadeObject.AddComponent<Image>();
            overlay.shade.sprite = entry.skillIcon.sprite;
            overlay.shade.color = new Color(0f, 0f, 0f, 0.6f);
            overlay.shade.type = Image.Type.Filled;
            overlay.shade.fillMethod = Image.FillMethod.Vertical;
            overlay.shade.fillOrigin = (int)Image.OriginVertical.Bottom;
            overlay.shade.raycastTarget = false;

            // Seconds left, in the game's own cooldown font.
            var labelObject = new GameObject("Seconds", typeof(RectTransform));
            labelObject.transform.SetParent(root.transform, false);
            Stretch(labelObject.GetComponent<RectTransform>());
            overlay.label = labelObject.AddComponent<Text>();
            Text gameText = entry.skillCooldownText;
            overlay.label.font = gameText != null && gameText.font != null
                ? gameText.font
                : Resources.GetBuiltinResource<Font>("Arial.ttf");
            overlay.label.fontSize = gameText != null ? gameText.fontSize : 16;
            overlay.label.alignment = TextAnchor.MiddleCenter;
            overlay.label.color = new Color(0.85f, 0.95f, 1f, 1f);
            overlay.label.horizontalOverflow = HorizontalWrapMode.Overflow;
            overlay.label.verticalOverflow = VerticalWrapMode.Overflow;
            overlay.label.raycastTarget = false;
            labelObject.AddComponent<Outline>().effectColor = Color.black;
            return overlay;
        }

        public void Show(Sprite icon, float remaining, float total, bool showSeconds)
        {
            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
            if (shade.sprite != icon)
                shade.sprite = icon;
            shade.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.01f, total));
            label.enabled = showSeconds;
            label.text = Mathf.CeilToInt(remaining).ToString();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
