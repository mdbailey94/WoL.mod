using System.Collections.Generic;
using UnityEngine;

namespace WoLChaosDepths
{
    // Phase 1a: one visual effect per keystone talent (Matt 2026-11-02, Log - Player 17:10). The
    // talent effects (TalentEffects / ArchetypeMechanics, WoL Modder; TalentMechanics, WoL Player)
    // call Fire when a keystone acts and SetActive for the ones that last while they apply. Each
    // plays WoL Art's strip fx_ks_<Key>.png (Log - Art 17:14) on the wizard's feet (or a struck
    // enemy's): once for Fire, looping while active. Drawn in code: Afterimage (a fading cyan copy
    // of the wizard), Flurry's shards orbiting one per stack, Lifebloom's tether stretched between
    // the two wizards, Assassin's crosshair over enemies still at full health, and Cataclysm's
    // violet screen tint. A strip that isn't there is skipped (logged once). Only while [Talents]
    // Enabled.
    public static class KeystoneFx
    {
        private const float Fps = 12f;
        private const float AfterimageLife = 1.5f;
        private static readonly Color AfterimageTint = new Color32(0x6f, 0xe0, 0xf0, 0x80);   // WoL Art's #6fe0f0 at 50%
        private static readonly Color CataclysmTint = new Color32(0xa6, 0x6c, 0xff, 0x26);    // #a66cff at about 15%
        private const float CataclysmTintTime = 0.3f;
        private const int FlurryShards = 5;
        private const float FlurryRadius = 0.6f, FlurrySpin = 4f;
        private const float MarkRange = 10f, MarkScan = 0.25f;
        private static readonly HashSet<string> missing = new HashSet<string>();

        // WoL Art's strips: frame size, frame count, fps, the anchor point in the frame (from its
        // top left) that goes on the feet, how far above the feet (units), and whether it's drawn
        // behind. 'only' picks single frames (Archmage's ring is state frames: 0 idle, 4 the flare).
        private class Spec
        {
            public string file;
            public int w, h, n;
            public float fps, ax, ay, lift;
            public bool behind;
            public int[] only;
        }

