using UnityEngine;

namespace WoLCustomPaintings
{
    // Puts the framed picture on one painting. It keeps re-applying while the painting still
    // shows its original sprite, and steps aside once the game changes it (e.g. when it breaks).
    public class PaintingSwap : MonoBehaviour
    {
        public Texture2D picture;

        private SpriteRenderer target;
        private Sprite original;
        private Sprite replacement;

        private void Start()
        {
            target = MainRenderer();
            if (target == null || picture == null)
            {
                enabled = false;
                return;
            }
            original = target.sprite;
            replacement = PaintingBuilder.Build(original, picture);
            if (replacement == null)
                enabled = false;
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                enabled = false;
                return;
            }
            if (target.sprite == original)
                target.sprite = replacement;
            else if (target.sprite != replacement)
                enabled = false;
        }

        // The painting's biggest sprite (not a shadow or a small decoration on it).
        private SpriteRenderer MainRenderer()
        {
            SpriteRenderer best = null;
            float bestArea = 0f;
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                    continue;
                Vector2 size = renderer.sprite.rect.size;
                float area = size.x * size.y;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = renderer;
                }
            }
            return best;
        }
    }
}
