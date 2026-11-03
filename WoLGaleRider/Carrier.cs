using System.Collections.Generic;
using UnityEngine;

namespace WoLGaleRider
{
    // The signature's storm ball tosses the enemies it catches around inside it (0.6.0, Matt:
    // "should pick up enemies and toss them around inside the ball"; 0.4.1 carried them rigidly).
    // Each one is lifted off the ground (its picture, the "Sprite" child, rises and bobs) and
    // whirled round the ball's middle on the game's 3/4-view ring: every few tenths of a second it's
    // tossed to a new distance and spin speed, sometimes the other way round, and turned to face
    // along its path so it reads as tumbling. It's held there (its own legs and the ball's knockback
    // would carry it off) until the slam lands on it or it dies; then it's set down as it was.
    // Bosses and the floor boss are never caught. The pattern follows Cyclone 0.8.0's Spin.
    public class Carrier
    {
        private const int MaxCarried = 8;
        private const float MinRing = 0.3f, MaxRing = 0.85f;      // of the ball's radius
        private const float MinTurn = 1.2f, MaxTurn = 2.6f;        // turns a second
        private const float MinToss = 0.25f, MaxToss = 0.6f;       // seconds between tosses
        private const float RingEase = 6f;                         // how fast it moves to a new distance
        private const float LiftLow = 0.35f, LiftHigh = 1.1f;      // picture height above the floor
        private const float RiseTime = 0.25f;

        private class Tossed
        {
            public Entity entity;
            public Transform picture;
            public Vector3 lifted, baseScale;
            public float angle, ring, ringTarget, turn, nextToss, age, liftTarget, lift;
        }

        private readonly Dictionary<Entity, Tossed> carried = new Dictionary<Entity, Tossed>();

        public int Count => carried.Count;
        public int Grabbed { get; private set; }

        public void Grab(Entity e)
        {
            if (e == null || e is Boss || e is MiniBoss || e.health == null || e.health.CurrentHealthValue <= 0
                || carried.Count >= MaxCarried || carried.ContainsKey(e))
                return;
            var t = new Tossed
            {
                entity = e,
                picture = e.spriteRenderer != null && e.spriteRenderer.transform != e.transform ? e.spriteRenderer.transform : null,
                angle = Random.value * Mathf.PI * 2f,
                ring = 0.6f,
            };
            t.baseScale = t.picture != null ? t.picture.localScale : Vector3.one;
            Toss(t);
            carried[e] = t;
            Grabbed++;
        }

        // A new throw: another distance, speed and height, now and then the other way round.
        private static void Toss(Tossed t)
        {
            t.ringTarget = Random.Range(MinRing, MaxRing);
            float speed = Random.Range(MinTurn, MaxTurn) * 2f * Mathf.PI;
            t.turn = Random.value < 0.25f ? -Mathf.Sign(t.turn == 0f ? 1f : t.turn) * speed : Mathf.Sign(t.turn == 0f ? 1f : t.turn) * speed;
            t.liftTarget = Random.Range(LiftLow, LiftHigh);
            t.nextToss = t.age + Random.Range(MinToss, MaxToss);
        }

        // Every frame while the ball rolls (and through the leap): whirl each caught enemy round
        // the ball's middle, inside its radius.
        public void Move(Vector2 center, float ballRadius)
        {
            if (carried.Count == 0)
                return;
            float dt = Time.deltaTime;
            foreach (Tossed t in new List<Tossed>(carried.Values))
            {
                Entity e = t.entity;
                if (e == null || !e.gameObject.activeInHierarchy || e.health == null || e.health.CurrentHealthValue <= 0)
                {
                    Let(t);
                    continue;
                }
                t.age += dt;
                if (t.age >= t.nextToss)
                    Toss(t);
                t.angle += t.turn * dt;
                t.ring = Mathf.Lerp(t.ring, t.ringTarget, 1f - Mathf.Exp(-RingEase * dt));
                float r = Mathf.Max(0.2f, ballRadius) * t.ring;
                Vector2 at = center + new Vector2(Mathf.Cos(t.angle), Mathf.Sin(t.angle) * 0.6f) * r;
                e.transform.position = new Vector3(at.x, at.y, e.transform.position.z);
                if (e.rigidbody2D != null)
                {
                    e.rigidbody2D.position = at;
                    e.rigidbody2D.velocity = Vector2.zero;
                }
                if (t.picture != null)
                {
                    t.picture.localPosition -= t.lifted;
                    t.lift = Mathf.Lerp(t.lift, t.liftTarget, 1f - Mathf.Exp(-8f * dt));
                    float up = t.lift * Mathf.Clamp01(t.age / RiseTime) + 0.12f * Mathf.Sin(t.age * 11f);
                    t.lifted = new Vector3(0f, Mathf.Round(up * 16f) / 16f, 0f);   // on the game's pixels
                    t.picture.localPosition += t.lifted;
                    float facing = Mathf.Sign(-Mathf.Sin(t.angle) * t.turn);
                    t.picture.localScale = new Vector3(Mathf.Abs(t.baseScale.x) * (facing == 0f ? 1f : facing), t.baseScale.y, t.baseScale.z);
                }
            }
        }

        // Set down as it was (its picture back on the ground, facing as before).
        private void Let(Tossed t)
        {
            if (t.picture != null)
            {
                t.picture.localPosition -= t.lifted;
                t.picture.localScale = t.baseScale;
                t.lifted = Vector3.zero;
            }
            carried.Remove(t.entity);
        }

        public void Clear()
        {
            foreach (Tossed t in new List<Tossed>(carried.Values))
                Let(t);
            carried.Clear();
            Grabbed = 0;
        }
    }
}
