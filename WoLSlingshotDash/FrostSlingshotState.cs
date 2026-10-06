using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Frost Slingshot (Water, the game's frost element): a charged launch throws an ice feint (the game's IceDecoy, which
    // enemies go after) out along your aim while you stay put. When it lands you swap places: you
    // appear where it landed, and it appears where you stood and bursts with a small Frost Nova
    // that freezes enemies around it, then lingers to draw them in. Further with more charge.
    public class FrostSlingshotState : ChargedDashState
    {
        public new static string staticID = "FrostSlingshot";

        private const float FlightTime = 0.3f;
        private const float MinDistance = 3f;
        private const float ExtraDistance = 6f; // added at full charge
        private const float WallMargin = 0.75f;
        private const float DecoyDuration = 2.5f;

        public FrostSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // The feint travels instead of you: no dash boost, and you stand still while throwing.
        protected override bool BoostsDash => false;
        protected override bool HoldsStill => true;

        protected override void OnLaunch(float charge)
        {
            Vector2 direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            Vector2 start = parent.transform.position;
            float distance = MinDistance + ExtraDistance * charge;
            // Stop short of walls so you never swap into one.
            RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, ChaosCollisions.layerAllWallAndObst);
            if (hit.collider != null)
                distance = Mathf.Max(0f, hit.distance - WallMargin);

            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 1.5f, false);
            GameObject decoy = SpawnDecoy(start);
            // The dash state ends before the feint lands, so the throw runs on the plugin.
            SlingshotDashPlugin.Run(Feint(parent, decoy, start, start + direction * distance, charge,
                parent.skillCategory, skillID));
        }

        private GameObject SpawnDecoy(Vector2 position)
        {
            try
            {
                IceDecoy decoy = ChaosInst<IceDecoy>(IceDecoy.Prefab, new Vector2?(position), null, null);
                if (decoy == null)
                    return null;
                decoy.parentEnt = parent;
                decoy.duration = DecoyDuration;
                return decoy.gameObject;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Ice feint unavailable: {e.Message}");
                return null;
            }
        }

        private static IEnumerator Feint(Player player, GameObject decoy, Vector2 start, Vector2 end, float charge,
            string skillCategory, string id)
        {
            float t = 0f;
            float nextDust = 0f;
            while (t < FlightTime)
            {
                t += Time.deltaTime;
                Vector2 position = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / FlightTime));
                if (decoy != null)
                    decoy.transform.position = position;
                if (t >= nextDust)
                {
                    nextDust = t + 0.05f;
                    PoolManager.GetPoolItem<DustEmitter>().EmitCircle(6, 0.4f, -3f, -1f, new Vector3?(position), null);
                }
                yield return null;
            }

            if (player == null || !player.gameObject.activeInHierarchy)
                yield break;

            // Swap: you to where the feint landed, the feint to where you stood.
            player.transform.position = end;
            if (player.rigidbody2D != null)
            {
                player.rigidbody2D.position = end;
                player.rigidbody2D.velocity = Vector2.zero;
            }
            if (decoy != null)
                decoy.transform.position = start;

            FrostNova.CreateFrostNova(start, 1.75f + charge, skillCategory, id, 1);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(40, 1.2f, -6f, -1f, new Vector3?(end), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(end), null, 24f, -1f, 1.7f, false);
        }
    }
}
