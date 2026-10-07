using UnityEngine;

namespace WoLCustomPaintings
{
    // Puts the framed picture on one painting. The game can swap the painting's sprite (an idle
    // animation, the shake when it's hit), so any intact-looking sprite is replaced each frame,
    // until the painting breaks: then the game's broken painting shows as usual.
    //
    // A painting can be drawn in layers (a frame, with the artwork as its own sprite on top), so
    // any other sprite of the painting drawn in front of the one we replace is hidden while the
    // picture is up, or it would cover the picture.
    public class PaintingSwap : MonoBehaviour
    {
        public Texture2D picture;

        private Destructible destructible;
        private SpriteRenderer target;
        private Vector2 intactSize;
        private Sprite lastOriginal;
        private Sprite replacement;
        private bool logged;
        private int swaps;
        private float checkAt = -1f;

        private float startTime;
        private readonly System.Collections.Generic.List<SpriteRenderer> covers = new System.Collections.Generic.List<SpriteRenderer>();

        private void Start()
        {
            startTime = Time.time;
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
                if (swaps == 0)
                    CustomPaintingsPlugin.Log($"Painting '{name}' counts as broken already, leaving it alone");
                if (target != null && target.sprite == replacement && lastOriginal != null)
                    target.sprite = lastOriginal;
                foreach (SpriteRenderer cover in covers)
                {
                    if (cover != null)
                        cover.enabled = true;
                }
                covers.Clear();
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
                FindCovers();
                CustomPaintingsPlugin.Log($"Painting '{name}': showing a picture on '{target.name}' (sprite '{target.sprite.name}', " +
                    $"{intactSize.x}x{intactSize.y}, {Renderers()} sprite renderer(s), visible={target.enabled && target.gameObject.activeInHierarchy})");
            }
            // A second after the picture first goes up: say whether it's still up, or the game keeps
            // putting its own sprite back.
            if (checkAt > 0f && Time.time >= checkAt)
            {
                checkAt = 0f;
                CustomPaintingsPlugin.Log($"Painting '{name}': picture " +
                    (target.sprite == replacement ? "up" : "replaced by the game") +
                    (swaps > 5 ? $" (set {swaps} times: the game keeps changing the sprite back)" : "") +
                    $", visible={target.enabled && target.gameObject.activeInHierarchy}");
            }
            // Keep anything that would cover the picture hidden (the game may switch it back on).
            if (replacement != null && target.sprite == replacement)
            {
                foreach (SpriteRenderer cover in covers)
                {
                    if (cover != null && cover.enabled)
                        cover.enabled = false;
                }
            }
            Sprite current = target.sprite;
            if (current == null || current == replacement)
                return;
            // Only the intact painting's frames (the broken one is a different size).
            if (current.rect.size != intactSize)
            {
                if (!logged)
                {
                    logged = true;
                    CustomPaintingsPlugin.Log($"Painting '{name}' changed to '{current.name}' ({current.rect.width}x{current.rect.height}), not swapping that");
                }
                return;
            }
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
            swaps++;
            if (checkAt < 0f)
                checkAt = Time.time + 1f;
        }

        // Every other sprite of the painting that's showing and drawn in front of the target (same
        // sorting layer, same or higher order). All of them are logged, to see how it's built.
        private void FindCovers()
        {
            covers.Clear();
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                bool showing = renderer.enabled && renderer.gameObject.activeInHierarchy;
                bool inFront = renderer != target && showing && renderer.sprite != null
                    && renderer.sortingLayerID == target.sortingLayerID && renderer.sortingOrder >= target.sortingOrder;
                CustomPaintingsPlugin.Log($"  layer '{renderer.name}': sprite '{(renderer.sprite != null ? renderer.sprite.name : "none")}'" +
                    (renderer.sprite != null ? $" {renderer.sprite.rect.width}x{renderer.sprite.rect.height}" : "") +
                    $", order {renderer.sortingLayerName}/{renderer.sortingOrder}, showing={showing}" +
                    (renderer == target ? " <- picture goes here" : inFront ? " <- in front, hidden while the picture is up" : ""));
                if (inFront)
                    covers.Add(renderer);
            }
        }

        private int Renderers() => GetComponentsInChildren<SpriteRenderer>(true).Length;

        // The painting's biggest sprite that's actually showing (not a shadow, a small decoration, or
        // a hidden one such as its broken look). Hidden ones only if nothing else has a sprite yet.
        private SpriteRenderer MainRenderer()
        {
            SpriteRenderer best = null, bestHidden = null;
            float bestArea = 0f, bestHiddenArea = 0f;
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                    continue;
                Vector2 size = renderer.sprite.rect.size;
                float area = size.x * size.y;
                if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                {
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = renderer;
                    }
                }
                else if (area > bestHiddenArea)
                {
                    bestHiddenArea = area;
                    bestHidden = renderer;
                }
            }
            return best != null ? best : (Time.time - startTime > 2f ? bestHidden : null);
        }
    }
}