        private static readonly Dictionary<string, Spec> specs = new Dictionary<string, Spec>
        {
            { "Graduate", new Spec { w = 32, h = 32, n = 6, fps = 12, ax = 16, ay = 18 } },
            { "Survivor", new Spec { w = 32, h = 40, n = 5, fps = 12, ax = 16, ay = 36 } },
            { "Juggernaut", new Spec { w = 40, h = 16, n = 5, fps = 12, ax = 20, ay = 9 } },
            { "LastStand", new Spec { w = 40, h = 20, n = 6, fps = 10, ax = 20, ay = 12, behind = true } },
            { "Assassin", new Spec { w = 24, h = 24, n = 4, fps = 12, ax = 12, ay = 12, lift = 0.6f } },
            { "Assassin_mark", new Spec { w = 9, h = 9, n = 2, fps = 3, ax = 4.5f, ay = 4.5f, lift = 1.7f } },
            { "Flurry", new Spec { w = 8, h = 8, n = 4, fps = 12, ax = 4, ay = 4, lift = 0.8f } },
            { "Archmage", new Spec { w = 32, h = 16, n = 5, fps = 1, ax = 16, ay = 8, behind = true, only = new[] { 0 } } },
            { "Archmage!", new Spec { file = "fx_ks_Archmage", w = 32, h = 16, n = 5, fps = 4, ax = 16, ay = 8, behind = true, only = new[] { 4 } } },
            { "Cataclysm", new Spec { w = 64, h = 32, n = 6, fps = 12, ax = 32, ay = 16 } },
            { "Velocity", new Spec { w = 24, h = 24, n = 4, fps = 16, ax = 12, ay = 12, lift = 0.6f, behind = true } },
            { "Sanctuary", new Spec { w = 64, h = 32, n = 6, fps = 10, ax = 32, ay = 16 } },
            { "Lifebloom", new Spec { w = 16, h = 8, n = 4, fps = 12, ax = 8, ay = 4, lift = 0.6f } },
            { "AllIn", new Spec { w = 32, h = 40, n = 6, fps = 10, ax = 16, ay = 35, behind = true } },
            { "DoubleOrNothing", new Spec { w = 12, h = 12, n = 6, fps = 10, ax = 6, ay = 6, lift = 1.9f } },
            { "DoubleOrNothing!", new Spec { file = "fx_ks_DoubleOrNothing_shatter", w = 16, h = 16, n = 4, fps = 12, ax = 8, ay = 8, lift = 1.9f } },
            // #76 pact effects (Log - Art 2026-11-03 10:40).
            { "Pact_BorrowedTime", new Spec { file = "fx_pact_BorrowedTime", w = 16, h = 24, n = 6, fps = 8, ax = 8, ay = 24, lift = 1.3f } },
            { "Pact_Bastion", new Spec { file = "fx_pact_Bastion", w = 40, h = 16, n = 4, fps = 6, ax = 20, ay = 9, behind = true } },
            { "Pact_MarkedForDeath", new Spec { file = "fx_pact_MarkedForDeath", w = 9, h = 9, n = 2, fps = 3, ax = 4.5f, ay = 4.5f, lift = 1.7f } },
            { "Pact_Overchannel", new Spec { file = "fx_pact_Overchannel", w = 24, h = 24, n = 4, fps = 10, ax = 12, ay = 12, lift = 0.6f } },
            { "Pact_MomentumEngine", new Spec { file = "fx_pact_MomentumEngine", w = 16, h = 8, n = 4, fps = 12, ax = 8, ay = 4, lift = 1.9f } },
            { "Pact_Martyr", new Spec { file = "fx_pact_Martyr", w = 32, h = 14, n = 4, fps = 8, ax = 16, ay = 7, behind = true } },
            { "Pact_DoubleDown", new Spec { file = "fx_pact_DoubleDown", w = 12, h = 12, n = 6, fps = 10, ax = 6, ay = 6, lift = 1.9f } },
        };

        // The looping effects showing now, per wizard and keystone (Flurry: one per shard).
        private static readonly Dictionary<string, Fx> looping = new Dictionary<string, Fx>();
        // Assassin's crosshairs, per enemy.
        private static readonly Dictionary<int, Fx> marks = new Dictionary<int, Fx>();
        private static Material material;
        private static float tintUntil = -1f;
        private static Host host;

        // A keystone just acted: its effect plays once on the wizard.
        public static void Fire(Player player, string key) => Fire((Entity)player, key);

        // ... or on another entity (Assassin's slash on the struck enemy).
        public static void Fire(Entity target, string key)
        {
            if (!Talents.Enabled || target == null || target.spriteRenderer == null)
                return;
            EnsureHost();
            try
            {
                Player player = target as Player;
                if (key == "Afterimage" && player != null)
                    Afterimage(player);
                else
                {
                    if (key == "Cataclysm")
                        tintUntil = Time.unscaledTime + CataclysmTintTime;
                    // A one-shot of a lasting keystone has its own strip where Art drew one
                    // (Archmage's flare, Double or Nothing's shatter).
                    Spawn(target, specs.ContainsKey(key + "!") ? key + "!" : key, false);
                }
            }
            catch (System.Exception e)
            {
                Missing(key, e.Message);
            }
        }

        // A keystone's effect left on the floor at a spot, looping for this long (Sanctuary's circle).
        public static void FireAt(Player player, Vector2 spot, string key, float seconds)
        {
            if (!Talents.Enabled || player == null || player.spriteRenderer == null)
                return;
            try
            {
                Fx fx = Spawn(player, key, true);
                if (fx != null)
                    fx.Pin(spot, seconds);
            }
            catch (System.Exception e)
            {
                Missing(key, e.Message);
            }
        }

