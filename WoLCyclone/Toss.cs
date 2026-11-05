using UnityEngine;

namespace WoLCyclone
{
    // The signature twister throws an enemy up into the air and drops it: its picture (the
    // "Sprite" child the game draws it with) rises and falls in an arc while the enemy itself is
    // held where it was caught, its shadow and hurt box staying on the ground; when it lands it
    // takes the fall's damage, with a puff of dust. Bosses aren't thrown (the state checks).
    public class Toss : MonoBehaviour
    {
        private Entity entity;
        private Player thrower;
        private Transform picture;
        private Vector3 lifted;   // the offset this toss put on the picture
        private Vector3 holdAt;
        private float age, duration, height;
        private int damage;
        private string skillCategory, skillID;

        public static bool Airborne(Entity e) => e != null && e.GetComponent<Toss>() != null;

        public static void Launch(Entity e, Player thrower, float height, float duration, int damage, string skillID)
        {
            if (e == null || e.health == null || Airborne(e))
                return;
            Toss t = e.gameObject.AddComponent<Toss>();
            t.entity = e;
            t.thrower = thrower;
            // Only a picture of its own can be lifted; a body drawn on the enemy's root object
            // isn't moved (it still takes the landing).
            t.picture = e.spriteRenderer != null && e.spriteRenderer.transform != e.transform ? e.spriteRenderer.transform : null;
            t.holdAt = e.transform.position;
            t.height = height;
            t.duration = Mathf.Max(0.2f, duration);
            t.damage = damage;
            t.skillCategory = thrower != null ? thrower.skillCategory : "";
            t.skillID = skillID;
        }

        private void LateUpdate()
        {
            if (entity == null || entity.health == null || entity.health.CurrentHealthValue <= 0)
            {
                Drop(false);
                return;
            }
            age += Time.deltaTime;
            float t = Mathf.Clamp01(age / duration);
            // Held in place while it's up (the twister's pull and its own legs would carry it off).
            transform.position = new Vector3(holdAt.x, holdAt.y, transform.position.z);
            if (entity.rigidbody2D != null)
                entity.rigidbody2D.velocity = Vector2.zero;
            if (picture != null)
            {
                picture.localPosition -= lifted;
                // Snapped to the game's pixels so the thrown sprite doesn't shimmer.
                lifted = new Vector3(0f, Mathf.Round(4f * height * t * (1f - t) * 16f) / 16f, 0f);
                picture.localPosition += lifted;
            }
            if (t >= 1f)
                Drop(true);
        }

        private void Drop(bool landed)
        {
            if (picture != null)
                picture.localPosition -= lifted;
            lifted = Vector3.zero;
            if (landed && entity != null && entity.health != null && entity.health.CurrentHealthValue > 0)
            {
                try
                {
                    var info = new AttackInfo
                    {
                        damage = damage,
                        entity = thrower,
                        // A signature hit: it doesn't refill the signature gauge.
                        isUltimate = true,
                        skillCategory = skillCategory,
                        skillID = skillID,
                        elementType = ElementType.Air,
                        showDamageNumber = true,
                        showDamageEffect = true,
                        attackerPosition = transform.position,
                        targetPosition = transform.position
                    };
                    entity.health.TakeDamage(info, thrower);
                    // From up to 11 units: a heavier crash than the old knee-high drop.
                    PoolManager.GetPoolItem<DustEmitter>().EmitCircle(24, 0.8f, -4f, -1f, new Vector3?(transform.position), null);
                    CameraController.ShakeCamera(0.35f, false);
                }
                catch (System.Exception e)
                {
                    CyclonePlugin.Log($"Cyclone: a tossed enemy's landing failed: {e.Message}");
                }
            }
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (picture != null && lifted != Vector3.zero)
                picture.localPosition -= lifted;
        }
    }
}
