using System.Collections.Generic;
using UnityEngine;

namespace WoLCyclone
{
    // What Cyclone looks like, all from the game's own wind effects:
    // - the Twister arcana's little tornado, pinned in place and made harmless, growing with the
    //   charge (its particles scaled up as well as its size);
    // - the air boss's storm vortex (dust, debris and light streaks), fading in around it as it
    //   grows, for the hurricane at full power (its pull and attack switched off);
    // - the air vortex swirls, which also stand in if the others can't be made.
    public class CycloneLook
    {
        private const int HarmlessLevel = 6;
        private const float TwisterStart = 0.6f;   // twister size, small to big
        private const float TwisterEnd = 2.2f;
        private const float StormFrom = 0.35f;     // progress at which the storm starts showing
        private const float StormStart = 0.5f;     // storm size as it appears and at full power
        private const float StormEnd = 1.3f;

        private readonly Player player;
        private readonly string skillCategory;
        private readonly string skillID;
        private Twister twister;
        private AirBossVortex storm;
        private readonly Dictionary<ParticleSystem, float> baseSizes = new Dictionary<ParticleSystem, float>();
        private readonly Dictionary<ParticleSystem, float> baseRates = new Dictionary<ParticleSystem, float>();
        private static bool loggedTwister, loggedStorm;

        public CycloneLook(Player player, string skillCategory, string skillID, Vector2 position)
        {
            this.player = player;
            this.skillCategory = skillCategory;
            this.skillID = skillID;
            SpawnTwister(position);
        }

        public void Update(Vector2 position, float progress)
        {
            UpdateTwister(position, progress);
            if (storm == null && progress >= StormFrom)
                SpawnStorm(position);
            UpdateStorm(position, progress);
        }

        public void Remove()
        {
            if (twister != null)
            {
                try
                {
                    twister.SetAudioStatus(false);
                }
                catch
                {
                }
                Object.Destroy(twister.gameObject);
                twister = null;
            }
            if (storm != null)
            {
                Object.Destroy(storm.gameObject);
                storm = null;
            }
        }

        // ---- The twister ----

        private void SpawnTwister(Vector2 position)
        {
            try
            {
                GameObject go = Object.Instantiate(Twister.Prefab, position, Quaternion.identity);
                twister = go != null ? go.GetComponent<Twister>() : null;
                if (twister == null)
                {
                    if (go != null)
                        Object.Destroy(go);
                    Log(ref loggedTwister, "Cyclone: the game's twister is unavailable");
                    return;
                }
                // Yours, harmless, going nowhere and never running out. Each step on its own, so one
                // the game objects to doesn't lose the rest.
                Step("own", () =>
                {
                    twister.parentEntity = player;
                    twister.parentObject = player.gameObject;
                });
                Step("disarm", () =>
                {
                    if (twister.attackBox != null)
                    {
                        twister.attackBox.enabled = false;
                        if (twister.attackBox.collider != null)
                            twister.attackBox.collider.enabled = false;
                    }
                    if (twister.atkCollider != null)
                        twister.atkCollider.enabled = false;
                });
                Step("attack info", () => twister.attackBox?.SetAttackInfo(skillCategory, skillID, HarmlessLevel, false));
                twister.moveSpeed = 0f;
                twister.baseMoveSpeed = 0f;
                twister.finalMoveSpeed = 0f;
                twister.moveVector = Vector2.zero;
                twister.lifeTime = 9999f;
                twister.goThroughWalls = true;
                twister.ignoreReflect = true;
                twister.ignoreNegate = true;
                twister.transform.localScale = Vector3.one * TwisterStart;
                Remember(twister.startPartSys);
                Remember(twister.frontPartSys);
                Remember(twister.backPartSys);
                Step("sound", () => twister.SetAudioStatus(true));
                Log(ref loggedTwister, "Cyclone: using the game's twister");
            }
            catch (System.Exception e)
            {
                Log(ref loggedTwister, $"Cyclone: the game's twister is unavailable: {e.Message}");
                twister = null;
            }
        }

        private void UpdateTwister(Vector2 position, float progress)
        {
            if (twister == null)
                return;
            try
            {
                if (!twister.gameObject.activeInHierarchy)
                {
                    // Something reset it (e.g. a wall): make a fresh one.
                    Object.Destroy(twister.gameObject);
                    twister = null;
                    SpawnTwister(position);
                    if (twister == null)
                        return;
                }
                float size = Mathf.Lerp(TwisterStart, TwisterEnd, progress);
                twister.transform.position = position;
                twister.transform.localScale = Vector3.one * size;
                twister.moveVector = Vector2.zero;
                twister.moveSpeed = 0f;
                if (twister.rigidbody2D != null)
                {
                    twister.rigidbody2D.position = position;
                    twister.rigidbody2D.velocity = Vector2.zero;
                }
                twister.RefreshFlightTime(false);
                Scale(twister.startPartSys, size, 1f + progress);
                Scale(twister.frontPartSys, size, 1f + progress);
                Scale(twister.backPartSys, size, 1f + progress);
            }
            catch
            {
            }
        }

