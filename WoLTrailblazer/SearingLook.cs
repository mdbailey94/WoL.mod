using System.Collections.Generic;
using UnityEngine;

namespace WoLTrailblazer
{
    // The fire Searing Rush leaves behind (the game's FireWall): one of its fire columns is copied
    // for each patch of the trail, stripped down to just its looks (no scripts, so no attack or
    // timing of its own; the trail's hits are separate), sized by the patch's level, and put out
    // when the patch burns out. If the game's fire wall can't be had, the trail uses plain
    // flame particles instead.
    //
    // Columns are pooled (#26, frame-rate drops): copying and stripping one took an Instantiate,
    // eight searches of its children and a DestroyImmediate per component, for every patch (15-20
    // a second at a sprint). Now each column is copied and stripped once; when its patch is put
    // out it is switched off after its flames have died and lit again for a later patch.
    public static class SearingLook
    {
        private static GameObject source;
        private static bool tried;
        private const int PoolCap = 48;  // a full trail is 40 patches
        private static readonly Stack<Column> spare = new Stack<Column>();

        // A pooled column's parts, found once when it was copied.
        private class Column : MonoBehaviour
        {
            public ParticleSystem[] systems;
            public SpriteRenderer[] sprites;
            public Renderer[] renderers;
            public Animator[] animators;

            // Called (Invoke) once the put-out column's last flames have burnt away.
            private void Return()
            {
                if (spare.Count < PoolCap)
                {
                    gameObject.SetActive(false);
                    spare.Push(this);
                }
                else
                    Destroy(gameObject);
            }

            public void ReturnAfter(float seconds) => Invoke(nameof(Return), seconds);
        }

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
                Column column = null;
                while (spare.Count > 0 && column == null)
                    column = spare.Pop(); // a scene change destroys the spares: skip those
                if (column == null)
                    column = Copy(position);
                go = column.gameObject;
                go.transform.position = position;
                go.transform.localScale = new Vector3(scale, scale, 1f);
                go.SetActive(true);
                foreach (SpriteRenderer sprite in column.sprites)
                    if (sprite != null)
                        sprite.enabled = true;
                foreach (ParticleSystem system in column.systems)
                    if (system != null)
                    {
                        system.Clear(false);
                        system.Play(false);
                    }
                foreach (Animator anim in column.animators)
                    if (anim != null)
                        anim.enabled = true;
                if (sortLike != null)
                {
                    foreach (Renderer renderer in column.renderers)
                    {
                        if (renderer == null)
                            continue;
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

        // A new column for the pool: copied switched off so none of its scripts ever run, stripped
        // to its looks, its parts noted, then switched on with every child on and its flames set
        // to loop.
        private static Column Copy(Vector2 position)
        {
            GameObject go;
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
            Column column = go.AddComponent<Column>();
            column.systems = go.GetComponentsInChildren<ParticleSystem>(true);
            column.sprites = go.GetComponentsInChildren<SpriteRenderer>(true);
            column.renderers = go.GetComponentsInChildren<Renderer>(true);
            column.animators = go.GetComponentsInChildren<Animator>(true);
            go.SetActive(true);
            foreach (Transform child in go.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (ParticleSystem system in column.systems)
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
                ParticleSystem.EmissionModule emission = system.emission;
                emission.enabled = true;
                if (emission.rateOverTimeMultiplier < 0.01f)
                    emission.rateOverTime = new ParticleSystem.MinMaxCurve(20f);
            }
            return column;
        }

        // Stops the flames rising and puts the column back in the pool once the last of them has
        // burnt out.
        public static void PutOut(GameObject go)
        {
            if (go == null)
                return;
            Column column = go.GetComponent<Column>();
            if (column == null)
            {
                Object.Destroy(go);
                return;
            }
            float longest = 0f;
            foreach (ParticleSystem system in column.systems)
            {
                if (system == null)
                    continue;
                system.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                longest = Mathf.Max(longest, system.main.startLifetimeMultiplier);
            }
            foreach (SpriteRenderer sprite in column.sprites)
                if (sprite != null)
                    sprite.enabled = false;
            column.ReturnAfter(Mathf.Clamp(longest, 0.2f, 2f));
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
