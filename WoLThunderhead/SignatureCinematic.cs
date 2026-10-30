using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The signature storm's camera, time, light and screen effects: everything the cast changes
    // that outlives a frame and has to be put back. One of these lives for one signature cast
    // (a scene object, so a level load destroys it too), runs on real time (unaffected by the
    // hit-stop and slow-motion it causes itself), and puts everything back when it goes:
    // - the stage light (GameController.stageLight, the scene's "StageLight" that vanilla dims in
    //   MuseumTutorial and FinalBossRoomEventHandler) dims as the storm gathers, flares at the
    //   impact and settles back to its own level;
    // - the camera zooms in a little for the wind-up and dive and back out after the impact. The
    //   CameraController puts the camera size back to normal at the start of every frame (its
    //   Update), so the zoom is re-applied after it each frame and simply stops when this goes;
    //   it's skipped while a cutscene has the camera (overrideCameraUpdate);
    // - a white flash over the screen (a sprite in front of the camera on the top sorting layer;
    //   the game's own overlay animations are full screen fades meant for scene changes);
    // - the impact's hit-stop (TimeScaleController.FreezeForFrames, as the normal slam uses) and a
    //   short slow-motion after it (TimeScaleController.SetTimeScaleDelayOverRealTime with ease-out,
    //   as SuperCritChance uses), which the game's time-scale controller ends by itself.
    // Every effect is tried on its own and logs the first time it fails.
    public class SignatureCinematic : MonoBehaviour
    {
        private const float DarkenTime = 0.4f;        // seconds to dim the stage after take-off
        private const float WindupDarken = 0.75f;     // the wind-up dims it this much further
        private const float WindupDarkenTime = 0.15f;
        private const float ImpactBright = 1.5f;      // the impact lights the stage up to this times its normal level...
        private const float LightReturnTime = 0.6f;   // ...and it settles back over this long
        private const float CutShortReturnTime = 0.3f; // light and zoom easing back when the cast ends early
        private const float TakeOffFlash = 0.35f;     // the take-off flash's strength (0 to 1) and fade time
        private const float TakeOffFlashFade = 0.12f;
        private const float ImpactFlashFade = 0.22f;  // seconds the impact flash fades over, after the hit-stop
        private const float ZoomInTime = 0.2f;        // seconds to zoom in at the wind-up
        private const float DiveZoomExtra = 0.04f;    // the dive pushes in this much further
        private const float DiveZoomTime = 0.08f;
        private const float ZoomOutTime = 0.5f;       // seconds to zoom back out after the impact
        private const float SlowMoScale = 0.3f;       // the slow-motion starts at this time scale and eases back to normal
        private const float FrameTime = 0.01666f;     // one frame of hit-stop, as TimeScaleController counts it
        private const float FinishTimeout = 1.5f;     // at most this long easing back after the cast before it's all put back at once
        private const float FlashCover = 1.4f;        // the flash covers this times the camera's view (shake and zoom)
        private const int FlashOrder = 32000;         // on top of everything on the top sorting layer

        private static SignatureCinematic current;
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static Sprite white;

        private Light stageLight;
        private bool lightOwned;
        private float lightOriginal, lightLastSet, lightTarget, lightRate;

        private float zoom = 1f, zoomTarget = 1f, zoomRate, zoomIn = 1f;
        private bool zoomApplied;
        private float zoomBase, zoomLastSet;

        private SpriteRenderer flash;
        private float flashStart, flashPeak, flashHold, flashFade;

        private float slowMoAt = -1f, slowMoTime;
        private bool usedTime;

        private bool finishing, restored;
        private float finishDeadline;
        private string finishHow = "scene change";

        // A fresh one for a signature cast (one at a time: an earlier one still easing back is put
        // back at once).
        public static SignatureCinematic Begin()
        {
            try
            {
                if (current != null)
                {
                    current.RestoreNow("a new cast started");
                    Destroy(current.gameObject);
                }
                current = new GameObject("Thunderhead Cinematic").AddComponent<SignatureCinematic>();
                return current;
            }
            catch (System.Exception e)
            {
                Fail("cinematic", e);
                return null;
            }
        }

        // ---- The beats (called by ThunderheadState; each returns what it did, for the log) ----

        // Leaving the ground: a quick flash, and the stage starts going dark.
        public string TakeOff(float darken)
        {
            Flash(TakeOffFlash, 0f, TakeOffFlashFade);
            try
            {
                stageLight = GameController.stageLight;
                if (stageLight == null || darken >= 1f)
                    return "flash, no stage light dimming here";
                lightOriginal = lightLastSet = stageLight.intensity;
                lightOwned = true;
                SetLight(lightOriginal * darken, DarkenTime);
                return $"flash, stage light {lightOriginal:0.00} -> {lightTarget:0.00}";
            }
            catch (System.Exception e)
            {
                Fail("stage light", e);
                return "flash";
            }
        }

        // Holding at the top before the dive: zoom in, a little darker still.
        public void Windup(float zoomTo)
        {
            zoomIn = zoomTo;
            SetZoom(zoomIn, ZoomInTime);
            if (lightOwned)
                SetLight(lightTarget * WindupDarken, WindupDarkenTime);
        }

        // Diving: the zoom pushes in a touch further (only when the zoom is on at all).
        public void Dive()
        {
            if (zoomIn < 1f)
                SetZoom(zoomIn - DiveZoomExtra, DiveZoomTime);
        }

        // The slam: hit-stop, white flash, the stage lit up and settling back, the zoom easing
        // out, then a moment of slow-motion.
        public string Impact(int hitStopFrames, float flashStrength, float slowMo)
        {
            var did = new List<string>();
            float hitStop = 0f;
            if (hitStopFrames > 0)
            {
                try
                {
                    TimeScaleController.FreezeForFrames(hitStopFrames, false);
                    usedTime = true;
                    hitStop = hitStopFrames * FrameTime;
                    did.Add($"hit-stop {hitStopFrames} frames");
                }
                catch (System.Exception e)
                {
                    Fail("hit-stop", e);
                }
            }
            if (flashStrength > 0f && Flash(flashStrength, hitStop, ImpactFlashFade))
                did.Add($"flash {flashStrength:0.00}");
            if (lightOwned)
            {
                float before = lightLastSet;
                SnapLight(lightOriginal * ImpactBright);
                SetLight(lightOriginal, LightReturnTime);
                did.Add($"stage light {before:0.00} -> {lightOriginal * ImpactBright:0.00} -> back to {lightOriginal:0.00}");
            }
            if (zoomApplied || zoom < 1f)
            {
                SetZoom(1f, ZoomOutTime);
                did.Add("zoom easing out");
            }
            if (slowMo > 0f)
            {
                slowMoAt = Time.unscaledTime + hitStop + 0.02f;
                slowMoTime = slowMo;
                did.Add($"slow-mo {slowMo:0.00}s from {SlowMoScale:0.0}x");
            }
            return did.Count > 0 ? string.Join(", ", did.ToArray()) : "no cinematic effects";
        }

        // The cast is over (landed, or cut short): ease back, then go (OnDestroy puts back anything left).
        public void Finish(string how)
        {
            if (finishing)
                return;
            finishing = true;
            finishHow = how;
            finishDeadline = Time.unscaledTime + FinishTimeout;
            if (lightOwned && !Mathf.Approximately(lightTarget, lightOriginal))
                SetLight(lightOriginal, CutShortReturnTime);
            if (!Mathf.Approximately(zoomTarget, 1f))
                SetZoom(1f, CutShortReturnTime);
        }

        // ---- Running ----

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            try
            {
                UpdateLight(dt);
                UpdateFlash(now);
                zoom = Mathf.MoveTowards(zoom, zoomTarget, zoomRate * dt);
                if (slowMoAt >= 0f && now >= slowMoAt && !TimeScaleController.paused)
                    StartSlowMo();
            }
            catch (System.Exception e)
            {
                Fail("update", e);
            }
            if (finishing && (Settled || now >= finishDeadline))
                Destroy(gameObject);
        }

        // After the CameraController's Update has set the camera for this frame.
        private void LateUpdate()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null)
                    return;
                CameraController controller = CameraController.Instance;
                if (zoom < 0.999f && controller != null && !controller.overrideCameraUpdate)
                {
                    float size = cam.orthographicSize;
                    // Normally the controller has just reset it; if it hasn't, don't zoom the zoom.
                    zoomBase = zoomApplied && Mathf.Abs(size - zoomLastSet) < 0.0001f ? zoomBase : size;
                    zoomLastSet = zoomBase * zoom;
                    cam.orthographicSize = zoomLastSet;
                    zoomApplied = true;
                }
                else if (zoomApplied)
                    PutZoomBack(cam);
                if (flash != null && flash.enabled)
                {
                    Transform view = cam.transform;
                    flash.transform.position = view.position + view.forward * (cam.nearClipPlane + 0.5f);
                    flash.transform.rotation = view.rotation;
                    float height = cam.orthographicSize * 2f * FlashCover;
                    flash.transform.localScale = new Vector3(height * cam.aspect, height, 1f);
                }
            }
            catch (System.Exception e)
            {
                Fail("camera", e);
            }
        }

        private bool Settled => (!lightOwned || Mathf.Abs(lightLastSet - lightTarget) < 0.001f)
            && zoom >= 1f && !zoomApplied && (flash == null || !flash.enabled) && slowMoAt < 0f;

        private void OnDestroy()
        {
            RestoreNow(finishing ? finishHow : "scene change");
        }

        // Everything back as it was, right now.
        private void RestoreNow(string how)
        {
            if (restored)
                return;
            restored = true;
            if (current == this)
                current = null;
            string light = "untouched", size = "untouched";
            try
            {
                if (lightOwned && stageLight != null)
                {
                    // Only if it's still ours: if the game set the light meanwhile, its value stands.
                    if (Mathf.Abs(stageLight.intensity - lightLastSet) < 0.001f)
                        stageLight.intensity = lightOriginal;
                    light = $"{stageLight.intensity:0.00}";
                }
            }
            catch (System.Exception e)
            {
                Fail("restoring the stage light", e);
            }
            lightOwned = false;
            try
            {
                Camera cam = Camera.main;
                if (zoomApplied && cam != null)
                    PutZoomBack(cam);
                if (cam != null)
                    size = $"{cam.orthographicSize:0.00}";
            }
            catch (System.Exception e)
            {
                Fail("restoring the camera", e);
            }
            zoom = zoomTarget = 1f;
            try
            {
                if (flash != null)
                    Destroy(flash.gameObject);
            }
            catch
            {
            }
            flash = null;
            slowMoAt = -1f;
            // The time-scale controller ends its own hit-stops and slow-motion; if the time scale
            // is still off with nothing running to end it (and the game isn't paused), end it.
            try
            {
                if (usedTime && !TimeScaleController.paused && !TimeScaleController.delayInProgress && Time.timeScale < 0.999f)
                {
                    ThunderheadPlugin.Log($"Thunderhead: the time scale was left at {Time.timeScale:0.00}, putting it back");
                    TimeScaleController.StopTimeScaleDelay();
                }
            }
            catch (System.Exception e)
            {
                Fail("restoring the time scale", e);
            }
            ThunderheadPlugin.Log($"Thunderhead: signature cinematic restored ({how}): stage light {light}, camera size {size}, " +
                $"time scale {Time.timeScale:0.00}{(TimeScaleController.delayInProgress ? " (easing back)" : "")}");
        }

        // ---- Light ----

        private void SetLight(float target, float seconds)
        {
            lightTarget = target;
            lightRate = Mathf.Abs(lightLastSet - target) / Mathf.Max(seconds, 0.01f);
        }

        private void SnapLight(float value)
        {
            try
            {
                if (!lightOwned || stageLight == null)
                    return;
                stageLight.intensity = value;
                lightLastSet = value;
            }
            catch (System.Exception e)
            {
                Fail("stage light", e);
            }
        }

        private void UpdateLight(float dt)
        {
            if (!lightOwned)
                return;
            if (stageLight == null)
            {
                lightOwned = false;
                return;
            }
            if (Mathf.Abs(stageLight.intensity - lightLastSet) > 0.001f)
            {
                // Something in the game set the light itself (a room event): leave it to that.
                lightOwned = false;
                ThunderheadPlugin.Log("Thunderhead: the stage light was changed by the game mid-cast, leaving it alone");
                return;
            }
            lightLastSet = Mathf.MoveTowards(lightLastSet, lightTarget, lightRate * dt);
            stageLight.intensity = lightLastSet;
        }

        // ---- Zoom ----

        private void SetZoom(float target, float seconds)
        {
            zoomTarget = Mathf.Clamp(target, 0.5f, 1f);
            zoomRate = Mathf.Abs(zoom - zoomTarget) / Mathf.Max(seconds, 0.01f);
        }

        private void PutZoomBack(Camera cam)
        {
            if (Mathf.Abs(cam.orthographicSize - zoomLastSet) < 0.0001f)
                cam.orthographicSize = zoomBase;
            zoomApplied = false;
        }

        // ---- Flash ----

        // A white flash: full strength for `hold` seconds, then fading out over `fade`.
        private bool Flash(float peak, float hold, float fade)
        {
            try
            {
                if (flash == null)
                {
                    if (white == null)
                    {
                        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
                        var pixels = new Color32[16];
                        for (int i = 0; i < pixels.Length; i++)
                            pixels[i] = new Color32(255, 255, 255, 255);
                        texture.SetPixels32(pixels);
                        texture.Apply();
                        white = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
                    }
                    var go = new GameObject("Thunderhead Flash");
                    go.transform.SetParent(transform, false);
                    flash = go.AddComponent<SpriteRenderer>();
                    flash.sprite = white;
                    SortingLayer[] layers = SortingLayer.layers;
                    if (layers != null && layers.Length > 0)
                        flash.sortingLayerID = layers[layers.Length - 1].id;
                    flash.sortingOrder = FlashOrder;
                }
                flashStart = Time.unscaledTime;
                flashPeak = Mathf.Clamp01(peak);
                flashHold = hold;
                flashFade = Mathf.Max(fade, 0.01f);
                flash.color = new Color(1f, 1f, 1f, flashPeak);
                flash.enabled = true;
                return true;
            }
            catch (System.Exception e)
            {
                Fail("screen flash", e);
                return false;
            }
        }

        private void UpdateFlash(float now)
        {
            if (flash == null || !flash.enabled)
                return;
            float fadeT = (now - flashStart - flashHold) / flashFade;
            float alpha = flashPeak * (1f - Mathf.Clamp01(fadeT));
            if (alpha <= 0.001f)
            {
                flash.enabled = false;
                return;
            }
            flash.color = new Color(1f, 1f, 1f, alpha);
        }

        // ---- Time ----

        private void StartSlowMo()
        {
            slowMoAt = -1f;
            if (TimeScaleController.delayInProgress)
            {
                ThunderheadPlugin.Log("Thunderhead: skipped the impact slow-mo (another time effect is running)");
                return;
            }
            try
            {
                TimeScaleController.SetTimeScaleDelayOverRealTime(slowMoTime, SlowMoScale, true, true);
                usedTime = true;
            }
            catch (System.Exception e)
            {
                Fail("slow-mo", e);
            }
        }

        private static void Fail(string what, System.Exception e)
        {
            if (failed.Add(what))
                ThunderheadPlugin.Log($"Thunderhead: signature {what} unavailable: {e.Message}");
        }
    }
}
