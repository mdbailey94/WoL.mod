using System.Collections.Generic;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Feint Swap's signature: its camera, time, light and screen effects, and the one place that
    // puts them back. One of these lives for one signature cast (a scene object, so a level load
    // destroys it too), runs on real time (unaffected by the hit-stops it causes itself), and
    // puts everything back when it goes (the house approach of Thunderhead's SignatureCinematic):
    // - the stage light (GameController.stageLight, the scene's light the final boss room and
    //   PvP dim) dims a little, frosty, while the feints stand, flares at the last spin and
    //   settles back to its own level. It's only put back if it still has the value we set, so a
    //   room that changes its own light isn't undone;
    // - the camera pulls back a little while the fan is out, so all six feints are in view. The
    //   CameraController puts the camera size back at the start of every frame (its Update), so
    //   the pull-back is re-applied after it each frame (LateUpdate) and simply stops when this
    //   goes; it's skipped while a cutscene has the camera (overrideCameraUpdate);
    // - a frosty flash over the screen (a sprite in front of the camera on the top sorting layer);
    // - hit-stop at each spin (TimeScaleController.FreezeForFrames, as the game's own hits use),
    //   which the time-scale controller ends by itself; camera shakes (CameraController.ShakeCamera)
    //   end by themselves too.
    // It's put back when the chain's last spin has settled (Finish), when the cast is cut short
    // (all feints broken, the wizard gone or dead: Finish), on a level load (OnDestroy) and, as a
    // last resort, by its own watchdog once it has run longer than any cast can.
    public class FeintCinematic : MonoBehaviour
    {
        private const float ThrowFlash = 0.35f;       // the throw's flash strength (0 to 1)...
        private const float ThrowFlashFade = 0.18f;   // ...and fade time
        private const float StandDarken = 0.8f;       // the stage dims to this times its level while the feints stand...
        private const float StandDarkenTime = 0.3f;   // ...over this long
        private const float StandZoom = 1.1f;         // the camera pulls back to this times its size while the fan is out
        private const float StandZoomTime = 0.35f;
        private const int SpinHitStop = 2;            // frames of hit-stop at each spin
        private const float SpinShake = 0.5f;         // each spin's shake, rising by ShakeStep with each swap
        private const float ShakeStep = 0.12f;
        private const float SpinFlash = 0.12f;        // a faint flash at each spin, rising by SpinFlashStep
        private const float SpinFlashStep = 0.03f;
        private const float SpinFlashFade = 0.1f;
        private const int FinaleHitStop = 5;          // the last spin: a longer hit-stop...
        private const float FinaleShake = 1.6f;       // ...a big shake...
        private const float FinaleFlash = 0.5f;       // ...a strong flash...
        private const float FinaleFlashFade = 0.3f;
        private const float FinaleBright = 1.35f;     // ...and the stage lit up to this times its level...
        private const float LightReturnTime = 0.6f;   // ...settling back over this long
        private const float ZoomReturnTime = 0.5f;    // the pull-back easing off after the last spin
        private const float CutShortReturnTime = 0.3f; // light and zoom easing back when the cast ends early
        private const float FinishTimeout = 1.5f;     // at most this long easing back before it's all put back at once
        private const float MaxGameTime = 9f;         // game seconds (throw, 2 s stand, chain) after which the watchdog ends it
        private const float MaxRealTime = 90f;        // real seconds, the same (a long pause can only end it early, never leave it on)
        private const float FrameTime = 0.01666f;     // one frame of hit-stop, as TimeScaleController counts it
        private const float FlashCover = 1.4f;        // the flash covers this times the camera's view (shake and zoom)
        private const int FlashOrder = 32000;         // on top of everything on the top sorting layer
        private static readonly Color FrostTint = new Color(0.78f, 0.92f, 1f);

        private static FeintCinematic current;
        private static readonly HashSet<string> failed = new HashSet<string>();
        private static Sprite white;

        private Light stageLight;
        private bool lightOwned;
        private float lightOriginal, lightLastSet, lightTarget, lightRate;

        private float zoom = 1f, zoomTarget = 1f, zoomRate;
        private bool zoomApplied;
        private float zoomBase, zoomLastSet;

        private SpriteRenderer flash;
        private float flashStart, flashPeak, flashHold, flashFade;

        private bool usedTime;
        private bool finishing, restored;
        private float finishDeadline;
        private float gameDeadline, realDeadline;
        private string finishHow = "level load";

        // A fresh one for a signature cast (one at a time: an earlier one still easing back is put
        // back at once).
        public static FeintCinematic Begin()
        {
            try
            {
                if (current != null)
                {
                    current.RestoreNow("a new signature cast started");
                    Destroy(current.gameObject);
                }
                current = new GameObject("Feint Swap Cinematic").AddComponent<FeintCinematic>();
                current.gameDeadline = Time.time + MaxGameTime;
                current.realDeadline = Time.unscaledTime + MaxRealTime;
                return current;
            }
            catch (System.Exception e)
            {
                Fail("cinematic", e);
                return null;
            }
        }

        // ---- The beats (called by FeintSwapSignature; each returns what it did, for the log) ----

        // The fan thrown: a frosty flash, the stage dims a little and the camera pulls back.
        public string Throw()
        {
            Flash(ThrowFlash, 0f, ThrowFlashFade);
            SetZoom(StandZoom, StandZoomTime);
            string light = "no stage light here";
            try
            {
                stageLight = GameController.stageLight;
                if (stageLight != null)
                {
                    lightOriginal = lightLastSet = stageLight.intensity;
                    lightOwned = true;
                    SetLight(lightOriginal * StandDarken, StandDarkenTime);
                    light = $"stage light {lightOriginal:0.00} -> {lightTarget:0.00}";
                }
            }
            catch (System.Exception e)
            {
                Fail("stage light", e);
            }
            return $"frosty flash {ThrowFlash:0.00}, camera pulls back to x{StandZoom:0.00}, {light}";
        }

        // Keeps the watchdog off while the cast is still running normally.
        public void KeepAlive()
        {
            gameDeadline = Mathf.Max(gameDeadline, Time.time + 2f);
        }

        // A spin bursting: hit-stop and a shake (and a faint flash) rising with each swap; the last
        // one bigger, with the stage lit up and settling back and the camera easing in again.
        public string Burst(int swap, bool finale)
        {
            var did = new List<string>();
            int frames = finale ? FinaleHitStop : SpinHitStop;
            float hitStop = 0f;
            try
            {
                TimeScaleController.FreezeForFrames(frames, false);
                usedTime = true;
                hitStop = frames * FrameTime;
                did.Add($"hit-stop {frames} frames");
            }
            catch (System.Exception e)
            {
                Fail("hit-stop", e);
            }
            float shake = finale ? FinaleShake : SpinShake + ShakeStep * (swap - 1);
            Effects.Shake(shake);
            did.Add($"shake {shake:0.00}");
            float strength = finale ? FinaleFlash : SpinFlash + SpinFlashStep * (swap - 1);
            if (Flash(strength, hitStop, finale ? FinaleFlashFade : SpinFlashFade))
                did.Add($"flash {strength:0.00}");
            if (finale)
            {
                if (lightOwned)
                {
                    float before = lightLastSet;
                    SnapLight(lightOriginal * FinaleBright);
                    SetLight(lightOriginal, LightReturnTime);
                    did.Add($"stage light {before:0.00} -> {lightOriginal * FinaleBright:0.00} -> back to {lightOriginal:0.00}");
                }
                SetZoom(1f, ZoomReturnTime);
                did.Add("camera easing back");
            }
            return string.Join(", ", did.ToArray());
        }

        // The cast is over (the last spin, or cut short): ease back, then go (OnDestroy puts back
        // anything left).
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
            }
            catch (System.Exception e)
            {
                Fail("update", e);
            }
            if (!finishing && (Time.time > gameDeadline || now > realDeadline))
                Finish($"watchdog, the cast ran over {MaxGameTime:0} s");
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
                if (Mathf.Abs(zoom - 1f) > 0.001f && controller != null && !controller.overrideCameraUpdate)
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
            && Mathf.Abs(zoom - 1f) <= 0.001f && !zoomApplied && (flash == null || !flash.enabled);

        private void OnDestroy()
        {
            RestoreNow(finishing ? finishHow : "level load");
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
                else if (lightOwned)
                    light = "gone with the room";
            }
            catch (System.Exception e)
            {
                light = "gone with the room";
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
            // The time-scale controller ends its own hit-stops; if the time scale is still off with
            // nothing running to end it (and the game isn't paused), end it.
            string time = "";
            try
            {
                if (usedTime && !TimeScaleController.paused && !TimeScaleController.delayInProgress && Time.timeScale < 0.999f)
                {
                    time = $" (was left at {Time.timeScale:0.00}, put back)";
                    TimeScaleController.StopTimeScaleDelay();
                }
                time = $"{Time.timeScale:0.00}{time}{(TimeScaleController.delayInProgress ? " (easing back)" : "")}";
            }
            catch (System.Exception e)
            {
                Fail("restoring the time scale", e);
            }
            SlingshotDashPlugin.Log($"Feint Swap: signature cinematic restored ({how}): camera size {size}, " +
                $"stage light {light}, time scale {time}");
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
                SlingshotDashPlugin.Log("Feint Swap: the stage light was changed by the game mid-signature, leaving it alone");
                return;
            }
            lightLastSet = Mathf.MoveTowards(lightLastSet, lightTarget, lightRate * dt);
            stageLight.intensity = lightLastSet;
        }

        // ---- Zoom (above 1 pulls the camera back) ----

        private void SetZoom(float target, float seconds)
        {
            zoomTarget = Mathf.Clamp(target, 1f, 1.25f);
            zoomRate = Mathf.Abs(zoom - zoomTarget) / Mathf.Max(seconds, 0.01f);
        }

        private void PutZoomBack(Camera cam)
        {
            if (Mathf.Abs(cam.orthographicSize - zoomLastSet) < 0.0001f)
                cam.orthographicSize = zoomBase;
            zoomApplied = false;
        }

        // ---- Flash ----

        // A frosty flash: full strength for `hold` seconds, then fading out over `fade`.
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
                    var go = new GameObject("Feint Swap Flash");
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
                flash.color = new Color(FrostTint.r, FrostTint.g, FrostTint.b, flashPeak);
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
            flash.color = new Color(FrostTint.r, FrostTint.g, FrostTint.b, alpha);
        }

        private static void Fail(string what, System.Exception e)
        {
            if (failed.Add(what))
                SlingshotDashPlugin.Log($"Feint Swap: signature {what} unavailable: {e.Message}");
        }
    }
}
