using System.Collections.Generic;
using UnityEngine;

namespace WoLGaleRider
{
    // The game's own effect sprites, found among what's loaded so the mod's effects can play them
    // (and so look exactly like the game's). Named sprites are looked up by texture and name (as
    // the game's atlases name them, e.g. WindSlash/WindBall0); particle sheets, which have no
    // named sprites, are cut into cells from their texture. Lookups are cached; a miss rescans
    // what's loaded at most every RescanInterval seconds (a new level may have brought it in),
    // and the callers fall back to their drawn art meanwhile.
    public static class GameSprites
    {
        private const float RescanInterval = 10f;

        private static Dictionary<string, Sprite> sprites;
        private static Dictionary<string, Texture2D> textures;
        private static float lastScan = -999f;
        private static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

        // Frames prefix+first .. prefix+(first+count-1) from texture, or null if any is missing.
        public static Sprite[] Named(string texture, string prefix, int first, int count)
        {
            string key = $"n/{texture}/{prefix}/{first}/{count}";
            Sprite[] found;
            if (cache.TryGetValue(key, out found) && AllAlive(found))
                return found;
            found = FindNamed(texture, prefix, first, count);
            if (found == null && Rescan())
                found = FindNamed(texture, prefix, first, count);
            if (found != null)
                cache[key] = found;
            return found;
        }

        // count cells of cellW x cellH from a sheet, along row (counted from the top, as the
        // sheet reads) from column first; pivot in cell pixels from the bottom-left. Null if the
        // texture isn't loaded.
        public static Sprite[] Cells(string texture, int cellW, int cellH, int row, int first, int count, Vector2 pivot)
        {
            string key = $"c/{texture}/{cellW}/{cellH}/{row}/{first}/{count}/{pivot.x}/{pivot.y}";
            Sprite[] found;
            if (cache.TryGetValue(key, out found) && AllAlive(found))
                return found;
            Texture2D tex = FindTexture(texture);
            if (tex == null && Rescan())
                tex = FindTexture(texture);
            if (tex == null)
                return null;
            found = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                var rect = new Rect((first + i) * cellW, tex.height - (row + 1) * cellH, cellW, cellH);
                found[i] = Sprite.Create(tex, rect, new Vector2(pivot.x / cellW, pivot.y / cellH), PixelArt.Ppu, 0, SpriteMeshType.FullRect);
                found[i].hideFlags = HideFlags.HideAndDontSave;
            }
            cache[key] = found;
            return found;
        }

        // One cell each from several rows (one animation frame from each variant).
        public static Sprite[] Column(string texture, int cellW, int cellH, int column, int rows, Vector2 pivot)
        {
            var frames = new Sprite[rows];
            for (int r = 0; r < rows; r++)
            {
                Sprite[] cell = Cells(texture, cellW, cellH, r, column, 1, pivot);
                if (cell == null)
                    return null;
                frames[r] = cell[0];
            }
            return frames;
        }

        // Moves a game sprite placed at its pivot so that the point centre (in its rect's pixels,
        // from the bottom-left) lands where it's put: the offset to add to its position.
        public static Vector2 PivotShift(Sprite sprite, Vector2 centre, float scale) =>
            sprite == null ? Vector2.zero : (sprite.pivot - centre) / sprite.pixelsPerUnit * scale;

        private static bool AllAlive(Sprite[] frames)
        {
            if (frames == null)
                return false;
            foreach (Sprite s in frames)
            {
                if (s == null || s.texture == null)
                    return false;
            }
            return true;
        }

        private static Sprite[] FindNamed(string texture, string prefix, int first, int count)
        {
            Scan();
            var found = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                Sprite s;
                if (!sprites.TryGetValue(texture + "/" + prefix + (first + i), out s) || s == null || s.texture == null)
                    return null;
                found[i] = s;
            }
            return found;
        }

        private static Texture2D FindTexture(string name)
        {
            Scan();
            Texture2D tex;
            return textures.TryGetValue(name, out tex) && tex != null ? tex : null;
        }

        private static void Scan()
        {
            if (sprites == null)
                Rescan();
        }

        // Indexes what's loaded now; false (nothing done) if it was done too recently.
        private static bool Rescan()
        {
            if (sprites != null && Time.realtimeSinceStartup - lastScan < RescanInterval)
                return false;
            lastScan = Time.realtimeSinceStartup;
            sprites = new Dictionary<string, Sprite>();
            textures = new Dictionary<string, Texture2D>();
            foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
            {
                if (s == null || s.texture == null || (s.hideFlags & HideFlags.DontSave) != 0)
                    continue;
                string key = s.texture.name + "/" + s.name;
                if (!sprites.ContainsKey(key))
                    sprites[key] = s;
            }
            foreach (Texture2D t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                if (t != null && !textures.ContainsKey(t.name))
                    textures[t.name] = t;
            }
            return true;
        }
    }
}
