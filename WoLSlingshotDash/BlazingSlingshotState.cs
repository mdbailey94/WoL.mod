using Chaos.AnimatorExtensions;
using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Vacuum Kick (Fire): a charged launch rushes you forward wrapped in Blazing Blitz's
    // flame trail, dropping flame bursts that shove enemies out of your path. The kick at the end
    // lands in a fiery blast that throws everyone nearby away. Bigger with more charge.
    //
    // Level 1 is the launch and trail (knockback pushes away from each burst), level 2 the blast.
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
        // The blast where you land.
        private const float BlastScale = 2f;
        private const float BlastChargeScale = 1.25f;

        private float nextTrail;
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

        // Charging: flames licking up round your feet, hotter as it builds, and the wizard
        // trembling with the heat.
        protected override void ChargeEffect(Vector2 position, float charge)
        {
            EmitBlitzFlames(position, 2 + Mathf.RoundToInt(5 * charge));
        }

        protected override float ChargeShake(float charge) => 0.03f + 0.04f * charge;

        protected override void OnLaunch(float charge)
        {
            nextTrail = 0f;
            nextFlame = 0f;
            launchedAt = Time.time;
            kickPlayed = false;
            // Flair only: a fiery blast and a shake as you take off.
            Effects.FireBlast(parent.transform.position, 6 + Mathf.RoundToInt(6 * charge));
            Effects.Shake(0.5f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(parent.transform.position), null, 24f, -1f,
                0.9f - 0.2f * charge, false);
            // A wide hit where you launch, shoving enemies away (no effect of its own; the
            // trail's flames show it).
            FlameBurst.CreateBurst(parent.transform.position, parent.skillCategory, skillID, 1 + LevelOffset, LaunchHitScale, false);
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
                // are the Blitz trail), so it catches enemies you dash past and shoves them out of
                // your way.
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, TrailHitScale, false);
                // The small visible burst (same skill, so it can't hit anyone twice).
                FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 1 + LevelOffset, 0.8f + 0.4f * charge, true);
            }
        }

        // The kick lands in a fiery blast that throws everyone nearby away (level 2).
        protected override void OnLand(float charge)
        {
            Vector2 position = parent.transform.position;
            float scale = BlastScale + BlastChargeScale * charge;
            FlameBurst.CreateBurst(position, parent.skillCategory, skillID, 2 + LevelOffset, scale, true);
            EmitBlitzFlames(position, 14);
            // Flair only: the blast's fire, a ring of dust thrown out and a shake.
            Effects.FireBlast(position, 12 + Mathf.RoundToInt(8 * charge));
            Effects.Dust(position, 60, scale);
            Effects.Shake(0.9f + 0.4f * charge);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                0.7f - 0.1f * charge, false);
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
