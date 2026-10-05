using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLRollingGale
{
    // Fires a line of wind bursts that step forward along the aim direction, each a little
    // further out and a little larger. Built on the same calls as SillySkills' Gale Burst.
    public class RollingGaleState : Player.SkillState
    {
        public new static string staticID = "RollingGale";

        private const float FirstBurstDistance = 2.5f;
        private const float BurstSpacing = 3f;
        private const float FirstBurstScale = 2.5f;
        private const float BurstScaleGrowth = 0.5f;
        private const float BurstInterval = 0.12f;

        private int stopwatchID;
        private int burstCount;
        private int burstsFired;
        private Vector2 origin;
        private Vector2 direction;

        public RollingGaleState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.1f,  // execute
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Lock the line's start and direction on cast so it doesn't swing if the player turns.
            direction = GetInputVector();
            if (direction == Vector2.zero)
                direction = Vector2.right;
            origin = parent.attackOriginTrans.position;

            burstCount = IsEmpowered ? 4 : 3;
            burstsFired = 0;
            stopwatchID = ChaosStopwatch.Begin(
                timeValue: 0.05f,
                useInterval: true,
                intervalTime: BurstInterval,
                totalIntervals: burstCount);

            parent.FaceTarget(origin + direction);
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, animExecTime);
        }

        public override void ExecuteSkill()
        {
            if (CancelToDash(false))
                return;

            switch (ChaosStopwatch.CheckInterval(stopwatchID, true))
            {
                case StopwatchState.Done:
                    base.ExecuteSkill();
                    break;
                case StopwatchState.Ready:
                    if (burstsFired < burstCount)
                        CreateBurst(burstsFired++);
                    break;
            }
        }

        private void CreateBurst(int index)
        {
            Vector2 position = origin + direction * (FirstBurstDistance + BurstSpacing * index);
            float scale = FirstBurstScale + BurstScaleGrowth * index;

            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, 1, scale);
            burst.emitParticles = false;

            var vortex = new ParticleSystemOverride
            {
                startSize = new float?(2.4f * scale),
                startLifetime = new float?(0.6f)
            };
            PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(position), vortex,
                new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);
            PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2), new Vector3?(position), null, null, 0f, null, null);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60, 1.5f, -8f, -1f, new Vector3?(position), null);

            // Rising pitch on each burst so the line sounds like it's rolling away.
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 1.1f + 0.1f * index, false);
        }
    }
}