        // ---- The storm ----

        private void SpawnStorm(Vector2 position)
        {
            try
            {
                GameObject go = Object.Instantiate(AirBossVortex.Prefab, position, Quaternion.identity);
                storm = go != null ? go.GetComponent<AirBossVortex>() : null;
                if (storm == null)
                {
                    if (go != null)
                        Object.Destroy(go);
                    Log(ref loggedStorm, "Cyclone: the game's storm vortex is unavailable");
                    StormFailed();
                    return;
                }
                // Only the look: no pull on anyone (you included), no attack, not following the boss.
                // (Fields only, nothing that can object.)
                storm.useGravityEffector = false;
                if (storm.gravityEffector != null)
                    storm.gravityEffector.enabled = false;
                if (storm.gravityCollider != null)
                    storm.gravityCollider.enabled = false;
                storm.useAttackBox = false;
                if (storm.attackObj != null)
                    storm.attackObj.SetActive(false);
                if (storm.attack != null)
                    storm.attack.enabled = false;
                if (storm.attackCollider != null)
                    storm.attackCollider.enabled = false;
                if (storm.followTrans != null)
                    storm.followTrans.enabled = false;
                storm.skillCategory = skillCategory;
                storm.skillID = skillID;
                storm.skillLevel = HarmlessLevel;
                storm.useParticleEffects = true;
                storm.transform.localScale = Vector3.one * StormStart;
                Remember(storm.dustEmitter);
                Remember(storm.debrisEmitter);
                Remember(storm.lightStreakEmitter);
                Log(ref loggedStorm, "Cyclone: using the game's storm vortex");
            }
            catch (System.Exception e)
            {
                Log(ref loggedStorm, $"Cyclone: the game's storm vortex is unavailable: {e.Message}");
                if (storm != null)
                    Object.Destroy(storm.gameObject);
                StormFailed();
            }
        }

        private bool stormFailed;

        private void StormFailed()
        {
            storm = null;
            stormFailed = true;
        }

        private void UpdateStorm(Vector2 position, float progress)
        {
            if (storm == null || stormFailed)
                return;
            try
            {
                // Fades in from StormFrom to full, growing with it.
                float t = Mathf.Clamp01((progress - StormFrom) / (1f - StormFrom));
                float size = Mathf.Lerp(StormStart, StormEnd, t);
                storm.transform.position = position;
                storm.transform.localScale = Vector3.one * size;
                if (storm.gravityEffector != null && storm.gravityEffector.enabled)
                    storm.gravityEffector.enabled = false;
                Scale(storm.dustEmitter, size, 0.2f + 0.8f * t);
                Scale(storm.debrisEmitter, size, 0.2f + 0.8f * t);
                Scale(storm.lightStreakEmitter, size, 0.1f + 0.9f * t);
            }
            catch
            {
            }
        }

        // ---- Particles ----

        private void Remember(ParticleSystem system)
        {
            if (system == null || baseSizes.ContainsKey(system))
                return;
            ParticleSystem.MainModule main = system.main;
            baseSizes[system] = main.startSizeMultiplier;
            baseRates[system] = system.emission.rateOverTimeMultiplier;
        }

        // Bigger particles with the size (scaling the object alone may not reach them), and
        // a denser emission as it builds.
        private void Scale(ParticleSystem system, float size, float density)
        {
            float baseSize, baseRate;
            if (system == null || !baseSizes.TryGetValue(system, out baseSize) || !baseRates.TryGetValue(system, out baseRate))
                return;
            ParticleSystem.MainModule main = system.main;
            // Particles that already scale with the object don't need it twice.
            main.startSizeMultiplier = main.scalingMode == ParticleSystemScalingMode.Hierarchy ? baseSize : baseSize * size;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTimeMultiplier = baseRate * density;
        }

        private static readonly HashSet<string> failedSteps = new HashSet<string>();

        private static void Step(string name, System.Action action)
        {
            try
            {
                action();
            }
            catch (System.Exception e)
            {
                if (failedSteps.Add(name))
                    CyclonePlugin.Log($"Cyclone: twister step '{name}' failed: {e.Message}");
            }
        }

        private static void Log(ref bool logged, string message)
        {
            if (logged)
                return;
            logged = true;
            CyclonePlugin.Log(message);
        }
    }
}
