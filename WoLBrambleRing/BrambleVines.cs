using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace WoLBrambleRing
{
    // The game's own vine traps (VineTrap: the snapping vines Rippling Vines and the Root Shield
    // relic raise, each with its own hit box), knitted into rows around a point. A vine that
    // catches an enemy grabs it and holds it where it stands (the game's own grab, which bosses
    // shrug off) until the vines are cleared away.
    public class BrambleVines
    {
        // Vines aren't raised inside walls, obstacles or pits (the check Root Shield uses).
        private const float WallCheckRadius = 0.25f;
        // How long a cleared vine's "Dead" animation gets before the object is removed.
        private const float UnravelTime = 1.5f;
        // Leaves each vine throws up as it bursts out of the ground.
        private const int LeavesPerVine = 2;

        private readonly Player player;
        private readonly string skillID;
        private readonly int level;
        private readonly bool ultimate;
        private readonly System.Action<Entity> onGrab;
        private readonly List<VineTrap> vines = new List<VineTrap>();
        private static bool loggedHitBox;

        public int Count => vines.Count;

        // onGrab is called with each entity a vine's hit box catches (just before the vine hits it).
        public BrambleVines(Player player, string skillID, int level, bool ultimate, System.Action<Entity> onGrab)
        {
            this.player = player;
            this.skillID = skillID;
            this.level = level;
            this.ultimate = ultimate;
            this.onGrab = onGrab;
        }

        // The spots for one row of vines around the center, in order round the ring, skipping any
        // inside walls or over pits (the number skipped is returned). offset puts this row's vines
        // between the previous row's.
        public static int RowPositions(Vector2 center, float radius, float spin, float spacing, int minCount,
            bool offset, List<Vector2> positions)
        {
            int count = Mathf.Max(minCount, Mathf.RoundToInt(2f * Mathf.PI * radius / spacing));
            float step = 360f / count;
            int blocked = 0;
            for (int i = 0; i < count; i++)
            {
                float angle = (spin + step * (i + (offset ? 0.5f : 0f))) * Mathf.Deg2Rad;
                Vector2 position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                if (Globals.CheckCircle(position, WallCheckRadius, ChaosCollisions.layerAll))
                    positions.Add(position);
                else
                    blocked++;
            }
            return blocked;
        }

        // Raises one vine. Null if the game's vine couldn't be made (logged once).
        public VineTrap Raise(Vector2 position)
        {
            GameObject go = null;
            try
            {
                go = Object.Instantiate(VineTrap.Prefab, position, Quaternion.identity);
                VineTrap vine = go.GetComponent<VineTrap>();
                if (vine == null || vine.attack == null)
                {
                    Object.Destroy(go);
                    BrambleRingState.LogOnce("vine", "the game's vine trap has no attack, raising no vines");
                    return null;
                }
                vine.attack.SetAttackInfo(player.skillCategory, skillID, level, ultimate);
                // Runs before the vine's own grab (its handler is added in its Start).
                vine.attack.entityCollisionEventHandlers += entity =>
                {
                    if (entity != null)
                        onGrab(entity);
                };
                vines.Add(vine);
                LogHitBox(vine);
                BrambleRingState.Effect("leaves", () => PoolManager.GetPoolItem<LeafEmitter>().EmitSingle(LeavesPerVine,
                    position + new Vector2(0f, 0.5f), null, null, 0f, null, LeafEmitter.LeafColor.Green));
                return vine;
            }
            catch (System.Exception e)
            {
                if (go != null)
                    Object.Destroy(go);
                BrambleRingState.LogOnce("vine", $"couldn't raise the game's vine trap: {e.Message}");
                return null;
            }
        }

        // Clears the vines away in the order they were raised (each withers with the game's own
        // animation and sound), stagger seconds apart.
        public IEnumerator Unravel(float stagger)
        {
            foreach (VineTrap vine in vines)
            {
                if (vine != null)
                {
                    try
                    {
                        vine.DestroyVine();
                    }
                    catch (System.Exception e)
                    {
                        BrambleRingState.LogOnce("unravel", $"couldn't wither a vine: {e.Message}");
                    }
                    Object.Destroy(vine.gameObject, UnravelTime);
                    if (stagger > 0f)
                        yield return new WaitForSeconds(stagger);
                }
            }
            vines.Clear();
        }

        // The size of a vine's hit box, once, for tuning the row spacing.
        private static void LogHitBox(VineTrap vine)
        {
            if (loggedHitBox)
                return;
            loggedHitBox = true;
            Collider2D box = vine.attack.collider;
            if (box == null)
            {
                BrambleRingState.Log("vine has no hit box");
                return;
            }
            Vector3 scale = box.transform.lossyScale;
            string size;
            if (box is CircleCollider2D circle)
                size = $"circle, radius {circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)):0.00}";
            else if (box is BoxCollider2D rect)
                size = $"box, {rect.size.x * Mathf.Abs(scale.x):0.00} x {rect.size.y * Mathf.Abs(scale.y):0.00}";
            else
                size = $"{box.GetType().Name}, {box.bounds.size.x:0.00} x {box.bounds.size.y:0.00}";
            BrambleRingState.Log($"vine hit box is a {size} on layer {LayerMask.LayerToName(box.gameObject.layer)}" +
                $"{(box.enabled ? "" : " (off until its animation turns it on)")}");
        }
    }
}