        // A lasting keystone turned on or off (Last Stand under 30% health, Flurry's stacks, ...).
        // Calling it every frame with the same value is fine.
        public static void SetActive(Player player, string key, bool on)
        {
            if (player == null)
                return;
            EnsureHost();
            int count = key == "Flurry" ? FlurryShards : 1;
            for (int i = 0; i < count; i++)
            {
                string id = player.playerID + "/" + key + (count > 1 ? "/" + i : "");
                Fx fx;
                looping.TryGetValue(id, out fx);
                if (fx != null && (!on || !Talents.Enabled))
                {
                    Object.Destroy(fx.gameObject);
                    looping.Remove(id);
                }
                else if (fx == null && on && Talents.Enabled && player.spriteRenderer != null)
                {
                    try
                    {
                        fx = Spawn(player, key, true);
                        if (fx == null)
                            continue;
                        if (key == "Flurry")
                            fx.Orbit(i);
                        else if (key == "Lifebloom")
                            fx.Tether();
                        looping[id] = fx;
                    }
                    catch (System.Exception e)
                    {
                        Missing(key, e.Message);
                    }
                }
            }
        }

        // Every looping effect off (a level load, talents switched off).
        public static void Clear()
        {
            foreach (Fx fx in looping.Values)
                if (fx != null)
                    Object.Destroy(fx.gameObject);
            looping.Clear();
            foreach (Fx fx in marks.Values)
                if (fx != null)
                    Object.Destroy(fx.gameObject);
            marks.Clear();
        }

        private static Fx Spawn(Entity owner, string key, bool loop)
        {
            Spec spec;
            specs.TryGetValue(key, out spec);
            string name = spec != null && spec.file != null ? spec.file : "fx_ks_" + key;
            Texture2D tex = ItemArt.LoadPng(name);
            if (tex == null)
            {
                Missing(key, "no " + name + ".png yet");
                return null;
            }
            // Without a spec: square frames, anchored at the bottom middle.
            int count = spec != null ? spec.n : Mathf.Max(1, tex.width / Mathf.Max(1, tex.height));
            Vector2 pivot = spec != null ? new Vector2(spec.ax / spec.w, (spec.h - spec.ay) / spec.h) : new Vector2(0.5f, 0.25f);
            SpriteRenderer body = owner.spriteRenderer;
            float ppu = body.sprite != null && body.sprite.pixelsPerUnit > 0f ? body.sprite.pixelsPerUnit : 16f;
            Sprite[] frames = ItemArt.Frames(name, count, pivot, ppu);
            if (frames == null || frames.Length == 0)
            {
                Missing(key, "couldn't cut " + name + ".png into " + count + " frames");
                return null;
            }
            if (spec != null && spec.only != null)
            {
                var picked = new List<Sprite>();
                foreach (int i in spec.only)
                    if (i >= 0 && i < frames.Length)
                        picked.Add(frames[i]);
                frames = picked.ToArray();
            }
            var go = new GameObject("ChaosDepths Keystone " + key);
            Fx fx = go.AddComponent<Fx>();
            fx.Init(owner, body, frames, ppu, loop, spec != null ? spec.fps : Fps, spec != null ? spec.lift : 0f, spec != null && spec.behind);
            if (key != "Assassin_mark")
                Say(key, $"Keystone: {key} {(loop ? "on" : "fired")} ({owner.name})");
            return fx;
        }

