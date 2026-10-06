using System.Collections;
using UnityEngine;

namespace WoLSlingshotDash
{
    // Raging Wind (Air): a charged launch bursts with wind where you leave and where you land,
    // knocking nearby enemies away. Bigger with more charge. The landing burst uses skill level 2,
    // which has gentler knockback. Enhanced, it also leaves a trail of gusts along the dash
    // (level 3) that each blow twice.
    public class SlingshotDashState : ChargedDashState
    {
        public new static string staticID = "SlingshotDash";

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        private const float GustInterval = 0.035f;
        private const float TrailInterval = 0.08f;
        private const float TrailScale = 1.1f;
        private const float TrailRepeat = 0.35f;
        private float nextGust;
        private float nextTrail;

        // Charging: a whirlwind spinning fast round your feet, quicker and wider as it builds.
        protected override float ChargeEffectInterval => 0.035f;
        private float whirl;

        protected override void ChargeEffect(Vector2 position, float charge)
        {
            whirl += 75f * Mathf.Deg2Rad;
            float radius = 0.5f + 0.5f * charge;
            for (int i = 0; i < 2; i++)
            {
                float a = whirl + i * Mathf.PI;
                Effects.AirPuffs(position + new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * radius,
                    1 + Mathf.RoundToInt(2 * charge));
            }
            if (charge >= 0.3f && Random.value < 0.25f)
                Effects.WindSwirl(position, 1);
        }

        protected override void OnLaunch(float charge)
        {
            Burst(charge, false);
            // Flair only (no hits): a kick of wind and a shake as you take off.
            Vector2 position = parent.transform.position;
            Effects.AirPuffs(position, 8 + Mathf.RoundToInt(8 * charge));
            Effects.Shake(0.5f + 0.5f * charge);
            nextGust = 0f;
            nextTrail = 0f;
        }

        // Flair only: a streaming gust behind you as you fly.
        protected override void WhileDashing(float charge)
        {
            // Enhanced: gusts left along the dash line.
            if (IsEmpowered && Time.time >= nextTrail)
            {
                nextTrail = Time.time + TrailInterval;
                SlingshotDashPlugin.Run(TrailGust(parent.transform.position, parent.skillCategory, skillID));
            }
            if (Time.time < nextGust)
                return;
            nextGust = Time.time + GustInterval;
            Vector2 position = parent.transform.position;
            Effects.AirPuffs(position, 2);
            Effects.Dust(position, 3, 0.25f);
        }

        protected override void OnLand(float charge)
        {
            Burst(charge, true);
            Effects.WindSwirl(parent.transform.position, 2);
            Effects.Shake(0.4f + 0.4f * charge);
        }

        // One gust on the trail: blows now and once more a moment later.
        private static IEnumerator TrailGust(Vector2 position, string skillCategory, string id)
        {
            for (int i = 0; i < 2; i++)
            {
                WindBurst gust = WindBurst.CreateBurst(position, skillCategory, id, 3, TrailScale);
                if (gust != null)
                    gust.emitParticles = false;
                Effects.WindSwirl(position, 1);
                Effects.AirPuffs(position, 3);
                yield return new WaitForSeconds(TrailRepeat);
            }
        }

        // The hit area is the burst scale, and the dust ring is drawn to match it.
        private void Burst(float charge, bool landing)
        {
            Vector3 position = parent.transform.position;
            float scale = (landing ? 1.75f : 1.5f) + 1.75f * charge;
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, landing ? 2 : 1, scale);
            burst.emitParticles = false;
            PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2 + Mathf.RoundToInt(3 * charge)),
                new Vector3?(position), null, null, 0f, null, null);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60 + Mathf.RoundToInt(90 * charge), scale, -8f, -1f,
                new Vector3?(position), null);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f,
                (landing ? 1.1f : 1.3f) - 0.3f * charge, false);
        }
    }
}
