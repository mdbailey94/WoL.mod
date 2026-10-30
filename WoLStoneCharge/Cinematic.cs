using UnityEngine;

namespace WoLStoneCharge
{
    // The signature charge's camera pull-back, darkened room and earthquake flash,
    // and the one place that puts them back (Cyclone's, after Tidal Wall's; Stone Charge 0.5.0).
    //
    // Camera: the game resizes its camera every frame to CameraController.originalCameraSize, so
    // the pull-back is an offset added to it and taken off again exactly.
    // Light: the room's GameController.stageLight, put back only if it still has the value we
    // set (a room that changes its own light isn't undone).
    // Time (hit-stop, slow motion) is the game's TimeScaleController, which eases back by itself.
    //
    // The state sets the zoom and light while you charge; flashes and the ease back after
    // the cast run here, on real time, from the plugin's Update (Tick), which also restores
    // everything if a cinematic ever runs longer than it should.
    public static class Cinematic
    {
        private const float MaxLifetime = 9f;   // real seconds before the watchdog restores everything

        private static bool active;
        private static int token;
        private static float zoomApplied;       // added to originalCameraSize
        private static Light light;
        private static float lightOriginal, lightSet = -1f;
        private static float zoom, lightMul = 1f;          // what the state asked for
        private static float flashUntil, flashMul = 1f;    // a flash on top, for a moment
        private static float easeStart = -1f, easeTime, easeZoom, easeLight;
        private static float deadline;

        public static int Begin()
        {
            if (active)
                Restore("replaced by a new signature cast");
            active = true;
            token++;
            zoomApplied = 0f;
            zoom = 0f;
            lightMul = 1f;
            flashUntil = 0f;
            easeStart = -1f;
            lightSet = -1f;
            light = null;
            try
            {
                light = GameController.stageLight;
                if (light != null)
                    lightOriginal = light.intensity;
            }
            catch
            {
                light = null;
            }
            deadline = Time.unscaledTime + MaxLifetime;
            return token;
        }

        // The pull-back (fraction of the normal camera size) and the room's brightness (1 = normal).
        public static void Set(int forToken, float zoomFraction, float lightMultiple)
        {
            if (!active || forToken != token || easeStart >= 0f)
                return;
            zoom = zoomFraction;
            lightMul = lightMultiple;
            deadline = Mathf.Max(deadline, Time.unscaledTime + 2f);
            Apply(zoom, lightMul);
        }

        // The room flashes to this multiple of its normal light for a moment (lightning, the burst).
        public static void Flash(int forToken, float multiple, float realSeconds)
        {
            if (!active || forToken != token)
                return;
            flashMul = multiple;
            flashUntil = Time.unscaledTime + realSeconds;
            Apply(zoom, multiple);
        }

        // Eases the camera and light back to normal over this long (real time), then restores.
        public static void EaseOut(int forToken, float realSeconds, string why)
        {
            if (!active || forToken != token || easeStart >= 0f)
                return;
            easeStart = Time.unscaledTime;
            easeTime = Mathf.Max(0.05f, realSeconds);
            easeZoom = zoom;
            easeLight = lightMul;
            easeWhy = why;
            deadline = Time.unscaledTime + realSeconds + 2f;
        }
        private static string easeWhy = "";

        public static void Tick()
        {
            if (!active)
                return;
            float now = Time.unscaledTime;
            if (now > deadline)
            {
                Restore($"watchdog, ran over {MaxLifetime:0} s");
                return;
            }
            bool flashing = now < flashUntil;
            if (easeStart >= 0f)
            {
                float t = Mathf.Clamp01((now - easeStart) / easeTime);
                float eased = t * t * (3f - 2f * t);
                zoom = Mathf.Lerp(easeZoom, 0f, eased);
                lightMul = Mathf.Lerp(easeLight, 1f, eased);
                Apply(zoom, flashing ? flashMul : lightMul);
                if (t >= 1f && !flashing)
                    Restore(easeWhy);
                return;
            }
            if (flashUntil > 0f && !flashing)
            {
                flashUntil = 0f;
                Apply(zoom, lightMul);
            }
        }

        private static void Apply(float zoomFraction, float lightMultiple)
        {
            try
            {
                float target = (CameraController.originalCameraSize - zoomApplied) * zoomFraction;
                CameraController.originalCameraSize += target - zoomApplied;
                zoomApplied = target;
            }
            catch
            {
            }
            try
            {
                if (light != null)
                {
                    lightSet = lightOriginal * lightMultiple;
                    light.intensity = lightSet;
                }
            }
            catch
            {
                light = null;
            }
        }

        // Puts the camera and light back. With a token, only if that cast's cinematic is running.
        public static void Restore(string why, int forToken = 0)
        {
            if (!active || (forToken != 0 && forToken != token))
                return;
            active = false;
            string camera = "camera untouched", lighting = "no stage light here";
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
            }
            catch
            {
                lighting = "light gone with the room";
            }
            light = null;
            lightSet = -1f;
            StoneChargePlugin.Log($"Stone Charge: signature restored ({why}): {camera}, {lighting}");
        }
    }
}