        // Afterimage: the wizard's current picture, tinted cyan, left where it stood and fading.
        private static void Afterimage(Player player)
        {
            SpriteRenderer body = player.spriteRenderer;
            if (body.sprite == null)
                return;
            var go = new GameObject("ChaosDepths Keystone Afterimage");
            go.transform.position = body.transform.position;
            go.transform.localScale = body.transform.lossyScale;
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = body.sprite;
            r.flipX = body.flipX;
            // The wizard's sprite is palette-indexed (its cloak colours come from the game's
            // palette shader and the renderer's property block); drawn with a plain sprite
            // material it showed as a red box (Matt, 2026-11-02). Same material and block as
            // the wizard, tinted cyan where the shader takes a tint.
            r.sharedMaterial = body.sharedMaterial;
            var block = new MaterialPropertyBlock();
            body.GetPropertyBlock(block);
            r.SetPropertyBlock(block);
            r.color = AfterimageTint;
            r.sortingLayerID = body.sortingLayerID;
            r.sortingOrder = body.sortingOrder - 1;
            go.AddComponent<Fade>().life = AfterimageLife;
            Say("Afterimage", "Keystone: Afterimage fired");
        }

        private static Material Material()
        {
            if (material == null)
                material = new Material(Shader.Find("Sprites/Default"));
            return material;
        }

        private static void Missing(string key, string why)
        {
            if (missing.Add(key))
                ChaosDepthsPlugin.Log($"Keystone: no effect for {key} ({why})");
        }

        private static readonly HashSet<string> said = new HashSet<string>();

        private static void Say(string key, string message)
        {
            if (said.Add(key))
                ChaosDepthsPlugin.Log(message);
        }

        private static void EnsureHost()
        {
            if (host != null)
                return;
            var go = new GameObject("ChaosDepths KeystoneFx");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            host = go.AddComponent<Host>();
        }

        // Assassin's crosshairs and Cataclysm's tint.
        private class Host : MonoBehaviour
        {
            private float nextScan;
            private readonly List<int> gone = new List<int>();

            private void Update()
            {
                if (Time.unscaledTime < nextScan)
                    return;
                nextScan = Time.unscaledTime + MarkScan;
                try
                {
                    Marks();
                }
                catch (System.Exception e)
                {
                    Missing("Assassin_mark", e.Message);
                }
            }

            // A crosshair over every enemy still at full health near a wizard with Assassin.
            private void Marks()
            {
                var keep = new HashSet<int>();
                Player[] players = GameController.activePlayers;
                if (Talents.Enabled && players != null)
                {
                    foreach (Player player in players)
                    {
                        if (player == null || Talents.Rank(player, "Assassin") <= 0)
                            continue;
                        foreach (Collider2D col in Physics2D.OverlapCircleAll(player.transform.position, MarkRange))
                        {
                            Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                            if (enemy == null || enemy.health == null || enemy.spriteRenderer == null)
                                continue;
                            Health h = enemy.health;
                            if (h.CurrentHealthValue <= 0 || h.CurrentHealthValue < h.healthStat.ModifiedValue)
                                continue;
                            int id = enemy.GetInstanceID();
                            keep.Add(id);
                            Fx mark;
                            if (!marks.TryGetValue(id, out mark) || mark == null)
                            {
                                mark = Spawn(enemy, "Assassin_mark", true);
                                if (mark != null)
                                    marks[id] = mark;
                            }
                        }
                    }
                }
                gone.Clear();
                foreach (KeyValuePair<int, Fx> pair in marks)
                    if (!keep.Contains(pair.Key))
                        gone.Add(pair.Key);
                foreach (int id in gone)
                {
                    if (marks[id] != null)
                        Destroy(marks[id].gameObject);
                    marks.Remove(id);
                }
            }

