using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Twin vines using the game's own vine (VinePull, the vine Soaring Ivy and the other vine
    // arcana throw), stretched from the wizard to the target at our range. Their attack uses a
    // harmless skill level (no damage or knockback): they're only the look. Each vine's animation
    // is held on its last part (fully stretched; Vines.HoldFrame) the whole time rather than
    // playing the throw over and over, and stretched to reach the target (VineStretch). If the game retracts them early they're put back; if they
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
                go.AddComponent<VineStretch>().vine = vine;
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

    // Stretches a game vine along its length so its far end reaches its target (the enemy, or
    // the spot on the wall or ground). Runs after the vine's animation each frame, so it works
    // whether or not the animation sets the vine's scale itself.
    public class VineStretch : MonoBehaviour
    {
        public VinePull vine;
        private Vector3 applied;
        private Vector3 baseScale;
        private bool hasBase;
        private SpriteRenderer[] sprites;
        private static bool loggedNoSprites;
        private static bool loggedStretch;

        private void LateUpdate()
        {
            try
            {
                Stretch();
            }
            catch
            {
            }
        }

        private void Stretch()
        {
            if (vine == null || vine.destroyQueued)
                return;
            Vector3 targetPos = vine.targetTrans != null ? vine.targetTrans.position : vine.targetPos;
            Vector2 toTarget = targetPos - transform.position;
            float distance = toTarget.magnitude;
            if (distance < 0.05f)
                return;

            // Something else (the animation) set the scale since we last did: that's the new base.
            if (!hasBase || transform.localScale != applied)
            {
                baseScale = transform.localScale;
                hasBase = true;
            }

            // The vine's length runs along whichever of its own axes points most at the target.
            Vector3 local = transform.InverseTransformDirection(toTarget.normalized);
            bool alongX = Mathf.Abs(local.x) >= Mathf.Abs(local.y);
            float sign = Mathf.Sign(alongX ? local.x : local.y);

            // From the art's reach in the vine's own units to world units at the unstretched size.
            float reach = Reach(alongX, sign);
            if (reach <= 0.01f)
                return;
            float baseAxis = alongX ? baseScale.x : baseScale.y;
            float current = alongX ? transform.localScale.x : transform.localScale.y;
            float lossy = alongX ? transform.lossyScale.x : transform.lossyScale.y;
            if (Mathf.Abs(current) < 0.0001f)
                return;
            float naturalLength = reach * Mathf.Abs(lossy * baseAxis / current);
            float factor = Mathf.Clamp(distance / Mathf.Max(0.01f, naturalLength), 0.2f, 20f);

            Vector3 scale = baseScale;
            if (alongX)
                scale.x = baseScale.x * factor;
            else
                scale.y = baseScale.y * factor;
            transform.localScale = scale;
            applied = scale;
            if (!loggedStretch)
            {
                loggedStretch = true;
                SlingshotDashPlugin.Log($"Vines: stretching the game's vine from {naturalLength:0.0} to {distance:0.0} " +
                    $"(along its {(alongX ? "x" : "y")})");
            }
        }

        // How far the vine's art reaches from its pivot toward the target, in the vine's own
        // units (before its scale).
        private float Reach(bool alongX, float sign)
        {
            if (sprites == null)
                sprites = GetComponentsInChildren<SpriteRenderer>(true);
            float reach = 0f;
            bool any = false;
            foreach (SpriteRenderer sr in sprites)
            {
                if (sr == null || sr.sprite == null || !sr.enabled || !sr.gameObject.activeInHierarchy)
                    continue;
                Bounds b = sr.sprite.bounds;
                for (int c = 0; c < 4; c++)
                {
                    var corner = new Vector3(c % 2 == 0 ? b.min.x : b.max.x, c < 2 ? b.min.y : b.max.y, 0f);
                    Vector3 p = transform.InverseTransformPoint(sr.transform.TransformPoint(corner));
                    float along = (alongX ? p.x : p.y) * sign;
                    reach = Mathf.Max(reach, along);
                    any = true;
                }
            }
            if (!any && !loggedNoSprites)
            {
                loggedNoSprites = true;
                SlingshotDashPlugin.Log("Vines: couldn't measure the game's vine to stretch it");
            }
            return reach;
        }
    }
}
