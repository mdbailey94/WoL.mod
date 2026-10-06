using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Twin vines using the game's own vine (VinePull, the vine Soaring Ivy and the other vine
    // arcana throw), stretched from the wizard to the target at our range. Their attack uses a
    // harmless skill level (no damage or knockback): they're only the look. Each vine's animation
    // is held on its last part (fully stretched; Vines.HoldFrame) the whole time rather than
    // playing the throw over and over. If the game retracts them early they're put back; if they
    // keep disappearing, Broken tells the caller to fall back to drawn vines.
    public class GameVines
    {
        private static readonly Vector2[] Offsets = { new Vector2(0f, 0.2f), new Vector2(0f, -0.1f) };
        private const int HarmlessLevel = 3;
        private const int MaxRespawns = 6;

        private readonly VinePull[] vines = new VinePull[2];
        // The animation state each vine is held in (0 = not yet), and the frame it was made.
        private readonly int[] heldState = new int[2];
        private readonly int[] spawnFrame = new int[2];
        private readonly Player player;
        private readonly string skillCategory;
        private readonly string skillID;
        private GameObject anchor;
        private int respawns;
        private static bool logged;

        public bool Broken => respawns > MaxRespawns;

        private GameVines(Player player, string skillCategory, string skillID)
        {
            this.player = player;
            this.skillCategory = skillCategory;
            this.skillID = skillID;
        }

        public static GameVines Create(Player player, string skillCategory, string skillID)
        {
            try
            {
                if (VinePull.Prefab == null)
                    return null;
                return new GameVines(player, skillCategory, skillID);
            }
            catch (System.Exception e)
            {
                Log($"Vines: the game's vine is unavailable, drawing our own: {e.Message}");
                return null;
            }
        }

        // Keep both vines stretched from the wizard to the target (an enemy, or a point).
        public void Hold(Transform target, Vector2 point)
        {
            if (Broken)
                return;
            if (target == null)
            {
                if (anchor == null)
                    anchor = new GameObject("SlingshotVineAnchor");
                anchor.transform.position = point;
                target = anchor.transform;
            }
            for (int i = 0; i < vines.Length; i++)
            {
                VinePull vine = vines[i];
                if (vine != null && vine.gameObject.activeInHierarchy && !vine.destroyQueued && vine.targetTrans == target)
                {
                    vine.targetPos = point;
                    vine.maxDist = 99f;
                    HoldLastFrame(i);
                    continue;
                }
                if (vine != null)
                {
                    respawns++;
                    Object.Destroy(vine.gameObject);
                }
                vines[i] = Spawn(target, point, Offsets[i]);
                heldState[i] = 0;
                spawnFrame[i] = Time.frameCount;
                if (vines[i] == null)
                    respawns = MaxRespawns + 1;
            }
            if (Broken)
            {
                Log("Vines: the game's vine keeps retracting, drawing our own instead");
                Remove();
            }
        }

        // Holds the vine's animation on its fully stretched end. The animation it starts takes
        // effect a frame after it's made, so it's read from the next frame on; after that, if the
        // game moves it on to another animation, it's put back.
        private void HoldLastFrame(int i)
        {
            try
            {
                Animator anim = vines[i].anim;
                if (anim == null || Time.frameCount <= spawnFrame[i])
                    return;
                AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
                if (heldState[i] == 0)
                {
                    if (info.length < 0.05f)
                        return;
                    heldState[i] = info.fullPathHash;
                }
                else if (info.fullPathHash == heldState[i] && anim.speed == 0f)
                    return;
                anim.Play(heldState[i], 0, SlingshotDashPlugin.VineHoldFrame);
                anim.speed = 0f;
            }
            catch
            {
            }
        }

        private VinePull Spawn(Transform target, Vector2 point, Vector2 offset)
        {
            try
            {
                GameObject go = Object.Instantiate(VinePull.Prefab, player.transform.position, Quaternion.identity);
                VinePull vine = go.GetComponent<VinePull>();
                if (vine == null)
                {
                    Object.Destroy(go);
                    return null;
                }
                vine.maxDist = 99f;
                vine.Initialize(player.transform, target, point, offset, Vector2.zero, skillCategory, skillID, HarmlessLevel, false);
                vine.maxDist = 99f;
                if (!logged)
                {
                    logged = true;
                    Log("Vines: using the game's vine");
                }
                return vine;
            }
            catch (System.Exception e)
            {
                Log($"Vines: couldn't make the game's vine: {e.Message}");
                return null;
            }
        }

        // Retract (the game's own animation) and clear away.
        public void Remove()
        {
            foreach (VinePull vine in vines)
            {
                if (vine == null)
                    continue;
                try
                {
                    // Let the retract play (the hold had stopped the animation).
                    if (vine.anim != null)
                        vine.anim.speed = 1f;
                    vine.Retract();
                }
                catch
                {
                }
                SlingshotDashPlugin.Run(DestroyLater(vine.gameObject, 0.3f));
            }
            for (int i = 0; i < vines.Length; i++)
                vines[i] = null;
            if (anchor != null)
                SlingshotDashPlugin.Run(DestroyLater(anchor, 0.35f));
            anchor = null;
        }

        private static IEnumerator DestroyLater(GameObject go, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (go != null)
                Object.Destroy(go);
        }

        private static void Log(string message) => SlingshotDashPlugin.Log(message);
    }
}
