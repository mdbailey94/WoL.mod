using UnityEngine;

namespace WoLTidalWall
{
    // The signature wave's camera pull-back and lighting dip, and the one place that puts them back.
    //
    // Camera: the game's camera resizes itself every frame to CameraController.originalCameraSize
    // (CameraController.Update; the game's own debug zoom keys change that same value,
    // GameController.InitDebugOptions), so the pull-back adds an offset to it and taking the offset
    // off again restores it exactly, whatever else changed meanwhile. The camera keeps following the
    // wizard the whole time (no overrides, no player-tracking changes).
    //
    // Light: the room's GameController.stageLight (what the final boss room and PvP dim). It's only
    // put back if it still has the value we set, so a room that changes its own light isn't undone.
    //
    // Time (hit-stop, slow motion) isn't handled here: the game's TimeScaleController always eases
    // time back to normal by itself.
    //
    // Restore() runs when the crash finishes easing back, when a gather is cancelled (dash, hurt,
    // death), when the wave object is destroyed (level load, anything else) and, as a last resort,
    // from the plugin's Update once the cinematic has run longer than it ever should.
    public static class Cinematic
    {
        private const float MaxLifetime = 6f;   // seconds of game time before the watchdog restores everything

        private static bool active;
        private static float zoomApplied;       // what we've added to originalCameraSize
        private static Light light;
        private static float lightOriginal;
        private static float lightSet = -1f;    // the intensity we last set, -1 when we haven't
        private static float deadline;
        private static string owner = string.Empty;
        private static bool hadLight;
        private static int token;               // which cast the current changes belong to

        public static bool Active => active;
        public static int Token => token;
        public static float BaseCameraSize => CameraController.originalCameraSize - zoomApplied;
        public static float LightOriginal => hadLight ? lightOriginal : -1f;

        // Starts a cinematic for one cast and returns its token; only that cast's Restore (or the
        // watchdog) ends it.
        public static int Begin(string who)
        {
            if (active)
                Restore("replaced by a new signature cast");
            active = true;
            owner = who;
            token++;
            zoomApplied = 0f;
            lightSet = -1f;
            light = null;
            hadLight = false;
            deadline = Time.time + MaxLifetime;
            try
            {
                light = GameController.stageLight;
                if (light != null)
                {
                    hadLight = true;
                    lightOriginal = light.intensity;
                }
            }
            catch
            {
                light = null;
            }
            return token;
        }

        // Keeps the watchdog off while the cast is still running normally.
        public static void KeepAlive(int forToken)
        {
            if (active && forToken == token)
                deadline = Mathf.Max(deadline, Time.time + 2f);
        }

        // Pulls the camera back by this fraction of its normal size (0 = normal).
        public static void SetZoom(int forToken, float fraction)
        {
            if (!active || forToken != token)
                return;
            Effects.Try("camera pull-back", () =>
            {
                float target = (CameraController.originalCameraSize - zoomApplied) * fraction;
                CameraController.originalCameraSize += target - zoomApplied;
                zoomApplied = target;
            });
        }

        // Sets the room light to this multiple of its normal brightness (1 = normal).
        public static void SetLight(int forToken, float multiple)
        {
            if (!active || forToken != token || light == null)
                return;
            Effects.Try("stage light", () =>
            {
                lightSet = lightOriginal * multiple;
                light.intensity = lightSet;
            });
        }

        // Puts the camera and light back. With a token, only if that cast's cinematic is the one
        // running (so a lingering old wave can't end a newer cast's).
        public static void Restore(string why, int forToken = 0)
        {
            if (!active || (forToken != 0 && forToken != token))
                return;
            active = false;
            string camera = "camera untouched";
            string lighting = "no stage light here";
            try
            {
                if (zoomApplied != 0f)
                {
                    CameraController.originalCameraSize -= zoomApplied;
                    camera = $"camera size back to {CameraController.originalCameraSize:0.00}";
                }
            }
            catch (System.Exception e)
            {
                camera = $"camera restore failed: {e.Message}";
            }
            zoomApplied = 0f;
            try
            {
                if (light != null && lightSet >= 0f)
                {
                    if (Mathf.Abs(light.intensity - lightSet) < 0.001f)
                    {
                        light.intensity = lightOriginal;
                        lighting = $"light back to {lightOriginal:0.00}";
                    }
                    else
                        lighting = $"light left at {light.intensity:0.00} (the room changed it)";
                }
                else if (light != null)
                    lighting = "light untouched";
                else if (hadLight)
                    lighting = "light gone with the room";
            }
            catch
            {
                // The light went with the room (a level load): nothing to put back.
                lighting = "light gone with the room";
            }
            light = null;
            lightSet = -1f;
            TidalWallPlugin.Log($"Tidal Wall: signature restored ({why}): {camera}, {lighting}");
        }

        // Called every frame by the plugin: never leave the camera or light changed for long.
        public static void Watchdog()
        {
            if (active && Time.time > deadline)
                Restore($"watchdog, {owner} ran over {MaxLifetime:0} s");
        }
    }
}
