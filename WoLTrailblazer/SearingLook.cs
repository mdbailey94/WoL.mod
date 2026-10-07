using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The fire Searing Rush leaves behind (the game's FireWall): one of its fire columns is copied
    // for each patch of the trail, stripped down to just its looks (no scripts, so no attack or
    // timing of its own; the trail's hits are separate), sized by the patch's level, and put out
    // when the patch burns out. If the game's fire wall can't be had, the trail uses plain
    // flame particles instead.
    public static class SearingLook
    {
        private static GameObject source;
        private static bool tried;

        public static bool Available
        {
            get
            {
                if (!tried)
                {
                    tried = true;
                    source = FindSource();
                }
                return source != null;
            }
        }

        // The column the fire wall is made of, or the whole fire wall if that isn't to hand.
        private static GameObject FindSource()
        {
            try
            {
                GameObject wallPrefab = FireWall.Prefab;
                if (wallPrefab == null)
                    throw new System.Exception("no fire wall prefab");
                // The wave's own column prefab (FireWall's static 'prefab' is the whole wall).
                Wave wall = wallPrefab.GetComponent<FireWall>();
                GameObject column = wall != null ? wall.prefab : null;
                if (column == null && wall != null)
                {
                    try
                    {
                        wall.LoadPrefab();
                        column = wall.prefab;
                    }
                    catch
                    {
                    }
                }
                TrailblazerPlugin.Log("Trailblazer: using Searing Rush's fire " +
                    (column != null ? $"(column '{column.name}')" : "(the whole fire wall)"));
                return column != null ? column : wallPrefab;
            }
            catch (System.Exception e)
            {
                TrailblazerPlugin.Log($"Trailblazer: Searing Rush's fire is unavailable, using plain flames: {e.Message}");
                return null;
            }
        }

        // A burning column at a spot; scale 1 is the game's own size.
        public static GameObject Light(Vector2 position, float scale, SpriteRenderer sortLike)
        {
            if (!Available)
                return null;
            GameObject go = null;
            try
            {
                // Copied switched off so none of its scripts ever run, stripped, then switched on.
                bool wasActive = source.activeSelf;
                source.SetActive(false);
                try
                {
                    go = Object.Instantiate(source, position, Quaternion.identity);
                }
                finally
                {
                    source.SetActive(wasActive);
                }
                Strip<MonoBehaviour>(go);
                Strip<Collider2D>(go);
                Strip<Rigidbody2D>(go);
                Strip<AudioSource>(go);
                go.transform.localScale = new Vector3(scale, scale, 1f);
                go.SetActive(true);
                foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                    child.gameObject.SetActive(true);
                foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    ParticleSystem.MainModule main = system.main;
                    main.loop = true;
                    main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                    ParticleSystem.EmissionModule emission = system.emission;
                    emission.enabled = true;
                    if (emission.rateOverTimeMultiplier < 0.01f)
                        emission.rateOverTime = new ParticleSystem.MinMaxCurve(20f);
                    system.Play();
                }
                foreach (Animator anim in go.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
                if (sortLike != null)
                {
                    foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
                    {
                        renderer.sortingLayerID = sortLike.sortingLayerID;
                        renderer.sortingOrder = sortLike.sortingOrder - 1;
                    }
                }
                return go;
            }
            catch (System.Exception e)
            {
                if (go != null)
                    Object.Destroy(go);
                TrailblazerPlugin.Log($"Trailblazer: couldn't light a fire column: {e.Message}");
                tried = true;
                source = null;
                return null;
            }
        }

        // Stops the flames rising and clears the column away once the last of them has burnt out.
        public static void PutOut(GameObject column)
        {
            if (column == null)
                return;
            float longest = 0f;
            foreach (ParticleSystem system in column.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                longest = Mathf.Max(longest, system.main.startLifetimeMultiplier);
            }
            foreach (SpriteRenderer sprite in column.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.enabled = false;
            Object.Destroy(column, Mathf.Clamp(longest, 0.2f, 2f));
        }

        private static void Strip<T>(GameObject go) where T : Component
        {
            foreach (T component in go.GetComponentsInChildren<T>(true))
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
    }
}