            private void OnGUI()
            {
                if (Time.unscaledTime >= tintUntil)
                    return;
                float t = (tintUntil - Time.unscaledTime) / CataclysmTintTime;
                Color previous = GUI.color;
                GUI.color = new Color(CataclysmTint.r, CataclysmTint.g, CataclysmTint.b, CataclysmTint.a * t);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        // A strip on an entity's feet, not parented (so its turning doesn't flip it). Pinned: left
        // on the floor. Orbit: one of Flurry's shards, shown while there are that many stacks.
        // Tether: Lifebloom's segment stretched from this wizard to the other.
        private class Fx : MonoBehaviour
        {
            private Entity owner;
            private SpriteRenderer body, r;
            private Sprite[] frames;
            private float ppu, born, until = -1f, fps, lift;
            private bool loop, pinned, behind, tether;
            private int orbit = -1;
            private Vector2 spot;

            public void Init(Entity entity, SpriteRenderer ownerBody, Sprite[] strip, float pixels, bool loops,
                float framesPerSecond, float above, bool drawBehind)
            {
                owner = entity;
                body = ownerBody;
                frames = strip;
                ppu = pixels;
                loop = loops;
                fps = framesPerSecond;
                lift = above;
                behind = drawBehind;
                born = Time.time;
                r = gameObject.AddComponent<SpriteRenderer>();
                r.material = Material();
                r.sprite = frames[0];
                Follow();
            }

            public void Pin(Vector2 at, float seconds)
            {
                pinned = true;
                spot = at;
                until = Time.time + seconds;
                Follow();
                r.sortingOrder = body.sortingOrder - 5;
            }

            public void Orbit(int index) => orbit = index;

            public void Tether() => tether = true;

            private void LateUpdate()
            {
                int frame = (int)((Time.time - born) * fps);
                if (owner == null || body == null || (!loop && frame >= frames.Length) || (until >= 0f && Time.time >= until))
                {
                    Destroy(gameObject);
                    return;
                }
                r.sprite = frames[loop ? frame % frames.Length : frame];
                Follow();
            }

            private void Follow()
            {
                Vector3 feet = pinned ? (Vector3)spot : owner.transform.position + new Vector3(0f, lift, 0f);
                transform.rotation = Quaternion.identity;
                transform.localScale = Vector3.one;
                r.enabled = true;
                if (orbit >= 0)
                {
                    int stacks = 0;
                    try
                    {
                        stacks = ArchetypeMechanics.FlurryStacks(owner as Player);
                    }
                    catch
                    {
                    }
                    r.enabled = orbit < stacks;
                    float angle = Time.time * FlurrySpin + orbit * Mathf.PI * 2f / FlurryShards;
                    feet += new Vector3(Mathf.Cos(angle) * FlurryRadius, Mathf.Sin(angle) * FlurryRadius * 0.7f, 0f);
                }
                if (tether)
                {
                    Player other = Other(owner as Player);
                    if (other == null)
                        r.enabled = false;
                    else
                    {
                        Vector3 to = other.transform.position + new Vector3(0f, lift, 0f);
                        Vector3 span = to - feet;
                        feet = (feet + to) * 0.5f;
                        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(span.y, span.x) * Mathf.Rad2Deg);
                        float segment = r.sprite != null ? r.sprite.bounds.size.x : 1f;
                        transform.localScale = new Vector3(span.magnitude / Mathf.Max(0.01f, segment), 1f, 1f);
                    }
                }
                transform.position = tether ? feet : new Vector3(Mathf.Round(feet.x * ppu) / ppu, Mathf.Round(feet.y * ppu) / ppu, feet.z);
                r.sortingLayerID = body.sortingLayerID;
                if (!pinned)
                    r.sortingOrder = body.sortingOrder + (behind ? -1 : 20);
            }

            private static Player Other(Player me)
            {
                Player[] players = GameController.activePlayers;
                if (me == null || players == null)
                    return null;
                foreach (Player p in players)
                    if (p != null && p != me && p.gameObject.activeInHierarchy)
                        return p;
                return null;
            }
        }

        private class Fade : MonoBehaviour
        {
            public float life;
            private float born = -1f;
            private SpriteRenderer r;
            private Color start;

            private void LateUpdate()
            {
                if (born < 0f)
                {
                    born = Time.time;
                    r = GetComponent<SpriteRenderer>();
                    start = r.color;
                }
                float t = (Time.time - born) / Mathf.Max(0.01f, life);
                if (t >= 1f || r == null)
                {
                    Destroy(gameObject);
                    return;
                }
                r.color = new Color(start.r, start.g, start.b, start.a * (1f - t));
            }
        }
    }
}
