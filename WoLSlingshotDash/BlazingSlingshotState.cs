using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Blazing Slingshot (Fire): a charged launch rushes you forward wrapped in Blazing Blitz's
    // flame trail, dropping flame bursts that drag enemies along your path. Where you land, a flame
    // vacuum keeps sucking everyone nearby in for a moment. Bigger with more charge.
    //
    // Level 1 is the trail: its knockback is overridden to point along the dash (BlazingDrag).
    // Level 2 is the vacuum's pulses: negative knockback, a pull toward the centre like Gust Burst.
    public class BlazingSlingshotState : ChargedDashState
    {
        public new static string staticID = "BlazingSlingshot";

        private const float TrailInterval = 0.1f;
        // Hit area of each trail burst; the visible flames are smaller.
        private const float TrailHitScale = 2f;
        private const float FlameInterval = 0.03f;
        private const int VacuumPulses = 4;
        private const float VacuumPulseInterval = 0.15f;

        private float nextTrail;
        private Vector2 direction;
        private float nextFlame;

        public BlazingSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        protected override void OnLaunch(float charge)
        {
            nextTrail = 0f;
            nextFlame = 0f;
            direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f,
                0.9f - 0.2f * charge, false);
        }

        protected override void WhileDashing(float charge)
        {
            Vector3 position = parent.transform.position;
            nextFlame -= Time.deltaTime;
            if (nextFlame <= 0f)
            {
                nextFlame = FlameInterval;
                EmitBlitzFlames(position, 2 + Mathf.RoundToInt(3 * charge));
            }
            nextTrail -= Time.deltaTime;
            if (nextTrail <= 0f)
            {
                nextTrail = TrailInterval;
                // The hit: a full-size burst with no effect of its own (the small visible flames
                // are the Blitz trail), so it catches enemies you dash past. Knocks them along the
                // dash so the trail drags them with you.
                FlameBurst burst = FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1, TrailHitScale, false);
                if (burst != null && burst.attack != null)
                    burst.attack.knockbackOverwriteVector = direction;
                // The small visible burst (same skill, so it can't hit anyone twice; same drag).
                FlameBurst flames = FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1, 0.8f + 0.4f * charge, true);
                if (flames != null && flames.attack != null)
                    flames.attack.knockbackOverwriteVector = direction;
            }
        }

        protected override void OnLand(float charge)
        {
            Vector3 position = parent.transform.position;
            GameObject vacuum = null;
            try
            {
                VacuumFlameSmall flame = ChaosInst<VacuumFlameSmall>(VacuumFlameSmall.Prefab, new Vector2?(position), null, null);
                vacuum = flame != null ? flame.gameObject : null;
            }
            catch (System.Exception e)
            {
                SlingshotDashPlugin.Log($"Vacuum flame effect unavailable: {e.Message}");
            }
            // The dash state ends here, so the vacuum runs on the plugin.
            SlingshotDashPlugin.Run(Vacuum(position, 1.4f + 0.6f * charge, parent.skillCategory, skillID, vacuum));
        }

        private static IEnumerator Vacuum(Vector3 position, float scale, string skillCategory, string id, GameObject vacuum)
        {
            for (int i = 0; i < VacuumPulses; i++)
            {
                FlameBurst.CreateBurst(position, skillCategory, id, 2, scale, true);
                EmitBlitzFlames(position, 6);
                yield return new WaitForSeconds(VacuumPulseInterval);
            }
            if (vacuum != null)
            {
                VacuumFlameSmall flame = vacuum.GetComponent<VacuumFlameSmall>();
                try
                {
                    if (flame != null)
                        flame.CleanUp();
                }
                catch
                {
                }
                if (vacuum != null && vacuum.activeSelf)
                    Object.Destroy(vacuum);
            }
        }

        // The flame particles Blazing Blitz trails behind the wizard.
        private static void EmitBlitzFlames(Vector3 position, int count)
        {
            try
            {
                FireBurst fire = PoolManager.GetPoolItem<FireBurst>();
                if (fire != null)
                    fire.EmitSingle(new int?(count), new Vector3?(position));
            }
            catch
            {
            }
        }
    }
}
