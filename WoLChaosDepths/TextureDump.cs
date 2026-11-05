using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace WoLChaosDepths
{
    // F12 (debug): writes the game's loaded textures whose names contain any of the
    // [Debug] DumpTextures terms as PNGs, at true pixel size, plus an index of every sprite cut
    // from each one (name and rectangle), so reference art can be taken from the game itself.
    // Textures the game marks unreadable are copied through a render texture first.
    public static class TextureDump
    {
        public static string Folder => Path.Combine(SaveStore.Folder, "dump");

        public static void Run(string filter)
        {
            try
            {
                var terms = new List<string>();
                foreach (string t in (filter ?? "").Split(','))
                    if (t.Trim().Length > 0)
                        terms.Add(t.Trim());
                if (terms.Count == 0)
                {
                    ChaosDepthsPlugin.Log("Debug: set [Debug] DumpTextures to a comma-separated list of name parts (e.g. Ice,Burn,Fire,ChaosGem,Status) and press F12 again");
                    return;
                }
                Directory.CreateDirectory(Folder);
                var index = new StringBuilder();
                var dumped = new Dictionary<Texture2D, string>();
                int written = 0, skipped = 0;
                foreach (Texture2D tex in Resources.FindObjectsOfTypeAll<Texture2D>())
                {
                    if (tex == null || string.IsNullOrEmpty(tex.name) || tex.width <= 0 || tex.height <= 0)
                        continue;
                    if (tex.width > 4096 || tex.height > 4096)
                        continue;
                    bool match = false;
                    foreach (string t in terms)
                        if (tex.name.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            match = true;
                            break;
                        }
                    if (!match)
                        continue;
                    string file = Safe(tex.name) + "_" + tex.width + "x" + tex.height + ".png";
                    string path = Path.Combine(Folder, file);
                    try
                    {
                        byte[] png = Readable(tex).EncodeToPNG();
                        File.WriteAllBytes(path, png);
                        dumped[tex] = file;
                        written++;
                    }
                    catch (Exception e)
                    {
                        skipped++;
                        ChaosDepthsPlugin.Log($"Debug: couldn't dump {tex.name} ({e.Message})");
                    }
                }
                // The sprites cut from the dumped sheets: name, sheet, rect (x y w h, y from the
                // bottom as Unity counts), pivot and pixels per unit.
                foreach (Sprite s in Resources.FindObjectsOfTypeAll<Sprite>())
                {
                    try
                    {
                        string file;
                        if (s == null || s.texture == null || !dumped.TryGetValue(s.texture, out file))
                            continue;
                        Rect r = s.textureRect;
                        index.AppendLine($"{s.name}\t{file}\t{r.x:0}\t{r.y:0}\t{r.width:0}\t{r.height:0}\tpivot {s.pivot.x:0.#},{s.pivot.y:0.#}\tppu {s.pixelsPerUnit:0.#}");
                    }
                    catch
                    {
                    }
                }
                File.WriteAllText(Path.Combine(Folder, "sprites.txt"), "sprite\tsheet\tx\ty\tw\th\tpivot\tppu\n" + index);
                ChaosDepthsPlugin.Log($"Debug: dumped {written} texture(s) matching [{string.Join(", ", terms.ToArray())}] to {Folder} ({skipped} couldn't be read); sprites.txt lists the sprites on them");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"texture dump: {e.Message}");
            }
        }

        // A readable copy of a texture, via a render texture when the original isn't readable.
        private static Texture2D Readable(Texture2D tex)
        {
            try
            {
                tex.GetPixel(0, 0);
                return tex;
            }
            catch
            {
            }
            RenderTexture rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            RenderTexture previous = RenderTexture.active;
            try
            {
                Graphics.Blit(tex, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                copy.Apply();
                return copy;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static string Safe(string name)
        {
            var sb = new StringBuilder();
            foreach (char c in name)
                sb.Append(Array.IndexOf(Path.GetInvalidFileNameChars(), c) >= 0 ? '_' : c);
            return sb.ToString();
        }
    }
}
