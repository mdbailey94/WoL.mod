using System;
using UnityEngine;

namespace WoLCyclone
{
    // 0.8.0 (#67, Matt): an enemy caught in the twister's core is lifted off the ground and spun
    // round it like a tornado: held on a ring round the twister (following it as it's steered),
    // its picture (the "Sprite" child, as Toss lifts) rising to the spin height and bobbing, and
    // turned to face along its path each half turn so it reads as spinning. When its spin time is
    // up, or the twister ends, it drops and takes the landing's damage, with a puff of dust.
    // Bosses are never caught (the state checks).
    public class Spin : MonoBehaviour
    {
        private const float RiseTime = 0.3f;     // seconds to rise to the spin height
        private const float BobHeight = 0.15f;

        private Entity entity;
        private Player thrower;
        private Transform picture;
        private Vector3 lifted;
        private Vector3 baseScale;
        private Func<Vector2> centerOf;
        private float age, duration, height, radius, angle, turnRate;
        private int damage;
        private string skillCategory, skillID;
        private bool released;

        public static bool Spinning(Entity e) => e != null && e.GetComponent<Spin>() != null;

        public static Spin Catch(Entity e, Player thrower, Func<Vector2> centerOf, float radius, float height,
            float duration, float turnsPerSecond, int damage, string skillID)
        {
            if (e == null || e.health == null || Spinning(e) || Toss.Airborne(e))
                return null;
            Spin s = e.gameObject.AddComponent<Spin>();
            s.entity = e;
            s.thrower = thrower;
            s.picture = e.spriteRenderer != null && e.spriteRenderer.transform != e.transform ? e.spriteRenderer.transform : null;
            s.baseScale = s.picture != null ? s.picture.localScale : Vector3.one;
            s.centerOf = centerOf;
            Vector2 from = (Vector2)e.transform.position - centerOf();
            s.angle = Mathf.Atan2(from.y / 0.6f, from.x);
            s.radius = radius;
            s.height = height;
            s.duration = Mathf.Max(0.3f, duration);
            s.turnRate = turnsPerSecond * 2f * Mathf.PI;
            s.damage = damage;
            s.skillCategory = thrower != null ? thrower.skillCategory : "";
            s.skillID = skillID;
            return s;
        }

        // The twister ended: drop now (the landing still hurts).
        public void Release()
        {
            if (!released)
                Drop(true);
        }

        private void LateUpdate()
        {
            if (released)
                return;
            if (entity == null || entity.health == null || entity.health.CurrentHealthValue <= 0)
            {
                Drop(false);
                return;
            }
            age += Time.deltaTime;
            angle += turnRate * Time.deltaTime;
            Vector2 c;
            try
            {
                c = centerOf();
            }
            catch
            {
                Drop(true);
                return;
            }
            // Round the twister on the game's 3/4-view ring, held there (its own legs and the
            // knockback of the twister's hits would carry it off).
            Vector2 at = c + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f) * radius;
            transform.position = new Vector3(at.x, at.y, transform.position.z);
            if (entity.rigidbody2D != null)
                entity.rigidbody2D.velocity = Vector2.zero;
            if (picture != null)
            {
                picture.localPosition -= lifted;
                float up = height * Mathf.Clamp01(age / RiseTime) + BobHeight * Mathf.Sin(age * 9f);
                // Snapped to the game's pixels so it doesn't shimmer.
                lifted = new Vector3(0f, Mathf.Round(up * 16f) / 16f, 0f);
                picture.localPosition += lifted;
                // Facing along its path: flips each half turn, so it reads as spinning.
                float facing = -Mathf.Sin(angle) >= 0f ? 1f : -1f;
                picture.localScale = new Vector3(Mathf.Abs(baseScale.x) * facing, baseScale.y, baseScale.z);
            }
            if (age >= duration)
                Drop(true);
        }

        private void Drop(bool landed)
        {
            released = true;
            Restore();
            if (landed && entity != null && entity.health != null && entity.health.CurrentHealthValue > 0 && damage > 0)
            {
                try
                {
                    var info = new AttackInfo
                    {
                        damage = damage,
                        entity = thrower,
                        // As Toss: a twister hit doesn't refill the signature gauge on its own landing.
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
                    PoolManager.GetPoolItem<DustEmitter>().EmitCircle(12, 0.5f, -3f, -1f, new Vector3?(transform.position), null);
                }
                catch (Exception e)
                {
                    CyclonePlugin.Log($"Cyclone: a spun enemy's landing failed: {e.Message}");
                }
            }
            Destroy(this);
        }

        private void Restore()
        {
            if (picture != null)
            {
                picture.localPosition -= lifted;
                picture.localScale = baseScale;
            }
            lifted = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (!released)
                Restore();
        }
    }
}
