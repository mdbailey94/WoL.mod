using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The wizard crackling with electricity while up in the air:
    // - the game's Electric Aura effect (from the relic), stripped down to just its looks (no
    //   attack, no scripts) and carried along on the wizard's lifted picture;
    // - sparks jumping round them;
    // - their picture flickering between a pale electric blue and a lightning yellow.
    // Everything goes back to normal when they land.
    public class ElectricLook
    {
        private static readonly Color Blue = new Color(0.7f, 0.88f, 1f, 1f);
        private static readonly Color Yellow = new Color(1f, 1f, 0.72f, 1f);
        private const float BodyHeight = 0.55f; // the middle of the wizard's picture, above their feet

        private readonly Player player;
        private readonly SpriteRenderer body;
        private readonly Color original;
        private GameObject aura;
        private readonly List<Renderer> auraRenderers = new List<Renderer>();
        private float nextFlicker;
        private float nextSpark;
        private bool flick;
        private static bool loggedAura;

        private ElectricLook(Player player)
        {
            this.player = player;
            body = player.spriteRenderer;
            original = body != null ? body.color : Color.white;
            MakeAura();
        }

        public static ElectricLook On(Player player)
        {
            try
            {
                return player != null && ThunderheadPlugin.ElectricLook ? new ElectricLook(player) : null;
            }
            catch (System.Exception e)
            {
                ThunderheadPlugin.Log($"Thunderhead: electric look unavailable: {e.Message}");
                return null;
            }
        }

        // Every frame in the air; lift is 0 on the ground to 1 at the top.
        public void Update(float lift)
        {
            if (body == null)
                return;
            float now = Time.time;
            if (now >= nextFlicker)
            {
                nextFlicker = now + Random.Range(0.04f, 0.09f);
                flick = !flick;
                body.color = Color.Lerp(original, flick ? Blue : Yellow, 0.35f + 0.65f * lift);
            }
            Vector2 center = (Vector2)body.transform.position + new Vector2(0f, BodyHeight);
            if (now >= nextSpark)
            {
                nextSpark = now + 0.05f;
                Vector2 spot = center + Random.insideUnitCircle * 0.6f;
                try
                {
                    PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(Random.value < 0.2f ? HitSparkType.Medium : HitSparkType.Small,
                        spot, spot, null, null, 0f);
                }
                catch
                {
                }
            }
            // Keep the aura drawn just in front of the wizard.
            foreach (Renderer renderer in auraRenderers)
            {
                if (renderer == null)
                    continue;
                renderer.sortingLayerID = body.sortingLayerID;
                renderer.sortingOrder = body.sortingOrder + 1;
            }
        }

        public void Off()
        {
            if (body != null)
                body.color = original;
            if (aura != null)
                Object.Destroy(aura);
            aura = null;
            auraRenderers.Clear();
        }

        private void Strip<T>() where T : Component
        {
            foreach (T component in aura.GetComponentsInChildren<T>(true))
            {
                try
                {
                    Object.DestroyImmediate(component);
                }
                catch
                {
                }
            }
        }

        private void MakeAura()
        {
            try
            {
                GameObject prefab = ElectricAura.Prefab;
                if (prefab == null || body == null)
                    throw new System.Exception("no prefab");
                // Copied switched off, so none of its scripts (its attack among them) ever run,
                // stripped, then switched on.
                bool wasActive = prefab.activeSelf;
                prefab.SetActive(false);
                try
                {
                    aura = Object.Instantiate(prefab, body.transform);
                }
                finally
                {
                    prefab.SetActive(wasActive);
                }
                aura.transform.localPosition = new Vector3(0f, BodyHeight, 0f);
                aura.transform.localScale = Vector3.one * ThunderheadPlugin.AuraSize;
                Strip<MonoBehaviour>();
                Strip<Collider2D>();
                Strip<Rigidbody2D>();
                Strip<AudioSource>();
                aura.SetActive(true);
                foreach (ParticleSystem system in aura.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = system.main;
                    main.loop = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    main.simulationSpace = ParticleSystemSimulationSpace.Local;
                    // Some emitters are switched on by the scripts removed above: a steady stream.
                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = true;
                    if (emission.rateOverTimeMultiplier < 0.01f)
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(25f);
                    if (!system.gameObject.activeSelf)
                        system.gameObject.SetActive(true);
                    system.Play();
                }
                foreach (Animator anim in aura.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
                auraRenderers.AddRange(aura.GetComponentsInChildren<Renderer>(true));
                if (!loggedAura)
                {
                    loggedAura = true;
                    ThunderheadPlugin.Log($"Thunderhead: using the game's electric aura ({auraRenderers.Count} part(s))");
                }
            }
            catch (System.Exception e)
            {
                if (aura != null)
                    Object.Destroy(aura);
                aura = null;
                if (!loggedAura)
                {
                    loggedAura = true;
                    ThunderheadPlugin.Log($"Thunderhead: the game's electric aura is unavailable, sparks only: {e.Message}");
                }
            }
        }
    }
}
