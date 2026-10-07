using System.Collections;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLSlingshotDash
{
    // The old dash version of Feint Swap, kept (hidden from the shop and spellbook) so a save that
    // has it in the dash slot still loads; FeintSwapState is the standard arcana that replaced it.
    //
    // Feint Swap (Water, the game's frost element): a charged launch throws an ice feint
    // (the game's IceDecoy, which enemies go after) out along your aim. It hovers there while you
    // keep moving; then, or as soon as you press dash again, you swap places with it. Both spots
    // burst with a small Frost Nova that freezes enemies, and the feint lingers where you were to
    // keep drawing them. A tap is just a plain dash. Holding 0.2 s to 1 s scales both the throw
    // distance (from a plain dash's length to MaxDistance) and the hover time (none at 0.2 s,
    // MaxHoverTime at 1 s); enhanced, both are full at 0.6 s.
    public class FrostSlingshotState : ChargedDashState
    {
        public new static string staticID = "FrostSlingshot";

        internal const float FlightTime = 0.3f;
        private const float MinHold = 0.2f;      // shorter is a normal dash
        internal const float MaxHoverTime = 2f;   // hover before the forced swap, at full charge
        internal const float MaxDistance = 9f;   // at full charge (the shortest is a plain dash)
        internal const float WallMargin = 0.75f;
        internal const float LingerTime = 2.5f;   // how long the feint stays after the swap

        protected override float MinChargeTime => MinHold;
        protected override bool TapIsPlainDash => true;

        // The feint in flight or hovering, until the swap.
        internal class Feint
        {
            public float thrownAt;
            public float hoverTime;
            public bool swapRequested;
            public bool swapped;
        }

        private Feint pending;

        public FrostSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // The feint travels instead of you: no dash boost, and you stand still while throwing.
        protected override bool BoostsDash => false;
        protected override bool HoldsStill => true;

        // Pressing dash while the feint hovers swaps you with it right away.
        protected override bool InterceptDash()
        {
            // Only while a feint can actually be out, so a dash press is never swallowed for good.
            if (pending == null || pending.swapped || Time.time > pending.thrownAt + FlightTime + pending.hoverTime + 0.5f)
                return false;
            pending.swapRequested = true;
            return true;
        }

        // Charging: ice crystals forming in a ring round your feet, closing in and growing as it
        // builds, with a frosty glint.
        protected override void ChargeEffect(Vector2 position, float charge)
        {
            float radius = 1.1f - 0.5f * charge;
            int count = 1 + Mathf.RoundToInt(2 * charge);
            for (int i = 0; i < count; i++)
            {
                float a = Random.value * Mathf.PI * 2f;
                IceShard.Spawn(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * radius, parent,
                    0.7f + 0.6f * charge);
            }
            if (Random.value < 0.3f)
                Effects.Shimmer(position, 1);
        }

        protected override void OnLaunch(float charge)
        {
            // Hold time 0.2 s -> 0, 1 s -> 1; enhanced, full range in half the charge-up (0.6 s).
            float span = (1f - MinHold) * (IsEmpowered ? 0.5f : 1f);
            float power = Mathf.Clamp01((charge - MinHold) / span);
            Vector2 direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            Vector2 start = parent.transform.position;
            float shortest = SlingshotDashPlugin.DashLength(parent);
            float distance = Mathf.Lerp(shortest, Mathf.Max(shortest, MaxDistance), power);
            // Stop short of walls so you never swap into one.
            RaycastHit2D hit = Physics2D.Raycast(start, direction, distance, ChaosCollisions.layerAllWallAndObst);
            if (hit.collider != null)
                distance = Mathf.Max(0f, hit.distance - WallMargin);

            parent.anim?.PlayDirectional(parent.ForehandAnimStr, -1, 0f);
            // Flair only: a splash and a glint as the feint leaves your hand.
            Effects.Splash(start, 0.8f, 2);
            Effects.Spark(start + direction * 0.5f, HitSparkType.Small);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(start), null, 24f, -1f, 1.5f, false);
            pending = new Feint { thrownAt = Time.time, hoverTime = MaxHoverTime * power };
            // The dash state ends long before the swap, so the feint runs on the plugin.
            SlingshotDashPlugin.Run(Throw(parent, pending, SpawnDecoy(start), start, start + direction * distance,
                SlingshotDashPlugin.FrostFreezeRadius * (1f + 0.5f * power), parent.skillCategory, skillID));
        }

        private GameObject SpawnDecoy(Vector2 position)
        {
            try
            {
                IceDecoy decoy = ChaosInst<IceDecoy>(IceDecoy.Prefab, new Vector2?(position), null, null);
                if (decoy == null)
                    return null;
                decoy.parentEnt = parent;
                decoy.duration = FlightTime + MaxHoverTime + LingerTime;
                return decoy.gameObject;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Ice feint unavailable: {e.Message}");
                return null;
            }
        }

        internal static IEnumerator Throw(Player player, Feint feint, GameObject decoy, Vector2 start, Vector2 end,
            float freezeRadius, string skillCategory, string id)
        {
            // Fly out.
            float t = 0f;
            float nextDust = 0f;
            while (t < FlightTime)
            {
                t += Time.deltaTime;
                Vector2 position = Vector2.Lerp(start, end, Mathf.SmoothStep(0f, 1f, t / FlightTime));
                MoveDecoy(decoy, position);
                if (t >= nextDust)
                {
                    nextDust = t + 0.05f;
                    PoolManager.GetPoolItem<DustEmitter>().EmitCircle(6, 0.4f, -3f, -1f, new Vector3?(position), null);
                    // Flair only: an icy shimmer trailing the feint.
                    Effects.Shimmer(position, 2);
                }
                yield return null;
            }

            // Hover with a slight bob until the time's up or you press dash.
            t = 0f;
            while (t < feint.hoverTime && !feint.swapRequested)
            {
                t += Time.deltaTime;
                MoveDecoy(decoy, end + new Vector2(0f, Mathf.Sin(t * 6f) * 0.08f));
                yield return null;
            }

            feint.swapped = true;
            if (player == null || !player.gameObject.activeInHierarchy)
                yield break;

            // Swap: you to the feint, the feint to wherever you are now.
            Vector2 here = player.transform.position;
            player.transform.position = end;
            if (player.rigidbody2D != null)
            {
                player.rigidbody2D.position = end;
                player.rigidbody2D.velocity = Vector2.zero;
            }
            MoveDecoy(decoy, here);

            // Freeze at both ends.
            FrostNova.CreateFrostNova(here, freezeRadius, skillCategory, id, 1);
            FrostNova.CreateFrostNova(end, freezeRadius, skillCategory, id, 1);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(40, 1.2f, -6f, -1f, new Vector3?(end), null);
            // Flair only: a frosty streak along the swap, splashes and glints at both ends, a shake.
            for (int i = 1; i < 6; i++)
                Effects.Shimmer(Vector2.Lerp(here, end, i / 6f), 2);
            foreach (Vector2 spot in new[] { here, end })
            {
                Effects.Splash(spot, freezeRadius, 3);
                Effects.Shimmer(spot, 6);
                Effects.Spark(spot, HitSparkType.Medium);
            }
            Effects.Shake(0.7f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(end), null, 24f, -1f, 1.7f, false);
        }

        internal static void MoveDecoy(GameObject decoy, Vector2 position)
        {
            if (decoy != null)
                decoy.transform.position = position;
        }
    }
}
