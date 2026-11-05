using UnityEngine;

namespace WoLSlingshotDash
{
    // Game textures aren't readable from code, so copy a sprite's pixels through a render texture.
    public static class SpriteReader
    {
        // A whole texture (e.g. a particle material's).
        public static Color32[] ReadTexture(Texture source)
        {
            if (source == null)
                return null;
            return ReadRegion(source, new Rect(0, 0, source.width, source.height));
        }

        public static Color32[] Read(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return null;
            return ReadRegion(sprite.texture, sprite.textureRect);
        }

        private static Color32[] ReadRegion(Texture source, Rect region)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = null;
            try
            {
                int w = Mathf.RoundToInt(region.width), h = Mathf.RoundToInt(region.height);
                if (w <= 0 || h <= 0)
                    return null;
                rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                var copy = new Texture2D(w, h, TextureFormat.RGBA32, false);
                copy.ReadPixels(region, 0, 0);
                copy.Apply();
                Color32[] result = copy.GetPixels32();
                Object.Destroy(copy);
                return result;
            }
            catch
            {
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }
    }
}
