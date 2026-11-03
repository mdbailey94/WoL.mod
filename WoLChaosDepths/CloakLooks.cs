using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace WoLChaosDepths
{
    // #80/#73: the worn cloak's upgrade tier (the Cloak Upgrades mod) shows on the wizard, with WoL
    // Art's pieces (Finished Art/Cloak upgrades mockup) in the cloak's talent archetype:
    //   T1   a dull-gold trim along the cloak's hem (each column's lowest cloak pixel);
    //   T2   the same trim on the hood rim too, and a 2x2 clasp gem at the chest (facing down);
    //   T3   the trims turn bright gold, and the archetype's 5x5 emblem shows on the back (facing up);
    //   T4   a light shimmer runs along the hem, and motes in the archetype's colour rise round
    //        the wizard.
    // The trim is an overlay of the body's frame, built once per frame and look from the cloak's
    // pixels (BodyPixels.CloakEdges: the sheet's greys from 95 up). The tier is read from Cloak Upgrades'
    // CloakScaling.TierOf (by reflection, so neither mod needs the other); without the mod, nothing
    // shows. Frame order in the strips: Bulwark, Duelist, Arcanist, Windrunner, Mender, Gambler,
    // Apprentice.
    public static class CloakLooks
    {
        private static readonly string[] Archetypes = { "Bulwark", "Duelist", "Arcanist", "Windrunner", "Mender", "Gambler", "Apprentice" };
        private static readonly Color[] Colours =
        {
            new Color32(0x9b, 0x9d, 0xad, 0xff), new Color32(0xe0, 0x48, 0x48, 0xff), new Color32(0xa6, 0x6c, 0xff, 0xff),
            new Color32(0x6f, 0xe0, 0xf0, 0xff), new Color32(0x6f, 0xe0, 0x7a, 0xff), new Color32(0xff, 0xd3, 0x4d, 0xff),
            new Color32(0xf0, 0xf0, 0xf0, 0xff)
        };
        // Art's trim colours (Log - Art 19:55): dull gold (T1-T2, #d9a531 mixed 35% toward
        // #8a6418), bright gold (T3+), the T4 shimmer.
        private static readonly Color TrimDull = Color.Lerp(new Color32(0xd9, 0xa5, 0x31, 0xff), new Color32(0x8a, 0x64, 0x18, 0xff), 0.35f);
        private static readonly Color TrimBright = new Color32(0xd9, 0xa5, 0x31, 0xff), TrimShimmer = new Color32(0xff, 0xf0, 0xa8, 0xff);
        private const int ShimmerPhases = 7;
        private static readonly Dictionary<KeyValuePair<Sprite, int>, Sprite> trims = new Dictionary<KeyValuePair<Sprite, int>, Sprite>();
        private static Func<string, int> tierOf;
        private static float nextLookup;
        private static Sprite[] clasps, emblems, motes;
        private static bool artTried;

        public static void Install()
        {
            var host = new GameObject("ChaosDepths CloakLooks");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Runner>();
        }

        private static int Tier(Player p)
        {
            if (tierOf == null && Time.unscaledTime >= nextLookup)
            {
                nextLookup = Time.unscaledTime + 5f;
                foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    Type t = asm.GetType("WoLCloakUpgrades.CloakScaling", false);
                    FieldInfo f = t != null ? t.GetField("TierOf", BindingFlags.Public | BindingFlags.Static) : null;
                    if (f != null)
                    {
                        tierOf = f.GetValue(null) as Func<string, int>;
                        break;
                    }
                }
            }
            string id = p != null && p.equippedOutfit != null ? p.equippedOutfit.outfitID : null;
            try
            {
                return tierOf != null && id != null ? Mathf.Clamp(tierOf(id), 0, 4) : 0;
            }
            catch
            {
                return 0;
            }
        }

        private static int ArchetypeOf(Player p)
        {
            Talents.Tree tree = Talents.TreeFor(Talents.CloakOf(p));
            int i = tree != null ? Array.IndexOf(Archetypes, tree.Key) : -1;
            return i >= 0 ? i : Archetypes.Length - 1;
        }

        private static bool Art()
        {
            if (!artTried)
            {
                artTried = true;
                clasps = Strip("cloak_clasp_gems", 2);
                emblems = Strip("cloak_emblems", 5);
                motes = Strip("cloak_mote", 8);
                ChaosDepthsPlugin.Log($"Cloak looks: art {(clasps != null && emblems != null && motes != null ? "loaded" : "missing, nothing drawn")}");
            }
            return clasps != null && emblems != null && motes != null;
        }

        private static Sprite[] Strip(string name, int w)
        {
            Texture2D tex = ItemArt.LoadPng(name);
            if (tex == null || tex.width < w)
                return null;
            tex.filterMode = FilterMode.Point;
            var frames = new Sprite[tex.width / w];
            for (int i = 0; i < frames.Length; i++)
                frames[i] = Sprite.Create(tex, new Rect(i * w, 0, w, tex.height), new Vector2(0.5f, 0.5f), 16f);
            return frames;
        }

        // The trim for one body frame: look 1 (dull hem), 2 (dull hem and rim), 3 (bright hem and
        // rim), or 10 + phase (bright, with the shimmer's light every 7th hem column, moving).
        // Null when the frame's cloak can't be read.
        private static Sprite Trim(Sprite body, int look)
        {
            var key = new KeyValuePair<Sprite, int>(body, look);
            Sprite made;
            if (trims.TryGetValue(key, out made))
                return made;
            made = null;
            BodyPixels.Edges e = BodyPixels.CloakEdges(body);
            if (e != null && e.hem.Count > 0)
            {
                if (trims.Count > 4000)
                    trims.Clear();   // frames from scenes long gone
                int fw = Mathf.RoundToInt(body.rect.width), fh = Mathf.RoundToInt(body.rect.height);
                int ox = Mathf.RoundToInt(body.textureRectOffset.x), oy = Mathf.RoundToInt(body.textureRectOffset.y);
                var tex = new Texture2D(fw, fh, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                var px = new Color32[fw * fh];
                Color edge = look >= 3 ? TrimBright : TrimDull;
                int phase = look >= 10 ? look - 10 : -1;
                void Put(int index, Color c)
                {
                    int x = index % e.width + ox, y = index / e.width + oy;
                    if (x >= 0 && x < fw && y >= 0 && y < fh)
                        px[y * fw + x] = c;
                }
                foreach (int i in e.hem)
                    Put(i, phase >= 0 && (i % e.width + phase * 3) % ShimmerPhases == 0 ? TrimShimmer : edge);
                if (look >= 2)
                    foreach (int i in e.rim)
                        Put(i, edge);
                tex.SetPixels32(px);
                tex.Apply();
                made = Sprite.Create(tex, new Rect(0, 0, fw, fh), new Vector2(body.pivot.x / fw, body.pivot.y / fh), body.pixelsPerUnit);
            }
            trims[key] = made;
            return made;
        }

        private class Runner : MonoBehaviour
        {
            private readonly Dictionary<int, Look> looks = new Dictionary<int, Look>();
            private bool loggedError;

            private void LateUpdate()
            {
                try
                {
                    Player[] players = GameController.activePlayers;
                    if (players == null || !Art())
                        return;
                    // A wizard that's gone (a new scene builds new ones): its parts go too.
                    foreach (int key in new List<int>(looks.Keys))
                        if (looks[key] == null || looks[key].Gone)
                        {
                            looks[key]?.Dispose();
                            looks.Remove(key);
                        }
                    foreach (Player p in players)
                    {
                        if (p == null)
                            continue;
                        Look look;
                        if (!looks.TryGetValue(p.GetInstanceID(), out look) || look == null)
                            looks[p.GetInstanceID()] = look = Look.For(p);
                        look.Tick();
                    }
                }
                catch (Exception e)
                {
                    if (!loggedError)
                    {
                        loggedError = true;
                        ChaosDepthsPlugin.LogError($"Cloak looks: {e.Message}");
                    }
                }
            }
        }

        // One wizard's clasp, emblem and motes.
        private class Look
        {
            private Player player;
            private SpriteRenderer clasp, emblem, trim;
            private float nextMote;
            private int lastTier = -1;

            public bool Gone => player == null;

            public void Dispose()
            {
                if (clasp != null)
                    UnityEngine.Object.Destroy(clasp.gameObject);
                if (emblem != null)
                    UnityEngine.Object.Destroy(emblem.gameObject);
                if (trim != null)
                    UnityEngine.Object.Destroy(trim.gameObject);
            }

            public static Look For(Player p)
            {
                var look = new Look { player = p };
                look.clasp = Part("ChaosDepths Cloak Clasp");
                look.emblem = Part("ChaosDepths Cloak Emblem");
                look.trim = Part("ChaosDepths Cloak Trim");
                return look;
            }

            private static SpriteRenderer Part(string name)
            {
                var go = new GameObject(name);
                UnityEngine.Object.DontDestroyOnLoad(go);
                SpriteRenderer r = go.AddComponent<SpriteRenderer>();
                r.enabled = false;
                return r;
            }

            public void Tick()
            {
                SpriteRenderer body = player != null && player.gameObject.activeInHierarchy ? player.spriteRenderer : null;
                int tier = body != null && body.enabled ? Tier(player) : 0;
                if (tier != lastTier)
                {
                    lastTier = tier;
                    if (player != null)
                        ChaosDepthsPlugin.Log($"Cloak looks: player {player.playerID + 1}'s cloak at tier {tier}");
                }
                clasp.enabled = emblem.enabled = trim.enabled = false;
                if (tier < 1 || body == null || body.sprite == null)
                    return;
                // The trim, laid exactly over the body's frame (same place, flip and size).
                int look = tier >= 4 ? 10 + (int)(Time.time * 8f) % ShimmerPhases : Mathf.Min(tier, 3);
                Sprite t = Trim(body.sprite, look);
                if (t != null)
                {
                    trim.sprite = t;
                    trim.transform.position = body.transform.position + new Vector3(0f, 0f, -0.005f);
                    trim.transform.rotation = body.transform.rotation;
                    trim.transform.localScale = body.transform.lossyScale;
                    trim.flipX = body.flipX;
                    trim.flipY = body.flipY;
                    trim.color = new Color(1f, 1f, 1f, body.color.a);   // fades with the body
                    trim.sortingLayerID = body.sortingLayerID;
                    trim.sortingOrder = body.sortingOrder + 1;
                    trim.enabled = true;
                }
                if (tier < 2)
                    return;
                int arche = ArchetypeOf(player);
                FacingDirection facing = player.facingDirection;
                BodyPixels.Shape shape = BodyPixels.Of(body.sprite);
                if (shape != null && shape.top > shape.bottom)
                {
                    if (facing == FacingDirection.Down)
                        Place(clasp, body, shape, 0.62f, clasps[Mathf.Min(arche, clasps.Length - 1)]);
                    else if (facing == FacingDirection.Up && tier >= 3)
                        Place(emblem, body, shape, 0.55f, emblems[Mathf.Min(arche, emblems.Length - 1)]);
                }
                if (tier >= 4 && Time.time >= nextMote)
                {
                    nextMote = Time.time + 0.3f;
                    Mote.Rise(body, Colours[arche], UnityEngine.Random.value < 0.5f);
                }
            }

            // On the middle of the picture's row at `height` (a fraction of its drawn height).
            private static void Place(SpriteRenderer part, SpriteRenderer body, BodyPixels.Shape shape, float height, Sprite sprite)
            {
                int y = shape.bottom + Mathf.RoundToInt((shape.top - shape.bottom) * height);
                int lo, hi;
                if (!shape.Row(y, out lo, out hi))
                    return;
                int x = (lo + hi) / 2;
                Vector2 local = shape.Local(x, y);
                if (body.flipX)
                    local.x = -local.x;
                part.transform.position = body.transform.TransformPoint(local) + new Vector3(0f, 0f, -0.01f);
                part.transform.localScale = body.transform.lossyScale;
                part.sprite = sprite;
                part.sortingLayerID = body.sortingLayerID;
                part.sortingOrder = body.sortingOrder + 2;
                part.enabled = true;
            }
        }

        // A T4 mote: rises 0.7 u over 1.2 s beside the wizard, its 4 frames looping, then goes.
        private class Mote : MonoBehaviour
        {
            private SpriteRenderer r;
            private float born;
            private Vector3 from;

            public static void Rise(SpriteRenderer body, Color colour, bool behind)
            {
                var go = new GameObject("ChaosDepths Cloak Mote");
                Mote m = go.AddComponent<Mote>();
                m.r = go.AddComponent<SpriteRenderer>();
                m.r.color = colour;
                m.r.sortingLayerID = body.sortingLayerID;
                m.r.sortingOrder = body.sortingOrder + (behind ? -1 : 1);
                m.r.sprite = motes[0];
                float side = UnityEngine.Random.Range(0.35f, 0.7f) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
                m.from = body.transform.position + new Vector3(side, UnityEngine.Random.Range(0.1f, 0.9f), 0f);
                go.transform.position = m.from;
                m.born = Time.time;
            }

            private void Update()
            {
                float t = (Time.time - born) / 1.2f;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    return;
                }
                transform.position = from + new Vector3(0f, 0.7f * t, 0f);
                r.sprite = motes[(int)(t * 12f) % motes.Length];
                Color c = r.color;
                c.a = 1f - t * t;
                r.color = c;
            }
        }
    }
}
