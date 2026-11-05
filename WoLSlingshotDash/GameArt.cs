using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WoLSlingshotDash
{
    // The game's own effect art (its wind, fire, earth and lightning sheets), found among the
    // loaded textures and sprites at runtime and cut into frames for the pooled FxSprites, so the
    // dashes' effects are drawn in the game's own style. Nothing is copied into the mod: if a
    // sheet isn't loaded (some only load with the enemies or arcana that use them), the mod's own
    // drawn frames stand in for that scene and the log says so once.
    internal static class GameArt
    {
        // Incremented on every level load: art missing in one scene is looked for again in the next.
        internal static int SceneCount;
        private static int scannedScene = -1;
        private static bool hooked;
        private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        private static readonly HashSet<string> reported = new HashSet<string>();

        // Names like "WindEffects_0".."WindEffects_12".
        public static string[] Seq(string prefix, int first, int last)
        {
            var names = new string[last - first + 1];
            for (int i = first; i <= last; i++)
                names[i - first] = prefix + i;
            return names;
        }

        // Everything loaded, by name, once per level (FindObjectsOfTypeAll is slow).
        private static void Scan()
        {
            if (!hooked)
            {
                hooked = true;
                SceneManager.sceneLoaded += (scene, mode) => SceneCount++;
            }
            if (scannedScene == SceneCount)
                return;
            scannedScene = SceneCount;
            textures.Clear();
            sprites.Clear();
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (t == null)
                    continue;
                string key = TextureKey(t.name, t.width, t.height);
                if (!textures.ContainsKey(key))
                    textures[key] = t;
            }
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null || s.texture == null)
                    continue;
                string key = s.texture.name + "/" + s.name;
                if (!sprites.ContainsKey(key))
                    sprites[key] = s;
            }
        }

        private static string TextureKey(string name, int w, int h) => name + "_" + w + "x" + h;

        // Frames cut from a particle sheet laid out in a grid: `frames` cells from column
        // `firstCol` of each row in `rows` (rows counted from the top, as in the PNG), one variant
        // per row. `pivot` is in pixels from a cell's bottom-left corner.
        public static FxAnim Grid(string owner, string name, string texture, int texW, int texH, int cellW, int cellH,
            int[] rows, int firstCol, int frames, Vector2 pivot, float frameTime, bool loop, int loopStart = 0,
            float fadeFrom = 1f, bool white = false, float baseAngle = 0f)
        {
            Scan();
            Texture2D tex;
            if (!textures.TryGetValue(TextureKey(texture, texW, texH), out tex) || tex == null)
            {
                Missing(owner, name, texture);
                return null;
            }
            float ppu = PixelFx.PixelsPerUnit;
            var sets = new Sprite[rows.Length][];
            for (int r = 0; r < rows.Length; r++)
            {
                sets[r] = new Sprite[frames];
                for (int f = 0; f < frames; f++)
                {
                    var rect = new Rect((firstCol + f) * cellW, texH - (rows[r] + 1) * cellH, cellW, cellH);
                    sets[r][f] = Sprite.Create(tex, rect, new Vector2(pivot.x / cellW, pivot.y / cellH), ppu, 0, SpriteMeshType.FullRect);
                    sets[r][f].name = name + " " + r + "/" + f;
                }
            }
            SlingshotDashPlugin.Log($"{owner}: visual '{name}' ready (the game's {texture} sheet: {frames} frames" +
                (rows.Length > 1 ? $" x {rows.Length} variants" : "") + $", {cellW}x{cellH} px, ppu {ppu:0.#})");
            return new FxAnim(name, sets, frameTime, loop, loopStart, fadeFrom, white, true, baseAngle);
        }

        // The game's named sprites (from `texture`), recut at the wizard's pixels per unit. `pivot`
        // (a fraction of each sprite's rect, as Unity pivots are) re-centres frames of one size;
        // null keeps each sprite's own pivot, which is how the game lines up sequences of
        // different-sized, tightly cropped frames (its explosion, rings, slashes). cropBottom/cropTop
        // keep only part of each (pixels from the rect's bottom), e.g. the far half of a
        // whirlwind, to draw behind the wizard; the pivot stays where it was.
        public static FxAnim Named(string owner, string name, string texture, string[] names, Vector2? pivot, float frameTime,
            bool loop, int loopStart = 0, float fadeFrom = 1f, bool white = false, float baseAngle = 0f,
            int cropBottom = 0, int cropTop = int.MaxValue)
        {
            Scan();
            var frames = new Sprite[names.Length];
            float ppu = PixelFx.PixelsPerUnit;
            for (int i = 0; i < names.Length; i++)
            {
                Sprite source;
                if (!sprites.TryGetValue(texture + "/" + names[i], out source) || source == null || source.texture == null)
                {
                    Missing(owner, name, names[i]);
                    return null;
                }
                Rect rect = source.textureRect;
                Vector2 own = source.pivot - source.textureRectOffset; // the sprite's pivot, from its packed rect's corner
                float px = pivot.HasValue ? pivot.Value.x * rect.width : own.x;
                float py = pivot.HasValue ? pivot.Value.y * rect.height : own.y;
                float bottom = Mathf.Clamp(cropBottom, 0, rect.height), top = Mathf.Clamp(cropTop, bottom, rect.height);
                if (top - bottom < 1f)
                    top = Mathf.Min(rect.height, bottom + 1f);
                var cut = new Rect(rect.x, rect.y + bottom, rect.width, top - bottom);
                frames[i] = Sprite.Create(source.texture, cut, new Vector2(px / cut.width, (py - bottom) / cut.height), ppu, 0, SpriteMeshType.FullRect);
                frames[i].name = name + " " + i;
            }
            SlingshotDashPlugin.Log($"{owner}: visual '{name}' ready (the game's sprites {names[0]}" +
                (names.Length > 1 ? $"..{names[names.Length - 1]}" : "") + $", {names.Length} frames, ppu {ppu:0.#})");
            return new FxAnim(name, new[] { frames }, frameTime, loop, loopStart, fadeFrom, white, true, baseAngle);
        }

        private static void Missing(string owner, string name, string what)
        {
            if (reported.Add(owner + "/" + name))
                SlingshotDashPlugin.Log($"{owner}: the game's '{what}' isn't loaded here, using the mod's own drawing for '{name}'");
        }
    }

    // One of a dash's visuals: the game's art when it can be had, otherwise the mod's drawn
    // stand-in. The game's frames are rebuilt if their texture goes away, and looked for again
    // after each level load if they were missing.
    internal sealed class ArtSlot
    {
        private readonly Func<FxAnim> game, drawn;
        private FxAnim gameAnim, drawnAnim;
        private int missingScene = -1;

        public ArtSlot(Func<FxAnim> game, Func<FxAnim> drawn)
        {
            this.game = game;
            this.drawn = drawn;
        }

        public FxAnim Get()
        {
            if (gameAnim != null && gameAnim.Valid)
                return gameAnim;
            if (game != null && missingScene != GameArt.SceneCount)
            {
                try
                {
                    gameAnim = game();
                }
                catch (Exception e)
                {
                    gameAnim = null;
                    SlingshotDashPlugin.Log($"Slingshot visuals: couldn't cut the game's art ({e.Message}), using the mod's own drawing");
                }
                if (gameAnim != null)
                    return gameAnim;
                missingScene = GameArt.SceneCount;
            }
            if (drawnAnim == null && drawn != null)
                drawnAnim = drawn();
            return drawnAnim;
        }
    }
}
