using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLRollingGale
{
    // Fires a line of wind bursts that step forward along the aim direction, each a little
    // further out and a little larger, and each pulsing a second time a moment later. Built on the same calls as SillySkills' Gale Burst.
    //
    // Signature (0.5.0; isUltimate, the game's flag for a signature cast): six bursts sweep out in
    // a fan from one side of the aim to the other, each further out, bigger and harder (level 3),
    // each landing with a short hit-stop; then a gathering beat and a huge seventh burst at the
    // fan's middle (level 4) that drags everything in, with a hit-stop, a flash, a heavy shake
    // and a moment of near-frozen time. The camera pulls back and the room darkens throughout
    // (Cinematic, always put back).
    public class RollingGaleState : Player.SkillState
    {
        public new static string staticID = "RollingGale";
        // The signature fan. The gameplay numbers are settings ([Signature], RollingGalePlugin.cs);
        // the camera pull-back, darkening, flash and shake stay fixed here.
        private static int FanBursts => RollingGalePlugin.FanBursts;
        private static float FanSpread => RollingGalePlugin.FanSpread;          // degrees either side of the aim
        private static float FanStart => RollingGalePlugin.FanDelay;
        private static float FanStep => RollingGalePlugin.FanInterval;
        private static float FanFirstDistance => RollingGalePlugin.FanFirstDistance;
        private static float FanDistanceStep => RollingGalePlugin.FanDistanceStep;
        private static float FanFirstScale => RollingGalePlugin.FanFirstScale;
        private static float FanScaleStep => RollingGalePlugin.FanScaleStep;
        private static float GatherTime => RollingGalePlugin.GatherTime;        // the beat before the final gale
        private static float FinalDistance => RollingGalePlugin.FinalDistance;
        private static float FinalScale => RollingGalePlugin.FinalScale;
        private const float SigZoom = 0.18f, SigDim = 0.6f;
        private static int FanHitStopFrames => RollingGalePlugin.FanHitStopFrames;
        private static int FinalHitStopFrames => RollingGalePlugin.FinalHitStopFrames;
        private static float FinalSlowMoTime => RollingGalePlugin.FinalSlowMoTime;
        private static float FinalSlowMoScale => RollingGalePlugin.FinalSlowMoScale;
        private const float FinalFlash = 1.7f, FinalShake = 2f, EaseBack = 0.8f;
        private bool signature, finalDone, gathered;
        private float sigTime;
        private int fanFired;
        private int cinematic;

        // The line ([Cast] and [Enhanced] settings).
        private static float FirstBurstDistance => RollingGalePlugin.FirstBurstDistance;
        private static float BurstSpacing => RollingGalePlugin.BurstSpacing;
        private static float FirstBurstScale => RollingGalePlugin.FirstBurstScale;
        private static float BurstScaleGrowth => RollingGalePlugin.BurstScaleGrowth;
        private static float BurstInterval => RollingGalePlugin.BurstInterval;
        // Each burst pulses again one interval later at the same spot, so the pull lasts longer.
        private static float EchoScale => RollingGalePlugin.EchoScale;

        private int stopwatchID;
        private int burstCount;
        private int burstsFired;
        private Vector2 origin;
        private Vector2 direction;

        public RollingGaleState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            // Lets the arcana into the signature slot (the spellbook's Signature page); see
            // SignatureUnlock.cs for when that's unlocked.
            hasSignatureVariant = true;
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

            burstCount = IsEmpowered ? RollingGalePlugin.EnhancedBurstCount : RollingGalePlugin.BurstCount;
            burstsFired = 0;
            // One extra interval for the last burst's echo.
            stopwatchID = ChaosStopwatch.Begin(
                timeValue: 0.05f,
                useInterval: true,
                intervalTime: BurstInterval,
                totalIntervals: burstCount + 1);

            parent.FaceTarget(origin + direction);
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, animExecTime);
            GaleLook.Cast(parent, origin, direction);

            signature = isUltimate;
            sigTime = 0f;
            fanFired = 0;
            finalDone = gathered = false;
            cinematic = 0;
            if (signature)
            {
                cinematic = Cinematic.Begin();
                RollingGalePlugin.Log($"Rolling Gale: signature: a fan of {FanBursts} bursts across {FanSpread * 2f:0} degrees, then a final gale x{FinalScale:0.#}");
            }
        }

        public override void OnExit()
        {
            if (signature)
                Cinematic.EaseOut(cinematic, 0.5f, finalDone ? "cast over" : "cut short before the final gale");
            base.OnExit();
        }

        public override void ExecuteSkill()
        {
            if (CancelToDash(false))
                return;
            if (signature)
            {
                SignatureTick();
                return;
            }

            switch (ChaosStopwatch.CheckInterval(stopwatchID, true))
            {
                case StopwatchState.Done:
                    base.ExecuteSkill();
                    break;
                case StopwatchState.Ready:
                    // Echo the previous burst, then fire the next one further out.
                    if (burstsFired > 0 && burstsFired <= burstCount)
                        CreateBurst(burstsFired - 1, true);
                    if (burstsFired < burstCount)
                        CreateBurst(burstsFired, false);
                    burstsFired++;
                    break;
            }
        }

        // ---- The signature ----

        private Vector2 FanSpot(int i)
        {
            float t = FanBursts > 1 ? i / (float)(FanBursts - 1) : 0.5f;
            Vector2 dir = Quaternion.Euler(0f, 0f, Mathf.Lerp(-FanSpread, FanSpread, t)) * (Vector3)direction;
            return origin + dir * (FanFirstDistance + FanDistanceStep * i);
        }

        private float FanScale(int i) => FanFirstScale + FanScaleStep * i;

        private void SignatureTick()
        {
            sigTime += Time.deltaTime;
            float settle = Mathf.Clamp01(sigTime / 0.3f);
            float eased = settle * settle * (3f - 2f * settle);
            if (!finalDone)
                Cinematic.Set(cinematic, SigZoom * eased, Mathf.Lerp(1f, SigDim, eased));
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, animExecTime);

            // The fan: each burst echoes the one before as it lands.
            while (fanFired < FanBursts && sigTime >= FanStart + FanStep * fanFired)
            {
                if (fanFired > 0)
                    Echo(FanSpot(fanFired - 1), FanScale(fanFired - 1));
                FanBurst(fanFired);
                fanFired++;
            }
            float finalAt = FanStart + FanStep * FanBursts + GatherTime;
            if (fanFired >= FanBursts && !gathered && sigTime >= finalAt - GatherTime)
            {
                gathered = true;
                Echo(FanSpot(FanBursts - 1), FanScale(FanBursts - 1));
                Gather();
            }
            if (!finalDone && sigTime >= finalAt)
            {
                finalDone = true;
                FinalGale();
            }
            if (finalDone && sigTime >= finalAt + 0.25f)
                base.ExecuteSkill();
        }

        private void FanBurst(int i)
        {
            Vector2 position = FanSpot(i);
            float scale = FanScale(i);
            WindBurst burst = WindBurst.CreateBurst(position, AttackInfo.GetInfo(parent, parent.skillCategory, skillID, 3, true), scale);
            if (burst != null)
                burst.emitParticles = false;
            Try(() =>
            {
                var vortex = new ParticleSystemOverride { startSize = new float?(2.4f * scale), startLifetime = new float?(0.6f) };
                PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(position), vortex,
                    new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);
            });
            Vector2 previous = i == 0 ? origin : FanSpot(i - 1);
            Vector2 heading = (position - previous).sqrMagnitude > 0.01f ? (position - previous).normalized : direction;
            GaleLook.Burst(position, scale, false, heading);
            GaleLook.Roll(previous, position, heading);
            Try(() => PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2), new Vector3?(position), null, null, 0f, null, null));
            Try(() => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60, 1.5f, -8f, -1f, new Vector3?(position), null));
            Try(() => TimeScaleController.FreezeForFrames(FanHitStopFrames, false));
            Try(() => CameraController.ShakeCamera(0.35f + 0.08f * i, false));
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 1.0f + 0.08f * i, false);
        }

        private void Echo(Vector2 position, float scale)
        {
            WindBurst burst = WindBurst.CreateBurst(position, AttackInfo.GetInfo(parent, parent.skillCategory, skillID, 2, true), scale * EchoScale);
            if (burst != null)
                burst.emitParticles = false;
            GaleLook.Burst(position, scale * EchoScale, true, direction);
        }

        // The beat before the final gale: the air rushes in to the fan's middle.
        private void Gather()
        {
            Vector2 at = origin + direction * FinalDistance;
            Try(() => PoolManager.GetPoolItem<DustSuctionEffect>().Emit(new int?(40), new Vector3?(at), null, null, 0f, null, null));
            Try(() => PoolManager.GetPoolItem<ParticleEffect>("WindTwirlEffect").Emit(new int?(1), new Vector3?(at), null, null, 0f, null, null));
            Try(() => CameraController.ShakeCamera(0.6f, false));
            Cinematic.Flash(cinematic, 0.4f, 0.12f);   // the room dims further for a breath
            RollingGalePlugin.Log("Rolling Gale: signature gather");
        }

        // The final gale: a huge burst at the fan's middle dragging everything in.
        private void FinalGale()
        {
            Vector2 at = origin + direction * FinalDistance;
            WindBurst.CreateBurst(at, AttackInfo.GetInfo(parent, parent.skillCategory, skillID, 4, true), FinalScale);
            Try(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    var vortex = new ParticleSystemOverride { startSize = new float?(2.4f * FinalScale * (1f + 0.3f * i)), startLifetime = new float?(0.5f + 0.15f * i) };
                    PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(at), vortex,
                        new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0.06f * i, null, null);
                }
            });
            GaleLook.Burst(at, FinalScale, false, direction);
            GaleLook.End(at);
            Try(() => PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(6), new Vector3?(at), null, null, 0f, null, null));
            Try(() => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(140, FinalScale * 0.5f, -8f, -1f, new Vector3?(at), null));
            Try(() => TimeScaleController.FreezeForFrames(FinalHitStopFrames, false));
            Cinematic.Flash(cinematic, FinalFlash, 0.1f);
            Try(() => CameraController.ShakeCamera(FinalShake, true));
            Try(() => TimeScaleController.SetTimeScaleDelayOverRealTime(FinalSlowMoTime, FinalSlowMoScale, true, true));
            Cinematic.EaseOut(cinematic, EaseBack, "final gale finished");
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(at), null, 24f, -1f, 0.7f, false);
            RollingGalePlugin.Log($"Rolling Gale: signature final gale x{FinalScale:0.#}: hit-stop {FinalHitStopFrames} frames, flash x{FinalFlash:0.0}, " +
                $"shake {FinalShake:0.0}, slow motion {FinalSlowMoTime:0.00} s, easing back over {EaseBack:0.0} s");
        }

        private static void Try(System.Action effect)
        {
            try
            {
                effect();
            }
            catch
            {
            }
        }

        private void CreateBurst(int index, bool echo)
        {
            Vector2 position = origin + direction * (FirstBurstDistance + BurstSpacing * index);
            float scale = (FirstBurstScale + BurstScaleGrowth * index) * (echo ? EchoScale : 1f);

            // Echoes use skill level 2: mostly pull, little damage.
            WindBurst burst = WindBurst.CreateBurst(position, parent.skillCategory, skillID, echo ? 2 : 1, scale);
            burst.emitParticles = false;

            var vortex = new ParticleSystemOverride
            {
                startSize = new float?(2.4f * scale),
                startLifetime = new float?(0.6f)
            };
            PoolManager.GetPoolItem<ParticleEffect>("AirVortex").Emit(new int?(1), new Vector3?(position), vortex,
                new Vector3?(new Vector3(0f, 0f, Random.Range(0f, 360f))), 0f, null, null);

            // The drawn look (GaleLook): the spiral and sucked-in debris, the crescents rolling on
            // to each new burst, and a puff when the last echo is spent.
            GaleLook.Burst(position, scale, echo, direction);
            if (echo)
            {
                if (index == burstCount - 1)
                    GaleLook.End(position);
                return;
            }
            Vector2 previous = index == 0 ? origin : origin + direction * (FirstBurstDistance + BurstSpacing * (index - 1));
            GaleLook.Roll(previous, position, direction);
            PoolManager.GetPoolItem<ParticleEffect>("WindBurstEffect").Emit(new int?(2), new Vector3?(position), null, null, 0f, null, null);
            PoolManager.GetPoolItem<DustEmitter>().EmitCircle(60, 1.5f, -8f, -1f, new Vector3?(position), null);

            // Rising pitch on each burst so the line sounds like it's rolling away.
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(position), null, 24f, -1f, 1.1f + 0.1f * index, false);
        }
    }
}
