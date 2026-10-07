using UnityEngine;

namespace WoLCustomPaintings
{
    // Puts the framed picture on one painting. The game can swap the painting's sprite (an idle
    // animation, the shake when it's hit), so any intact-looking sprite is replaced each frame,
    // until the painting breaks: then the game's broken painting shows as usual.
    public class PaintingSwap : MonoBehaviour
    {
        public Texture2D picture;

        private Destructible destructible;
        private SpriteRenderer target;
        private Vector2 intactSize;
        private Sprite lastOriginal;
        private Sprite replacement;
        private bool logged;

        private void Start()
        {
            destructible = GetComponent<Destructible>();
        }

        private void LateUpdate()
        {
            if (picture == null)
            {
                enabled = false;
                return;
            }
            if (destructible != null && destructible.destroyed)
            {
                if (target != null && target.sprite == replacement && lastOriginal != null)
                    target.sprite = lastOriginal;
                enabled = false;
                return;
            }
            if (target == null)
            {
                // The sprite may only be set a moment after the painting appears.
                target = MainRenderer();
                if (target == null)
                    return;
                intactSize = target.sprite.rect.size;
            }
            Sprite current = target.sprite;
            if (current == null || current == replacement)
                return;
            // Only the intact painting's frames (the broken one is a different size).
            if (current.rect.size != intactSize)
                return;
            if (current != lastOriginal)
            {
                Sprite built = PaintingBuilder.Build(current, picture);
                if (built == null)
                {
                    if (!logged)
                    {
                        logged = true;
                        CustomPaintingsPlugin.Log($"Painting '{current.name}' is too small to frame a picture");
                    }
                    enabled = false;
                    return;
                }
                lastOriginal = current;
                replacement = built;
            }
            target.sprite = replacement;
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
