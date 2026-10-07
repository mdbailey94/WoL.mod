using UnityEngine;

namespace WoLCustomPaintings
{
    // Puts the framed picture on one painting. The game can swap the painting's sprite (an idle
    // animation, the shake when it's hit), so any intact-looking sprite is replaced each frame,
    // until the painting breaks: then the game's broken painting shows as usual.
    //
    // The game's breakable paintings are drawn in layers: a faint shadow (a white square), the
    // frame, and the artwork ("DestructibleSprite") on top. The picture replaces the artwork,
    // inside the game's own frame.
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
        private bool isArt;
        private static bool loggedLayers;

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
                isArt = target.name == ArtLayer;
                LogLayers();
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
                Sprite built = isArt
                    ? PaintingBuilder.BuildArt(target, Frame(), picture, CustomPaintingsPlugin.Detail)
                    : PaintingBuilder.Build(current, picture);
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

        // The layers of the first painting, once, to show how they're built.
        private void LogLayers()
        {
            if (loggedLayers)
                return;
            loggedLayers = true;
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
                CustomPaintingsPlugin.Log($"  layer '{renderer.name}': sprite '{(renderer.sprite != null ? renderer.sprite.name : "none")}'" +
                    (renderer.sprite != null ? $" {renderer.sprite.rect.width}x{renderer.sprite.rect.height}" : "") +
                    $", order {renderer.sortingLayerName}/{renderer.sortingOrder}" + (renderer == target ? " <- picture goes here" : ""));
        }

        private int Renderers() => GetComponentsInChildren<SpriteRenderer>(true).Length;

        private const string ArtLayer = "DestructibleSprite";

        // The painting's frame layer, if it has one.
        private SpriteRenderer Frame()
        {
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer != target && renderer.sprite != null
                    && renderer.name.IndexOf("Frame", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return renderer;
            }
            return null;
        }

        // The painting's artwork layer if it has one; otherwise its biggest sprite that's showing
        // and isn't a shadow (hidden ones only if nothing else has a sprite yet).
        private SpriteRenderer MainRenderer()
        {
            SpriteRenderer best = null, bestHidden = null;
            float bestArea = 0f, bestHiddenArea = 0f;
            foreach (SpriteRenderer renderer in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (renderer.sprite == null)
                    continue;
                if (renderer.name == ArtLayer)
                    return renderer;
                if (renderer.name.IndexOf("Shadow", System.StringComparison.OrdinalIgnoreCase) >= 0 || renderer.sprite.name == "White")
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
