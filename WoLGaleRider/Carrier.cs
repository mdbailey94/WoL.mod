using System.Collections.Generic;
using UnityEngine;

namespace WoLGaleRider
{
    // Carries enemies along with the wizard (the signature, 0.4.1; Stone Charge's): every frame
    // each carried enemy moves by as much as the wizard did and is otherwise held still, so it's
    // swept along in the storm ball until the slam lands on it. One that falls too far behind (a
    // wall caught it) is let go. Bosses and the floor boss are never carried.
    public class Carrier
    {
        private const int MaxCarried = 8;
        private readonly HashSet<Entity> carried = new HashSet<Entity>();
        private Vector2 last;
        private bool started;

        public int Count => carried.Count;
        public int Grabbed { get; private set; }

        public void Grab(Entity e)
        {
            if (e == null || e is Boss || e is MiniBoss || e.health == null || e.health.CurrentHealthValue <= 0
                || carried.Count >= MaxCarried || !carried.Add(e))
                return;
            Grabbed++;
        }

        public void Move(Vector2 here, Vector2 hold, float keep)
        {
            if (!started)
            {
                last = here;
                started = true;
                return;
            }
            Vector2 delta = here - last;
            last = here;
            if (carried.Count == 0)
                return;
            foreach (Entity e in new List<Entity>(carried))
            {
                if (e == null || !e.gameObject.activeInHierarchy || e.health == null || e.health.CurrentHealthValue <= 0
                    || Vector2.Distance(e.transform.position, hold) > keep)
                {
                    carried.Remove(e);
                    continue;
                }
                e.transform.position += (Vector3)delta;
                if (e.rigidbody2D != null)
                    e.rigidbody2D.velocity = Vector2.zero;
            }
        }

        public void Clear()
        {
            carried.Clear();
            started = false;
            Grabbed = 0;
        }
    }
}
