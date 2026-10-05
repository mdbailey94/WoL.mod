using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace WoLCameraZoom
{
    [BepInPlugin(PluginGuid, PluginName, PluginVersion)]
    public class CameraZoomPlugin : BaseUnityPlugin
    {
        public const string PluginGuid = "mdbailey94.wol.camerazoom";
        public const string PluginName = "Camera Zoom";
        public const string PluginVersion = "0.1.0";

        private const float MinZoom = 0.5f;
        private const float MaxZoom = 2.5f;
        private const float ZoomStep = 0.1f;

        private ConfigEntry<bool> modEnabled;
        private ConfigEntry<float> zoom;
        private ConfigEntry<KeyboardShortcut> zoomOutKey;
        private ConfigEntry<KeyboardShortcut> zoomInKey;
        private ConfigEntry<KeyboardShortcut> resetKey;

        // The camera we last scaled, and its size before scaling, so we can put it back after rendering.
        private Camera scaledCamera;
        private float originalSize;

        private void Awake()
        {
            modEnabled = Config.Bind("General", "Enabled", true, "Turn the mod on or off (also in the title screen Mods menu).");
            zoom = Config.Bind("General", "Zoom", 1.4f,
                new ConfigDescription("Camera zoom multiplier. 1 = vanilla, higher = see more of the arena.",
                    new AcceptableValueRange<float>(MinZoom, MaxZoom)));
            zoomOutKey = Config.Bind("Hotkeys", "ZoomOut", new KeyboardShortcut(KeyCode.PageDown));
            zoomInKey = Config.Bind("Hotkeys", "ZoomIn", new KeyboardShortcut(KeyCode.PageUp));
            resetKey = Config.Bind("Hotkeys", "Reset", new KeyboardShortcut(KeyCode.Home));

            Camera.onPreCull += OnCameraPreCull;
            Camera.onPostRender += OnCameraPostRender;

            Logger.LogInfo($"{PluginName} {PluginVersion} loaded (zoom {zoom.Value:0.0}x)");
        }

        private void OnDestroy()
        {
            Camera.onPreCull -= OnCameraPreCull;
            Camera.onPostRender -= OnCameraPostRender;
        }

        private void Update()
        {
            if (!modEnabled.Value)
                return;

            if (zoomOutKey.Value.IsDown())
                SetZoom(zoom.Value + ZoomStep);
            else if (zoomInKey.Value.IsDown())
                SetZoom(zoom.Value - ZoomStep);
            else if (resetKey.Value.IsDown())
                SetZoom(1f);
        }

        private void SetZoom(float value)
        {
            zoom.Value = Mathf.Clamp(value, MinZoom, MaxZoom);
            Logger.LogInfo($"Zoom: {zoom.Value:0.0}x");
        }

        // Scale only the main gameplay camera, and only for rendering. Restoring the size in
        // OnCameraPostRender means the game's own logic (camera follow, bounds, spawning) keeps
        // seeing the vanilla value, and any script that resets the size each frame can't fight us.
        private void OnCameraPreCull(Camera cam)
        {
            if (!modEnabled.Value || cam != Camera.main || !cam.orthographic || Mathf.Approximately(zoom.Value, 1f))
                return;

            scaledCamera = cam;
            originalSize = cam.orthographicSize;
            cam.orthographicSize = originalSize * zoom.Value;
        }

        private void OnCameraPostRender(Camera cam)
        {
            if (cam != scaledCamera)
                return;

            cam.orthographicSize = originalSize;
            scaledCamera = null;
        }
    }
}
