using Chaos.AnimatorExtensions;
using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vacuum Kick (Fire): a charged launch rushes you forward wrapped in Blazing Blitz's
    // flame trail, dropping flame bursts that drag enemies along your path. Where you land, a flame
    // vacuum keeps sucking everyone nearby in for a moment. Bigger with more charge.
    //
    // Level 1 is the trail: its knockback is overridden to point along the dash (BlazingDrag).
    // Level 2 is the vacuum's pulses: negative knockback, a pull toward the centre like Gust Burst.
    // Enhanced, levels 3 and 4 stand in for them: the same, but every hit sets enemies on fire.
    public class BlazingSlingshotState : ChargedDashState
    {
        public new static string staticID = "BlazingSlingshot";

        private const float TrailInterval = 0.1f;
        // Hit area of each trail burst; the visible flames are smaller.
        private const float TrailHitScale = 2f;
        // Hit area of the burst where you launch.
        private const float LaunchHitScale = 3f;
        private const float FlameInterval = 0.03f;
        private const int VacuumPulses = 4;
        private const float VacuumPulseInterval = 0.15f;

        private float nextTrail;
        private Vector2 direction;
        // Enhanced uses the burning copies of the levels (3 and 4).
        private int LevelOffset => IsEmpowered ? 2 : 0;
        private float nextFlame;

        public BlazingSlingshotState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        // When in the dash the wizard swings into the kick (flair only), and whether it has.
        private const float KickAt = 0.55f;
        private float launchedAt;
        private bool kickPlayed;

        // Charging: flames licking up round your feet, hotter as it builds.
        protected override void ChargeEffect(Vector2 position, float charge)
        {
            EmitBlitzFlames(position, 2 + Mathf.RoundToInt(5 * charge));
        }

        protected override void OnLaunch(float charge)
        {
            nextTrail = 0f;
            nextFlame = 0f;
            launchedAt = Time.time;
            kickPlayed = false;
            // Flair only: a fiery blast and a shake as you take off.
            Effects.FireBlast(parent.transform.position, 6 + Mathf.RoundToInt(6 * charge));
            Effects.Shake(0.5f + 0.4f * charge);
            direction = inputVector.sqrMagnitude > 0.01f
                ? inputVector.normalized
                : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f,
                0.9f - 0.2f * charge, false);
            // A wide hit where you launch (no effect of its own; the trail's flames show it).
            FlameBurst start = FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1 + LevelOffset, LaunchHitScale, false);
            if (start != null && start.attack != null)
                start.attack.knockbackOverwriteVector = direction;
        }

        protected override void WhileDashing(float charge)
        {
            Vector3 position = parent.transform.position;
            // Flair only: swing into the kick for the end of the rush.
            if (!kickPlayed && Time.time - launchedAt >= dashDuration * KickAt)
            {
                kickPlayed = true;
                parent.anim?.PlayDirectional(parent.KickAnimStr, -1, 0f);
            }
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
                FlameBurst burst = FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, TrailHitScale, false);
                if (burst != null && burst.attack != null)
                    burst.attack.knockbackOverwriteVector = direction;
                // The small visible burst (same skill, so it can't hit anyone twice; same drag).
                FlameBurst flames = FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, 0.8f + 0.4f * charge, true);
                if (flames != null && flames.attack != null)
                    flames.attack.knockbackOverwriteVector = direction;
            }
        }

        protected override void OnLand(float charge)
        {
            Vector3 position = parent.transform.position;
            // Flair only: the kick lands in a blast, with dust streaming in to it.
            Effects.FireBlast(position, 10 + Mathf.RoundToInt(8 * charge));
            Effects.DustSuction(position, 30);
            Effects.Shake(0.8f + 0.4f * charge);
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
            SlingshotDashPlugin.Run(Vacuum(position, 1.0f + 0.4f * charge, parent.skillCategory, skillID, 2 + LevelOffset, vacuum));
        }

        private static IEnumerator Vacuum(Vector3 position, float scale, string skillCategory, string id, int level, GameObject vacuum)
        {
            for (int i = 0; i < VacuumPulses; i++)
            {
                FlameBurst.CreateBurst(position, skillCategory, id, level, scale, true);
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
