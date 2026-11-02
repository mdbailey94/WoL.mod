using System.IO;
using System.Reflection;
using UnityEngine;

namespace WoLTrailblazer
{
    // WoL Art's white-hot fire for a maxed-out trail (task #23): the flame tongue, the size of the
    // game's MeteorStrike tiles with the same pivot at its base, and a small ember. Null if a sheet
    // can't be loaded (logged once), and the trail keeps its orange fire.
    internal static class WhiteHotArt
    {
        private const int TongueWidth = 25, TongueHeight = 60, TongueFrames = 6;
        private const int EmberSize = 8, EmberFrames = 4;
        private static readonly Vector2 TonguePivot = new Vector2(0.5f, 2.5f / 60f);

        private static Sprite[] tongue, ember;
        private static bool failed;

        public static Sprite[] Tongue(float ppu) =>
            Cut(ref tongue, "trailblazer_max_tongue.png", TongueWidth, TongueHeight, TongueFrames, TonguePivot, ppu);

        public static Sprite[] Ember(float ppu) =>
            Cut(ref ember, "trailblazer_max_ember.png", EmberSize, EmberSize, EmberFrames, new Vector2(0.5f, 0.5f), ppu);

        private static Sprite[] Cut(ref Sprite[] frames, string file, int width, int height, int count, Vector2 pivot, float ppu)
        {
            if (failed)
                return null;
            if (PixelCanvas.Alive(frames))
                return frames;
            try
            {
                string resource = $"{nameof(WoLTrailblazer)}.Assets.{file}";
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
                {
                    if (stream == null)
                        throw new FileNotFoundException(resource);
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                    texture.LoadImage(data);
                    var cut = new Sprite[count];
                    for (int i = 0; i < count; i++)
                        cut[i] = Sprite.Create(texture, new Rect(i * width, 0, width, height), pivot, ppu);
                    frames = cut;
                }
            }
            catch (System.Exception e)
            {
                failed = true;
                TrailblazerPlugin.Log($"Trailblazer: the white-hot fire isn't available, keeping the orange fire: {e.Message}");
                return null;
            }
            return frames;
        }
    }
}
