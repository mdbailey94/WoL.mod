using UnityEngine;

namespace WoLSlingshotDash
{
    // Slingshot (Air): a charged launch bursts with wind where you leave and where you land,
    // knocking nearby enemies away. Bigger with more charge.
    public class SlingshotDashState : ChargedDashState
    {
        public new static string staticID = "SlingshotDash";

        public SlingshotDashState(FSM fsm, Player parentPlayer) : base(staticID, fsm, parentPlayer)
        {
        }

        protected override void OnLaunch(float charge) => Burst(charge, false);

        protected override void OnLand(float charge) => Burst(charge, true);

        // The hit area is the burst scale, and the dust ring is drawn to match it.
        private void Burst(float charge, bool landing)
        {
            Vector3 position = parent.transform.position;
            float scale = (landing ? 1.75f : 1.5f) + 1.75f * charge;
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
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
