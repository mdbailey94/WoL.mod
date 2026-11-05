using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLBrambleRing
{
    // The signature cast's camera, time and lighting beats, run from one hidden object of their
    // own (not the wizard, so a beat can't be cut off half-done with something left changed), and
    // every one of them puts back what it changed:
    // - Camera zoom: the game's CameraController resets the camera's size every frame in Update;
    //   this sets it again after that (LateUpdate), so a punch-in lasts only while it's driven and
    //   the size is set back to the game's own (CameraController.originalCameraSize) when it ends,
    //   is cut short (cast cancelled, wizard died, level load, a cutscene taking the camera) or runs
    //   past ZoomSafety. Skipped in PvP, where the game sizes the camera itself.
    // - Time: the game's own hit-stop and slow-down (TimeScaleController.FreezeForFrames and
    //   SetTimeScaleDelayOverRealTime), which put the time scale back themselves; a moment after
    //   they should have finished it's checked, and forced back (StopTimeScaleDelay) only if it's
    //   stuck with nothing of the game's running.
    // - The room's light (GameController.stageLight, the light the game finds in each room, which
    //   the museum and final boss rooms dim the same way): dimmed and tinted green while any
    //   signature thicket holds it, faded back to exactly what it was once the last one lets go,
    //   its coroutine stops checking in, its wizard dies, or it overstays; on a level load or if
    //   this object goes away it's put back at once. If the game changes the light itself
    //   meanwhile (a boss room event), the game's value is left alone.
    public class BrambleCinematics : MonoBehaviour
    {
        // The gloom: the room's light drops to this fraction, its colour is multiplied toward this
        // green, fading in and out over these many real seconds.
        private const float GloomIntensity = 0.55f;
        private static readonly Color GloomTint = new Color(0.6f, 1f, 0.55f, 1f);
        private const float GloomFadeIn = 0.35f;
        private const float GloomFadeOut = 0.6f;
        // A flare (the slam and the final snap): the light jumps this much brighter for a moment.
        private const float FlareBoost = 1.6f;
        private const float FlareTime = 0.08f;
        // A thicket that hasn't checked in for this long (its coroutine stopped) loses its hold.
        private const float StaleHold = 1f;
        // No zoom lasts longer than this many real seconds, whatever happens.
        private const float ZoomSafety = 3f;
        // How long after a hit-stop or slow-down should have ended the time scale is checked, and
        // how long to wait on a slow-down of the game's own (a death) before leaving it to the game.
        private const float TimeCheckMargin = 0.3f;
        private const float TimeCheckGiveUp = 3f;
        // One frame of hit-stop, as the game counts it (TimeScaleController.FreezeForFrames).
        private const float FrameTime = 0.01666f;

        private static BrambleCinematics instance;

        // Counts level loads, so a thicket can tell it's been left behind on another level.
        public static int LevelLoads { get; private set; }

        // A thicket's hold on the gloom: Beat() every frame while it stands, Release() when it's done.
        public class Hold
        {
            internal readonly Player player;
            internal readonly float deadline;
            internal float beat;
            internal bool released;

            internal Hold(Player player, float maxSeconds)
            {
                this.player = player;
                beat = Time.unscaledTime;
                deadline = Time.unscaledTime + maxSeconds;
            }

            public void Beat() => beat = Time.unscaledTime;
            public void Release() => released = true;

            // Still holding the gloom; if not, why (null when it simply ended).
            internal bool Active(out string why)
            {
                why = null;
                if (released)
                    return false;
                if (player == null || player.IsDead)
                    why = "the wizard is down";
                else if (Time.unscaledTime - beat > StaleHold)
                    why = "its thicket stopped checking in";
                else if (Time.unscaledTime > deadline)
                    why = "it overstayed";
                return why == null;
            }
        }

        // Light
        private Light light;
        private bool lightHeld;
        private float savedIntensity, lastIntensity;
        private Color savedColor, lastColor;
        private float gloom;
        private float flareUntil;
        private readonly List<Hold> holds = new List<Hold>();

        // Zoom (factors of the game's camera size; 1 is the game's own)
        private bool zoomOn;
        private Player zoomPlayer;
        private float zoomStarted, zoomFactor = 1f, zoomFrom = 1f, zoomTo = 1f, zoomT0, zoomLen;
        private float zoomHoldUntil = -1f, zoomRelease;

        // Time
        private bool timeTouched;
        private float timeCheckAt, timeGiveUpAt;

        // Set when a signature beat changes anything; cleared by the "all restored" line.
        private bool reportPending;
        private string lightReport = "untouched", cameraReport = "untouched", timeReport = "untouched";

        // The one instance, made on first use.
        public static BrambleCinematics Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("BrambleRingCinematics");
                    go.hideFlags = HideFlags.HideAndDontSave;
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<BrambleCinematics>();
                }
                return instance;
            }
        }

        private void OnEnable() => SceneManager.sceneLoaded += OnSceneLoaded;

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            RestoreAll("cinematics object disabled");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single)
                return;
            LevelLoads++;
            RestoreAll("level load");
        }

        private void LateUpdate()
        {
            // A part that fails puts back what it changed and lets go, rather than retrying.
            try
            {
                UpdateLight();
            }
            catch (System.Exception e)
            {
                BrambleRingState.LogOnce("cine:light", $"signature: gloom failed, putting the light back: {e.Message}");
                try
                {
                    PutLightBack();
                }
                catch
                {
                    lightHeld = false;
                    light = null;
                    holds.Clear();
                }
            }
            try
            {
                UpdateZoom();
            }
            catch (System.Exception e)
            {
                BrambleRingState.LogOnce("cine:zoom", $"signature: zoom failed, putting the camera back: {e.Message}");
                try
                {
                    EndZoom("it failed");
                }
                catch
                {
                    zoomOn = false;
                }
            }
            try
            {
                UpdateTime();
            }
            catch (System.Exception e)
            {
                BrambleRingState.LogOnce("cine:time", $"signature: time check failed: {e.Message}");
                timeTouched = false;
            }
            if (reportPending && !lightHeld && !zoomOn && !timeTouched)
            {
                reportPending = false;
                BrambleRingState.Log($"signature: light, camera and time all restored (light {lightReport}; camera {cameraReport}; time {timeReport})");
            }
        }

        // A beat changed something: the "all restored" line is owed, starting from a clean report.
        private void Touch()
        {
            if (reportPending)
                return;
            reportPending = true;
            lightReport = cameraReport = timeReport = "untouched";
        }

        // ---- Light ----

        // Dims the room for a thicket until its hold is released (or lapses).
        public Hold BeginGloom(Player player, float maxSeconds)
        {
            var hold = new Hold(player, maxSeconds);
            Guard("gloom", () =>
            {
                if (!lightHeld)
                {
                    Light stage = GameController.stageLight;
                    if (stage == null)
                    {
                        BrambleRingState.Log("signature: no gloom, this room has no stage light");
                        return;
                    }
                    light = stage;
                    savedIntensity = lastIntensity = stage.intensity;
                    savedColor = lastColor = stage.color;
                    gloom = 0f;
                    lightHeld = true;
                    Touch();
                    BrambleRingState.Log($"signature: gloom on, room light {savedIntensity:0.00} dimming to " +
                        $"{savedIntensity * GloomIntensity:0.00} and tinting green");
                }
                holds.Add(hold);
            });
            return hold;
        }

        // A moment's brighter light (only while the gloom holds the light).
        public void Flare()
        {
            if (lightHeld)
                flareUntil = Time.unscaledTime + FlareTime;
        }

        private void UpdateLight()
        {
            if (!lightHeld)
                return;
            if (light == null)
            {
                ReleaseLight("gloom off: the room's light is gone (level changed), nothing to put back", "gone with its room");
                return;
            }
            if (Mathf.Abs(light.intensity - lastIntensity) > 0.001f || light.color != lastColor)
            {
                ReleaseLight($"gloom off: the game changed the room's light itself (now {light.intensity:0.00}), leaving it as the game set it",
                    $"left as the game set it ({light.intensity:0.00})");
                return;
            }
            bool active = false;
            for (int i = holds.Count - 1; i >= 0; i--)
            {
                string why;
                if (holds[i].Active(out why))
                {
                    active = true;
                    continue;
                }
                if (why != null)
                    BrambleRingState.Log($"signature: gloom lifting early, {why}");
                holds.RemoveAt(i);
            }
            gloom = Mathf.MoveTowards(gloom, active ? 1f : 0f, Time.unscaledDeltaTime / (active ? GloomFadeIn : GloomFadeOut));
            bool flaring = Time.unscaledTime < flareUntil;
            if (!active && gloom <= 0f && !flaring)
            {
                PutLightBack();
                return;
            }
            float eased = gloom * gloom * (3f - 2f * gloom);
            float intensity = savedIntensity * Mathf.Lerp(1f, GloomIntensity, eased) * (flaring ? FlareBoost : 1f);
            Color color = Color.Lerp(savedColor, savedColor * GloomTint, eased);
            color.a = savedColor.a;
            light.intensity = intensity;
            light.color = color;
            // Read back what the light holds now, so a change by the game can be told from ours.
            lastIntensity = light.intensity;
            lastColor = light.color;
        }

        private void PutLightBack()
        {
            if (light != null)
            {
                light.intensity = savedIntensity;
                light.color = savedColor;
            }
            ReleaseLight($"gloom off: room light restored to {savedIntensity:0.00}", $"back to {savedIntensity:0.00}");
        }

        private void ReleaseLight(string message, string report)
        {
            BrambleRingState.Log("signature: " + message);
            lightReport = report;
            lightHeld = false;
            light = null;
            holds.Clear();
            gloom = 0f;
            flareUntil = 0f;
        }

        // ---- Camera ----

        // Zooms to `to` times the game's camera size over `over` real seconds, holds it `hold`
        // seconds and eases back over `release`; hold < 0 stays there until the next Zoom call.
        public void Zoom(Player player, float to, float over, float hold, float release)
        {
            Guard("zoom", () =>
            {
                string why;
                if (!CanZoom(out why))
                {
                    BrambleRingState.LogOnce("zoom:" + why, $"signature: no camera zoom, {why}");
                    return;
                }
                if (!zoomOn)
                {
                    zoomOn = true;
                    zoomStarted = Time.unscaledTime;
                    zoomFactor = 1f;
                    Touch();
                }
                zoomPlayer = player;
                zoomFrom = zoomFactor;
                zoomTo = to;
                zoomT0 = Time.unscaledTime;
                zoomLen = Mathf.Max(over, 0.0001f);
                zoomHoldUntil = hold >= 0f ? zoomT0 + zoomLen + hold : -1f;
                zoomRelease = release;
            });
        }

        // Eases any zoom back out (a cancelled cast).
        public void ZoomBack(string why)
        {
            if (!zoomOn)
                return;
            BrambleRingState.Log($"signature: camera easing back, {why}");
            Zoom(zoomPlayer, 1f, 0.15f, -1f, 0f);
        }

        private bool CanZoom(out string why)
        {
            CameraController controller = GameController.cameraController;
            Camera cam = Camera.main;
            why = null;
            if (controller == null || cam == null || !cam.orthographic)
                why = "no game camera";
            else if (controller.overrideCameraUpdate)
                why = "the game has the camera (cutscene)";
            else if (GameController.pvpOn)
                why = "PvP sizes the camera itself";
            return why == null;
        }

        private void UpdateZoom()
        {
            if (!zoomOn)
                return;
            string why;
            if (!CanZoom(out why))
            {
                EndZoom(why);
                return;
            }
            if (zoomPlayer != null && zoomPlayer.IsDead)
            {
                EndZoom("the wizard is down");
                return;
            }
            if (Time.unscaledTime - zoomStarted > ZoomSafety)
            {
                EndZoom("it ran too long");
                return;
            }
            float t = Mathf.Clamp01((Time.unscaledTime - zoomT0) / zoomLen);
            zoomFactor = Mathf.Lerp(zoomFrom, zoomTo, 1f - (1f - t) * (1f - t));
            if (t >= 1f && zoomHoldUntil >= 0f && Time.unscaledTime >= zoomHoldUntil)
            {
                zoomFrom = zoomFactor;
                zoomTo = 1f;
                zoomT0 = Time.unscaledTime;
                zoomLen = Mathf.Max(zoomRelease, 0.0001f);
                zoomHoldUntil = -1f;
            }
            else if (t >= 1f && Mathf.Approximately(zoomTo, 1f) && zoomHoldUntil < 0f)
            {
                EndZoom(null);
                return;
            }
            Camera.main.orthographicSize = CameraController.originalCameraSize * zoomFactor;
        }

        private void EndZoom(string why)
        {
            zoomOn = false;
            zoomFactor = 1f;
            zoomPlayer = null;
            Camera cam = Camera.main;
            if (cam != null && cam.orthographic && !GameController.pvpOn)
                cam.orthographicSize = CameraController.originalCameraSize;
            cameraReport = $"size back to {CameraController.originalCameraSize:0.00}" + (why != null ? $" early, {why}" : "");
            if (why != null)
                BrambleRingState.Log($"signature: camera zoom ended early, {why}; size back to {CameraController.originalCameraSize:0.00}");
        }

        // ---- Time ----

        // The game's hit-stop for `frames` frames, then (slowTime > 0) a slow-down from slowScale
        // easing back to full speed over slowTime real seconds.
        public void HitStop(int frames, float slowTime, float slowScale)
        {
            if (TimeScaleController.paused || TimeScaleController.instance == null)
                return;
            Guard("hit-stop", () =>
            {
                TimeScaleController.FreezeForFrames(frames, false);
                MarkTime(frames * FrameTime);
                if (slowTime > 0f)
                    StartCoroutine(SlowAfter(frames * FrameTime, slowTime, slowScale));
            });
        }

        private IEnumerator SlowAfter(float delay, float slowTime, float slowScale)
        {
            float at = Time.unscaledTime + delay;
            while (Time.unscaledTime < at)
                yield return null;
            if (TimeScaleController.paused)
                yield break;
            Guard("slow-motion", () =>
            {
                TimeScaleController.SetTimeScaleDelayOverRealTime(slowTime, slowScale, true, true);
                MarkTime(slowTime);
            });
        }

        private void MarkTime(float realSeconds)
        {
            if (!timeTouched)
                timeGiveUpAt = Time.unscaledTime + realSeconds + TimeCheckGiveUp;
            timeTouched = true;
            Touch();
            timeCheckAt = Mathf.Max(timeCheckAt, Time.unscaledTime + realSeconds + TimeCheckMargin);
        }

        private void UpdateTime()
        {
            if (!timeTouched || Time.unscaledTime < timeCheckAt || TimeScaleController.paused)
                return;
            float normal = TimeScaleController.originalTimeScale;
            if (TimeScaleController.delayInProgress)
            {
                // A slow-down still running: ours stretched, or one of the game's own (a death).
                if (Time.unscaledTime < timeGiveUpAt)
                    return;
                timeTouched = false;
                timeReport = $"left to the game's own slow-down (now {Time.timeScale:0.00})";
                BrambleRingState.Log($"signature: time left to the game's own slow-down in progress (time scale {Time.timeScale:0.00})");
                return;
            }
            timeTouched = false;
            if (Mathf.Abs(Time.timeScale - normal) > 0.001f)
            {
                TimeScaleController.StopTimeScaleDelay();
                timeReport = $"forced back to {Time.timeScale:0.00}";
                BrambleRingState.Log($"signature: time scale was left at {Time.timeScale:0.00}, forced back to {normal:0.00}");
                return;
            }
            timeReport = $"scale {Time.timeScale:0.00}";
        }

        // ---- Restore ----

        // Puts everything back at once (level load, this object going away).
        private void RestoreAll(string why)
        {
            Guard("restore", () =>
            {
                if (lightHeld)
                {
                    if (light != null && Mathf.Abs(light.intensity - lastIntensity) <= 0.001f && light.color == lastColor)
                    {
                        light.intensity = savedIntensity;
                        light.color = savedColor;
                        ReleaseLight($"gloom off ({why}): room light restored to {savedIntensity:0.00}", $"back to {savedIntensity:0.00}");
                    }
                    else
                        ReleaseLight($"gloom off ({why}), the room's light is gone or the game's own", $"released ({why})");
                }
                if (zoomOn)
                    EndZoom(why);
            });
            // Time puts itself back (the game's TimeScaleController outlives levels); still check it.
            if (timeTouched)
                timeCheckAt = Time.unscaledTime;
        }

        // Runs one part; if it throws it's logged once and the rest carry on.
        private static void Guard(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                BrambleRingState.LogOnce("cine:" + name, $"signature: {name} failed, skipping it: {e.Message}");
            }
        }
    }
}
