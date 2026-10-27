using UnityEngine;

namespace WoLGaleRider
{
    // Lifts the wizard's picture so they sit on top of the wind ball (Thunderhead's Levitator,
    // the same way): the body sprite's local position is raised by `height` after the game has
    // placed it each frame, the shadow stays on the ground, and the picture keeps the draw order
    // it had on the ground so it doesn't sort as if it stood further up the room.
    public class Rider : MonoBehaviour
    {
        public float height;
        private Vector3 applied;
        private Vector3 lastSet;
        private bool active;
        private SpriteRenderer body;
        private int groundOrder;
        private int groundLayer;
        private SpriteRenderer shadow;
        private int shadowOffset;

        public int DrawOrder => shadow != null ? shadow.sortingOrder + shadowOffset : groundOrder;
        public int DrawLayer => groundLayer;

        public static Rider On(Player player)
        {
            try
            {
                if (player == null || player.spriteRenderer == null)
                    return null;
                GameObject body = player.spriteRenderer.gameObject;
                Rider rider = body.GetComponent<Rider>();
                if (rider == null)
                    rider = body.AddComponent<Rider>();
                rider.enabled = true;
                rider.height = 0f;
                rider.body = player.spriteRenderer;
                rider.groundOrder = player.spriteRenderer.sortingOrder;
                rider.groundLayer = player.spriteRenderer.sortingLayerID;
                rider.shadow = player.shadowSprite;
                if (rider.shadow != null)
                    rider.shadowOffset = rider.groundOrder - rider.shadow.sortingOrder;
                return rider;
            }
            catch
            {
                return null;
            }
        }

        private void LateUpdate()
        {
            Vector3 position = transform.localPosition;
            if (active && position == lastSet)
                position -= applied;
            applied = new Vector3(0f, height, 0f);
            lastSet = position + applied;
            transform.localPosition = lastSet;
            active = true;
        }

        private void OnWillRenderObject()
        {
            if (!active || body == null || height <= 0.01f)
                return;
            body.sortingLayerID = groundLayer;
            body.sortingOrder = DrawOrder;
        }

        public void Off()
        {
            if (active && transform.localPosition == lastSet)
                transform.localPosition -= applied;
            applied = Vector3.zero;
            height = 0f;
            active = false;
            enabled = false;
        }
    }
}
