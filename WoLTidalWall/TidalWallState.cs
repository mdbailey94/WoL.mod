using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLTidalWall
{
    // Tidal Wall (Water, standard arcana): a single standing wall of water rises across your aim,
    // a short way in front of you. It blocks enemy projectiles (the game's own water wall does it),
    // stands for a moment, then rolls slowly forward as one front, shoving everything it touches
    // along with it on a steady beat, and crashes in a splash at the end of its run or when it
    // meets a wall. Its job is control, not damage: a few light pushes and a small crash. The wave
    // itself is WaveFront.cs, on its own object, so the wizard is free once it's cast.
    //
    // Enhanced (IsEmpowered): a wider wall that rolls further.
    //
    // Cast from the signature slot with the gauge full (isUltimate, the game's flag for a signature
    // cast; it's set on the state just before the cast and cleared when the state exits) it's a
    // towering TIDAL WAVE, in four beats: the wizard gathers the water (camera pulls back, the room
    // darkens, water streams in behind them with a rumble), the wave erupts (hit-stop), rushes far
    // and fast with a hit-stop on every push that lands, and crashes (time all but stops and eases
    // back, the room flashes, everything it carried freezes). Enhanced and signature are separate
    // flags and stack: a signature cast of the enhanced arcana (the usual case, the game empowers
    // whatever sits in the signature slot) is wider and rushes further still.
    //
    // The camera and light changes are Cinematic.cs, and always put back (see there).
    public class TidalWallState : Player.SkillState
    {
        public new static string staticID = "TidalWall";

        private const float RiseDistance = 2f;          // the wall rises this far in front (Water Wall's 1.75, a little more for width)
        private const float WallBackoff = 0.4f;         // a wall rising into a level wall stops this far short of it
        // The standing wall.
        private const int Columns = 5;                  // water columns side by side (Water Wall's own count)
        private const int EnhancedExtraColumns = 2;     // enhanced: wider
        // 0.4.4 (Matt: too wide, pushes too far): a quarter narrower and a fifth shorter at every
        // level, and half the knockback (TidalWallPlugin).
        private const float ColumnSpacing = 0.7f;       // so 3.5 wide, 4.9 enhanced (0.9: 4.5 / 6.3)
        private const float WallDepth = 1.4f;           // thickness of its hit area along the aim
        private const float HoldTime = 0.2f;            // seconds it stands before rolling
        private const float RollSpeed = 6f;             // slow: it carries enemies rather than smashing them
        private const float RollTime = 0.8f;            // 4.8 units (was 6)
        private const float EnhancedRollTime = 1.05f;   // 6.3 units (was 7.8)
        private const float TickInterval = 0.35f;       // a push every this many seconds while rolling
        // The signature tidal wave.
        private const int SignatureColumns = 8;         // 10 when also enhanced
        private const float SignatureColumnSpacing = 1f;   // 8 wide, 10 when also enhanced (1.3: 10.4 / 13)
        private const float SignatureColumnScale = 1.7f;   // the columns tower over the wizard
        private const float SignatureDepth = 2.4f;
        private const float SignatureHoldTime = 0.1f;
        private const float SignatureSpeed = 12f;
        private const float SignatureRollTime = 1f;     // 12 units (was 15)
        private const float EnhancedSignatureRollTime = 1.2f; // 14.4 units (was 18)
        private const float SignatureTickInterval = 0.25f;
        // Skill levels (SkillStats in the plugin).
        private const int TickLevel = 1;
        private const int CrashLevel = 2;
        private const int SignatureTickLevel = 3;
        private const int SignatureCrashLevel = 4;
        // The signature's gather (beat 1).
        private const float GatherTime = 0.6f;          // seconds the wizard draws the water in
        private const float GatherBehind = 1.5f;        // the water gathers this far behind the wizard
        private const float GatherRadius = 3.5f;        // from this far around
        private const float GatherPose = 0.3f;          // the wizard holds the cast pose here (past animExecTime)
        private const float RumbleInterval = 0.12f;
        private const float RumbleShake = 0.12f;        // grows to RumbleShake + RumbleGrowth as the water builds
        private const float RumbleGrowth = 0.25f;

        private Vector2 direction;
        private bool signature;   // this cast is the signature wave (isUltimate at cast time)
        private bool launched;
        private bool gathering;
        private float gatherTime;
        private float rumbleClock;
        private int cinematic;    // Cinematic token while the signature's camera/light are changed
        private ParticleEffect gatherEffect;

        public override string OnEnterAnimStr => isUltimate ? parent.PBAoEAnimStr : parent.ForehandAnimStr;

        public TidalWallState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot: the spellbook's Signature page and the
            // saved loadout both require it (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.2f,  // execute
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Lock the aim on cast so the wave doesn't swing if the player turns.
            direction = inputVector;
            if (direction == Vector2.zero)
                direction = Vector2.right;
            direction.Normalize();

            // The game sets isUltimate for a signature cast (the signature slot's arcana with the
            // gauge full) and clears it once the state exits, so it's read once here.
            signature = isUltimate;
            launched = false;
            gathering = false;
            gatherTime = 0f;
            rumbleClock = 0f;
            cinematic = 0;
            gatherEffect = null;

            WaveSettings plan = Plan();
            TidalWallPlugin.Log($"Tidal Wall: cast ({Variant()}), wall {plan.Width:0.0} wide ({plan.Columns} columns), " +
                $"rolls {plan.Distance:0.0} at {plan.Speed:0.0}/s ({plan.RollTime:0.00} s), a push every {plan.TickInterval:0.00} s, " +
                $"toward ({direction.x:0.00}, {direction.y:0.00})");
        }

        // Which version of the arcana this cast is, for the log: normal, enhanced (the relic-based
        // empowerment, IsEmpowered), signature (the gauge cast, isUltimate) or both at once.
        private string Variant()
        {
            if (IsEmpowered && signature)
                return "both: enhanced + signature";
            if (signature)
                return "signature";
            return IsEmpowered ? "enhanced" : "normal";
        }

        // The wave this cast makes (where it rises is worked out when it's let go).
        private WaveSettings Plan()
        {
            int extra = IsEmpowered ? EnhancedExtraColumns : 0;
            return new WaveSettings
            {
                Variant = Variant(),
                Signature = signature,
                SkillCategory = parent.skillCategory,
                SkillID = skillID,
                Caster = parent.gameObject,
                Direction = direction,
                Columns = (signature ? SignatureColumns : Columns) + extra,
                Spacing = signature ? SignatureColumnSpacing : ColumnSpacing,
                ColumnScale = signature ? SignatureColumnScale : 1f,
                Depth = signature ? SignatureDepth : WallDepth,
                HoldTime = signature ? SignatureHoldTime : HoldTime,
                Speed = signature ? SignatureSpeed : RollSpeed,
                RollTime = signature ? (IsEmpowered ? EnhancedSignatureRollTime : SignatureRollTime)
                    : (IsEmpowered ? EnhancedRollTime : RollTime),
                TickInterval = signature ? SignatureTickInterval : TickInterval,
                TickLevel = signature ? SignatureTickLevel : TickLevel,
                CrashLevel = signature ? SignatureCrashLevel : CrashLevel,
            };
        }

        public override void ExecuteSkill()
        {
            if (launched)
            {
                base.ExecuteSkill();
                return;
            }

            if (!signature)
            {
                // The wall rises with the swing.
                parent.FaceTarget((Vector2)parent.transform.position + direction);
                Launch();
                base.ExecuteSkill();
                return;
            }

            // The signature: gather the water first. No dash-cancel here, the gauge is already spent.
            if (!gathering)
                StartGather();
            gatherTime += Time.deltaTime;
            UpdateGather();
            if (gatherTime >= GatherTime)
            {
                parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, animExecTime);
                StopGatherEffect();
                Launch();
                base.ExecuteSkill();
            }
        }

        // Beat 1: the wizard draws the water in behind them; the camera pulls back and the room
        // darkens as it builds.
        private void StartGather()
        {
            gathering = true;
            parent.FaceTarget((Vector2)parent.transform.position + direction);
            cinematic = Cinematic.Begin("Tidal Wall's signature");
            float baseSize = 0f;
            Effects.Try("camera size", () => baseSize = Cinematic.BaseCameraSize);
            float light = Cinematic.LightOriginal;
            Vector2 behind = (Vector2)parent.transform.position - direction * GatherBehind;
            gatherEffect = Effects.Gather(behind, GatherRadius, GatherTime);
            Effects.Ring(behind, GatherRadius * 0.9f);
            Effects.Sound("Rumble", parent.transform.position, 0.8f);
            Effects.Sound("AquaBeamStart", behind, 0.75f);
            string lighting = light >= 0f ? $"stage light {light:0.00} -> {light * WaveFront.SignatureDim:0.00}" : "no stage light in this room";
            TidalWallPlugin.Log($"Tidal Wall: signature beat 1/4 gather: {GatherTime:0.00} s, camera pulls back " +
                $"{WaveFront.SignatureZoom * 100f:0}% (size {baseSize:0.00} -> {baseSize * (1f + WaveFront.SignatureZoom):0.00}), " +
                $"{lighting}, rumble");
        }

        private void UpdateGather()
        {
            float t = Mathf.Clamp01(gatherTime / GatherTime);
            float eased = t * t * (3f - 2f * t);
            Cinematic.KeepAlive(cinematic);
            Cinematic.SetZoom(cinematic, WaveFront.SignatureZoom * eased);
            Cinematic.SetLight(cinematic, Mathf.Lerp(1f, WaveFront.SignatureDim, eased));
            // Hold the cast pose while the water builds (past animExecTime, so the game keeps
            // calling ExecuteSkill).
            parent.anim.PlayDirectional(parent.PBAoEAnimStr, -1, GatherPose);

            rumbleClock += Time.deltaTime;
            if (rumbleClock >= RumbleInterval)
            {
                rumbleClock -= RumbleInterval;
                Effects.Shake(RumbleShake + RumbleGrowth * t);
                Vector2 behind = (Vector2)parent.transform.position - direction * GatherBehind;
                Effects.Drops(behind + Random.insideUnitCircle * GatherRadius, 3, false);
            }
        }

        private void StopGatherEffect()
        {
            if (gatherEffect == null)
                return;
            Effects.Try("water gathering stop", () => gatherEffect.Stop());
            gatherEffect = null;
        }

        // Sends the wave off: it rises in front of the wizard (short of any wall there) and runs
        // by itself from now on.
        private void Launch()
        {
            launched = true;
            WaveSettings wave = Plan();
            Vector2 origin = parent.attackOriginTrans.position;
            Vector2 target = origin + direction * RiseDistance;
            Vector2 reached = Globals.GetLinecastVector(origin, target, ChaosCollisions.layerAllWall);
            if ((reached - target).sqrMagnitude > 0.0001f)
                target = reached - direction * WallBackoff;
            wave.Center = target;
            wave.CinematicToken = cinematic;
            try
            {
                WaveFront.Launch(wave);
            }
            catch (System.Exception e)
            {
                TidalWallPlugin.Log($"Tidal Wall: couldn't launch the wave: {e.Message}");
                if (cinematic != 0)
                    Cinematic.Restore("the wave couldn't be launched", cinematic);
            }
        }

        public override void OnExit()
        {
            // Cut short during the signature's gather (hurt, death, a level load): the water drains
            // away and the camera and light go straight back.
            if (signature && gathering && !launched)
            {
                StopGatherEffect();
                Effects.DropRing((Vector2)parent.transform.position - direction * GatherBehind, GatherRadius * 0.5f, 2);
                string next = fsm != null && fsm.nextStateName != null ? fsm.nextStateName : "?";
                TidalWallPlugin.Log($"Tidal Wall: signature cancelled during the gather ({gatherTime:0.00} of {GatherTime:0.00} s, " +
                    $"next state {next}), the water drains away");
                Cinematic.Restore("cancelled during the gather", cinematic);
            }
            base.OnExit();
        }
    }
}
