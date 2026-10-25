using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge (Earth, standard arcana): a shield crash you have to run up to.
    //
    // Cast: a quick slam raises a stone shield (Aegis Charge's) a unit in front of you, shoving
    // whatever's there (level 1). Then, for up to ChargeTime, you're free to run about behind it
    // (the game's run speed, working up to a sprint after SprintDelay), and every moment on the
    // move stores force in the shield in proportion to your run speed against its base (sprint,
    // speed relics, the cloak and speed buffs count, the speeds a dash forces for a moment don't):
    // standing still stores nothing. The shield turns to face the way you run, hits from the
    // front do less (FrontArmour) and you aren't staggered (Brace), but you still take the damage,
    // and you can't cast anything else; a dash drops the shield and all the force with it.
    //
    // Press the button again, run the shield into an enemy, or let the time run out to unload,
    // with whatever force is stored by then: a short rush forward
    // (RushDistance) bashing with the shield (level 2) and a crash where it ends (level 3), both
    // shoving along your way. Their damage and knockback scale with the stored force, 1x at none up
    // to FullMultiplier at full, and the crash stuns longer from StunEnergy up. An instant crash
    // is weak; a crash after a long sprint is not.
    public class StoneChargeState : Player.SkillState
    {
        public new static string staticID = "StoneCharge";

        // Stat levels of the three hits (see the plugin's skillStats).
        private const int RaiseLevel = 1;
        private const int BashLevel = 2;
        private const int CrashLevel = 3;

        // Where the hits land, ahead of the wizard along the rush.
        private const float BashOffset = 1f;    // the shield's spot
        private const float CrashOffset = 1f;
        // Seconds on the ground after the crash before you can act.
        private const float RecoverTime = 0.2f;
        // The game's run speed-up (+40%), given back after SprintDelay on the move.
        private const float SprintBonus = 0.4f;
        private const string SprintModID = "StoneCharge_Sprint";
        private const string ArmourModID = "StoneCharge_Armour";
        // A hit counts as "from the front" within 60 degrees of the way you're facing.
        private const float FrontCos = 0.5f;
        // Energy is logged (and the shield shows it) at these steps.
        private const float MilestoneStep = 25f;
        // Energy look: rock debris round the wizard every so often, floor spikes behind them when
        // moving at half force or more (the Charge arcana's), a rock aura from three quarters.
        private const float EffectInterval = 0.12f;
        private const float SpikeInterval = 0.125f;
        private const float InputDeadZone = 0.0016f;
        // Running the shield into an enemy crashes at once: anything this close to the shield's
        // spot while you're on the move counts.
        private const float ContactRadius = 0.9f;

        private enum Phase { Raising, Charging, Rushing, Recovering, Done }

        private Phase phase;
        private float time;             // seconds since the shield went up
        private float energy;           // 0-100
        private float moveTime;         // seconds on the move without stopping
        private float lastSpeed = 1f;   // the latest speed read (x base run speed)
        private float peakSpeed;
        private int milestones;
        private bool wasMoving;
        private bool sprinting;
        private float nextEffect, nextSpike;
        private Vector2 aim;            // where the shield points and the rush goes
        private StoneShield shield;
        private bool braced, armourOn, listening, floorToggled;
        private int frontHits, otherHits;
        private bool unloaded;
        private bool loggedHits;
        // The rush.
        private Vector2 rushDir;
        private float rushSpeed, rushTime, recoverTime;
        private bool bashedMid;
        private float force;            // energy / 100 at the unload
        // What the hits caught.
        private int bashHits, crashHits;
        private int crashDamage;
        private readonly List<string> hitNames = new List<string>();
        private static readonly HashSet<string> failed = new HashSet<string>();

        public override string OnEnterAnimStr => parent.GSlamAnimStr;

        public StoneChargeState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            isMovementSkill = true;
            isMeleeSkill = true;
            applyStopElementStatus = true;
            // The cooldown counts from the end (the crash or a cancel), like the Charge arcana's.
            startCooldownOnEnter = false;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.3f,  // execute: the slam lands and the shield rises
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();
            phase = Phase.Raising;
            time = 0f;
            energy = 0f;
            moveTime = 0f;
            lastSpeed = peakSpeed = 1f;
            milestones = 0;
            wasMoving = false;
            sprinting = false;
            nextEffect = nextSpike = 0f;
            shield = null;
            braced = armourOn = listening = floorToggled = false;
            frontHits = otherHits = 0;
            unloaded = loggedHits = false;
            bashedMid = false;
            force = 0f;
            bashHits = crashHits = crashDamage = 0;
            hitNames.Clear();
            // The shield goes up the way you're aiming (or facing).
            aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            parent.movement.EndMovement();
        }

        // The base Update runs ExecuteSkill once the animation passes its execute time; while the
        // shield is up the run/idle animations loop, so the phases are driven from here instead.
        public override void Update()
        {
            UpdateRepeatLock(string.Empty);
            if (skillExecuted)
            {
                ConcludeSkill();
                return;
            }
            if (phase == Phase.Raising && !parent.anim.AnimPlayed(animExecTime))
            {
                // The wind-up can be dashed out of, like any slam.
                CancelToDash(false);
                return;
            }
            ExecuteSkill();
        }

        public override void ExecuteSkill()
        {
            switch (phase)
            {
                case Phase.Raising:
                    RaiseShield();
                    phase = Phase.Charging;
                    Charge();
                    break;
                case Phase.Charging:
                    Charge();
                    break;
                case Phase.Rushing:
                    Rush();
                    break;
                case Phase.Recovering:
                    Recover();
                    break;
                default:
                    base.ExecuteSkill();
                    break;
            }
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();
            if (parent.rigidbody2D == null)
                return;
            if (phase == Phase.Rushing)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            else if (phase == Phase.Recovering)
                parent.rigidbody2D.velocity = Vector2.zero;
        }

        public override void OnExit()
        {
            StopSprint();
            SetArmour(false);
            StopListening();
            if (braced)
            {
                braced = false;
                try { parent.Endure(); } catch { }
            }
            if (floorToggled)
            {
                floorToggled = false;
                try { parent.ToggleEnemyFloorCollisions(); } catch { }
            }
            if (shield != null)
            {
                shield.Drop();
                shield = null;
            }
            if (!unloaded)
            {
                string next = fsm.nextStateName ?? "";
                string why = next.Contains("Dash") ? "dashed" : next.Contains("Dead") ? "died" : $"left for {next}";
                if (phase == Phase.Raising)
                    Log($"cancelled ({why}) before the shield went up");
                else
                    Log($"shield dropped ({why}) after {time:0.0} s with {energy:0}% force lost{HitsTaken()}");
            }
            else if (!loggedHits)
                LogHits();
            base.OnExit();
        }

        // ---- The shield ----

        private void RaiseShield()
        {
            Vector2 at = parent.attackOriginTrans.position;
            shield = StoneShield.Raise(parent, skillID, aim);
            // Its Start() fires the level-1 shove burst at the shield's spot a frame later; count
            // what it hits through the game's attack history instead: cheap and good enough.
            if (StoneChargePlugin.Brace)
            {
                try
                {
                    parent.Endure(ignoreKnockback: true, ignoreHurtState: true, faceHurtDirection: false);
                    braced = true;
                }
                catch (System.Exception e)
                {
                    LogOnce("brace", $"couldn't brace against hits: {e.Message}");
                }
            }
            if (StoneChargePlugin.FrontArmourPercent > 0f && parent.health != null)
            {
                parent.health.takeDamageEnterHandlers += OnHurt;
                listening = true;
            }
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(20, 0.75f, -1f, -1f, new Vector3?(parent.transform.position), null));
            Effect("shake", () => CameraController.ShakeCamera(0.25f, false));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosion", new Vector2?(at), null, 24f, -1f, 1.1f, false));

            float rate = StoneChargePlugin.EnergyPerSecond * (IsEmpowered ? 1f + StoneChargePlugin.EnhancedEnergyBonus / 100f : 1f);
            Log($"cast{(IsEmpowered ? " (enhanced)" : "")}: shield up for {StoneChargePlugin.ChargeTime:0.#} s, " +
                $"force builds {rate:0}%/s at base run speed (full in {100f / rate:0.0} s at x1.00, {100f / (rate * (1f + SprintBonus)):0.0} s sprinting); " +
                $"run into a foe or press again to crash{(StoneChargePlugin.FrontArmourPercent > 0f ? $", {StoneChargePlugin.FrontArmourPercent:0}% less from hits in front" : "")}" +
                (braced ? ", braced" : ""));
        }

        // Every frame the shield is up: run about, store force, watch for the second press.
        private void Charge()
        {
            if (CancelToDash(false))
                return;
            float dt = Time.deltaTime;
            time += dt;

            Vector2 input = GetInputVector(false, false, false);
            bool moving = input.sqrMagnitude > InputDeadZone;
            Vector3 here = parent.transform.position;
            if (moving)
            {
                parent.movement.moveVector = input;
                parent.movement.MoveToMoveVector();
                if (!wasMoving)
                {
                    parent.FaceTarget(here + (Vector3)input);
                    parent.anim.PlayDirectional("Run" + parent.facingDirection);
                }
                else
                    parent.PlayAnimOnFaceDirChange("Run", here + (Vector3)input);
                aim = input.normalized;
                moveTime += dt;
                if (!sprinting && moveTime >= StoneChargePlugin.SprintDelay)
                    StartSprint();
                lastSpeed = SpeedFactor();
                peakSpeed = Mathf.Max(peakSpeed, lastSpeed);
                float rate = StoneChargePlugin.EnergyPerSecond * (IsEmpowered ? 1f + StoneChargePlugin.EnhancedEnergyBonus / 100f : 1f);
                energy = Mathf.Min(100f, energy + dt * rate * lastSpeed);
            }
            else
            {
                parent.movement.EndMovement();
                if (wasMoving || time <= dt)
                    parent.anim.PlayDirectional("Idle" + parent.facingDirection);
                moveTime = 0f;
                StopSprint();
            }
            wasMoving = moving;
            shield?.Aim(aim);
            Milestones();
            ChargeEffects(moving);

            Enemy struck;
            if (time >= StoneChargePlugin.MinPressTime && skillSlot >= 0 && parent.inputDevice.GetButtonDown("Skill" + skillSlot))
                Unload("press");
            else if (moving && time >= StoneChargePlugin.MinPressTime && ShieldTouches(here, out struck))
                Unload("ran into " + struck.name);
            else if (time >= StoneChargePlugin.ChargeTime)
                Unload("timeout");
        }

        // A live enemy (not an ally) touching the shield's spot ahead of the wizard.
        private bool ShieldTouches(Vector2 here, out Enemy struck)
        {
            struck = null;
            try
            {
                foreach (Collider2D col in Physics2D.OverlapCircleAll(here + aim * BashOffset, ContactRadius))
                {
                    Enemy enemy = col != null ? col.GetComponentInParent<Enemy>() : null;
                    if (enemy == null || !enemy.gameObject.activeInHierarchy)
                        continue;
                    if (enemy.health == null || enemy.health.CurrentHealthValue <= 0)
                        continue;
                    if (GameController.allies != null && GameController.allies.Contains(enemy.gameObject))
                        continue;
                    struck = enemy;
                    return true;
                }
            }
            catch (System.Exception e)
            {
                LogOnce("contact", $"couldn't check for enemies at the shield: {e.Message}");
            }
            return false;
        }

        private void Milestones()
        {
            int reached = Mathf.FloorToInt(energy / MilestoneStep + 0.0001f);
            while (milestones < reached)
            {
                milestones++;
                float percent = milestones * MilestoneStep;
                Log($"force {percent:0}% after {time:0.0} s (speed x{lastSpeed:0.00}{(sprinting ? ", sprinting" : "")}" +
                    $"{(IsEmpowered ? ", enhanced" : "")})");
                Vector3 at = parent.transform.position;
                int step = milestones;
                Effect("form", () => PoolManager.GetPoolItem<ParticleEffect>("RockFormEffect").Emit(new int?(4 + 3 * step), new Vector3?(at), RockFormEffect.SmallSettings, null, 0f, null, null));
                Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthPebble", new Vector2?(at), null, 24f, -1f, 0.8f + 0.15f * step, false));
                if (percent >= 100f)
                {
                    Effect("shake", () => CameraController.ShakeCamera(0.15f, false));
                    Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthDust", new Vector2?(at), null, 24f, -1f, 1f, false));
                }
            }
        }

        // The look of the force building: rock debris round the wizard, more and wider as it
        // builds; from half force the ground cracks and spikes behind them as they run (the Charge
        // arcana's spikes, no hit); at full a steady rock aura.
        private void ChargeEffects(bool moving)
        {
            if (time >= nextEffect)
            {
                nextEffect = time + EffectInterval;
                Vector3 at = parent.transform.position;
                if (energy >= MilestoneStep)
                {
                    int count = 1 + Mathf.FloorToInt(energy / MilestoneStep);
                    float spread = 0.2f + 0.3f * energy / 100f;
                    Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(count),
                        new Vector3?(at + (Vector3)(Random.insideUnitCircle * spread)), RockDebrisEmitter.SmallCircleSlowExplosionOverride, null, 0f, null));
                }
                if (energy >= 100f)
                    Effect("form", () => PoolManager.GetPoolItem<ParticleEffect>("RockFormEffect").Emit(new int?(2), new Vector3?(at), RockFormEffect.SmallSettings, null, 0f, null, null));
            }
            if (moving && energy >= 2f * MilestoneStep && time >= nextSpike)
            {
                nextSpike = time + SpikeInterval * (energy >= 3f * MilestoneStep ? 0.7f : 1f);
                SpawnSpike();
            }
        }

        // The Charge arcana's floor spikes, as it spawns them: a cosmetic spike behind the wizard.
        private void SpawnSpike()
        {
            Effect("spike", () =>
            {
                Vector3 behind = parent.transform.position + (Vector3)(Random.insideUnitCircle * 0.25f) - (Vector3)aim * 0.5f;
                EarthFloorSpike spike = Object.Instantiate(EarthFloorSpike.Prefab, behind, Quaternion.identity).GetComponent<EarthFloorSpike>();
                spike.breakPrimaryParticleCount = 3;
                spike.breakSeconaryParticleCount = 2;
                spike.spawnParticleCount = 3;
                spike.useFloorCollider = false;
                spike.shakeOnSpawn = false;
                spike.Activate(useAttackBox: false, useFloorCollider: false);
            });
        }

        // ---- Speed ----

        // The game's run speed-up, which its Run state drops the moment you cast: given back after
        // SprintDelay on the move behind the shield, and lost again when you stop.
        private void StartSprint()
        {
            try
            {
                parent.movement.moveSpeedStat.RemoveMod(SprintModID);
                parent.movement.moveSpeedStat.AddMod(new NumVarStatMod(SprintModID, SprintBonus, 10, VarStatModType.LateMultiplicative));
                sprinting = true;
                Effect("sound", () => SoundManager.PlayWithDistAndSPR("RunSpeed", parent.transform.position));
                Log($"sprinting (+{SprintBonus * 100f:0}% run speed) after {moveTime:0.0} s on the move; run speed now {parent.movement.moveSpeedStat.CurrentValue:0.0} u/s");
            }
            catch (System.Exception e)
            {
                LogOnce("sprint", $"couldn't apply the sprint: {e.Message}");
            }
        }

        private void StopSprint()
        {
            if (!sprinting)
                return;
            sprinting = false;
            try { parent.movement?.moveSpeedStat?.RemoveMod(SprintModID); } catch { }
        }

        // The wizard's run speed against its base (1 = base, 1.4 sprinting, more with speed relics,
        // the cloak and speed buffs). Counts the stat's lasting modifiers the way the game does
        // (multiplied, then added, then late-multiplied) but not the Override ones: those are the
        // speeds a dash or an attack forces on the wizard for a moment. (Trailblazer's read.)
        private static bool loggedSpeed;

        private float SpeedFactor()
        {
            float factor = 1f;
            try
            {
                NumVarStat stat = parent.movement != null ? parent.movement.moveSpeedStat : null;
                if (stat != null && stat.BaseValue > 0.01f)
                {
                    float speed = stat.BaseValue;
                    int overrides = 0;
                    if (stat.modifiers != null)
                    {
                        foreach (VarStatMod<float> mod in stat.modifiers)
                        {
                            NumVarStatMod numMod = mod as NumVarStatMod;
                            if (numMod == null)
                                continue;
                            switch (numMod.modType)
                            {
                                case VarStatModType.Additive:
                                    speed += numMod.modValue;
                                    break;
                                case VarStatModType.Multiplicative:
                                case VarStatModType.LateMultiplicative:
                                    speed *= 1f + numMod.modValue;
                                    break;
                                case VarStatModType.Override:
                                case VarStatModType.OverrideWithMods:
                                    overrides++;
                                    break;
                            }
                        }
                    }
                    factor = speed / stat.BaseValue;
                    if (!loggedSpeed)
                    {
                        loggedSpeed = true;
                        Log($"run speed {speed:0.##} of base {stat.BaseValue:0.##} (x{factor:0.00}; the stat reads {stat.ModifiedValue:0.##}, {overrides} override mod(s) ignored)");
                    }
                }
            }
            catch
            {
            }
            return Mathf.Clamp(factor, 0.5f, 6f);
        }

        // ---- Taking hits behind the shield ----

        // Just before the game works out a hit on the wizard: hits from in front get the armour,
        // the rest don't. (The armour stat is read after this is announced.)
        private void OnHurt(AttackInfo info, Entity attacker)
        {
            try
            {
                if (info == null || info.isStatusEffect)
                    return;
                Vector2 from = info.attackerPosition - (Vector2)parent.transform.position;
                bool front = from.sqrMagnitude > 0.0001f && Vector2.Dot(from.normalized, aim) >= FrontCos;
                SetArmour(front);
                if (front)
                {
                    frontHits++;
                    if (frontHits == 1)
                        Log($"blocked a hit from the front with the shield ({StoneChargePlugin.FrontArmourPercent:0}% less damage)");
                }
                else
                    otherHits++;
            }
            catch
            {
            }
        }

        private void SetArmour(bool on)
        {
            if (on == armourOn || parent.health == null || parent.health.armorStat == null)
                return;
            try
            {
                if (on)
                    parent.health.armorStat.AddMod(new NumVarStatMod(ArmourModID, StoneChargePlugin.FrontArmourPercent / 100f, 10, VarStatModType.Additive));
                else
                    parent.health.armorStat.RemoveMod(ArmourModID);
                armourOn = on;
            }
            catch (System.Exception e)
            {
                LogOnce("armour", $"couldn't set the shield's armour: {e.Message}");
            }
        }

        private void StopListening()
        {
            if (!listening)
                return;
            listening = false;
            try { if (parent.health != null) parent.health.takeDamageEnterHandlers -= OnHurt; } catch { }
        }

        private string HitsTaken() =>
            frontHits + otherHits > 0 ? $"; took {frontHits} hit(s) on the shield, {otherHits} from the sides or behind" : "";

        // ---- The crash ----

        private int ScaledDamage(int baseDamage) =>
            Mathf.Max(baseDamage > 0 ? 1 : 0, Mathf.RoundToInt(baseDamage * (1f + (StoneChargePlugin.FullMultiplier - 1f) * force)));

        private float ScaledKnockback => Mathf.Lerp(StoneChargePlugin.Knockback, StoneChargePlugin.FullKnockback, force);

        private bool Stuns => force * 100f >= StoneChargePlugin.StunEnergyPercent - 0.001f;

        // The second press or the timeout: commit to the rush.
        private void Unload(string trigger)
        {
            phase = Phase.Rushing;
            unloaded = true;
            force = Mathf.Clamp01(energy / 100f);
            rushDir = aim;
            rushTime = 0f;
            bashedMid = false;
            StopSprint();
            parent.FaceTarget(parent.transform.position + (Vector3)rushDir);
            parent.movement.EndMovement();
            parent.movement.moveVector = rushDir;
            rushSpeed = StoneChargePlugin.RushDistance / StoneChargePlugin.RushTime;
            try
            {
                // Rush through enemies rather than getting stuck on them (Aegis Charge does this).
                parent.ToggleEnemyFloorCollisions(false);
                floorToggled = true;
            }
            catch
            {
            }
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            parent.anim.PlayDirectional(parent.SlideAnimStr, -1, 0.25f);

            AttackInfo bashInfo = AttackInfo.GetInfo(parent, parent.skillCategory, skillID, BashLevel, isUltimate);
            AttackInfo crashInfo = AttackInfo.GetInfo(parent, parent.skillCategory, skillID, CrashLevel, isUltimate);
            int bash = bashInfo != null ? ScaledDamage(bashInfo.damage) : -1;
            int crash = crashInfo != null ? ScaledDamage(crashInfo.damage) : -1;
            Log($"unload ({trigger}) at {energy:0}% force after {time:0.0} s (speed x{lastSpeed:0.00}, peak x{peakSpeed:0.00}): " +
                $"rush {StoneChargePlugin.RushDistance:0.#} u, bash {bash} + crash {crash} damage (x{1f + (StoneChargePlugin.FullMultiplier - 1f) * force:0.0}), " +
                $"knockback {ScaledKnockback:0}{(Stuns ? $", stun x{StoneChargePlugin.StunMultiplier:0.#}" : "")}, " +
                $"crash radius {(IsEmpowered ? StoneChargePlugin.EnhancedCrashRadius : StoneChargePlugin.CrashRadius):0.0}{HitsTaken()}");

            Vector2 at = parent.transform.position;
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthCrumble", new Vector2?(at), null, 24f, -1f, 1.1f, false));
            Effect("debris", () => PoolManager.GetPoolItem<RockBurstDirectionalEffect>("RockBurstDirectionalEffect").EmitSingle(RockDebrisType.Default,
                new int?(3 + Mathf.RoundToInt(5f * force)), new Vector3?(at + rushDir * BashOffset), null, new Vector3?(Globals.GetRotationVector(rushDir)), 0f, null));
            Bash();
        }

        private void Rush()
        {
            rushTime += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = rushDir * rushSpeed;
            if (!bashedMid && rushTime >= StoneChargePlugin.RushTime * 0.5f)
            {
                bashedMid = true;
                Bash();
            }
            if (rushTime >= StoneChargePlugin.RushTime)
                Crash();
        }

        // The shield bash (level 2) at the shield's spot: a quiet burst (the shield is the look).
        private void Bash()
        {
            Vector2 at = (Vector2)parent.attackOriginTrans.position + rushDir * BashOffset;
            try
            {
                EarthBurst burst = EarthBurst.CreateBurst(at, parent.skillCategory, skillID, BashLevel, StoneChargePlugin.BashRadius);
                burst.skipParticleEmit = true;
                Scale(burst.attack, false);
                burst.attack.entityCollisionEventHandlers += OnBashHit;
            }
            catch (System.Exception e)
            {
                LogOnce("bash", $"couldn't make the bash burst: {e.Message}");
            }
        }

        // The crash (level 3) where the rush ends, a unit ahead.
        private void Crash()
        {
            phase = Phase.Recovering;
            recoverTime = 0f;
            parent.movement.EndMovement();
            float radius = IsEmpowered ? StoneChargePlugin.EnhancedCrashRadius : StoneChargePlugin.CrashRadius;
            Vector2 at = (Vector2)parent.attackOriginTrans.position + rushDir * CrashOffset;
            try
            {
                EarthBurst burst = EarthBurst.CreateBurst(at, parent.skillCategory, skillID, CrashLevel, radius);
                Scale(burst.attack, true);
                crashDamage = burst.attack.atkInfo != null ? burst.attack.atkInfo.damage : 0;
                burst.attack.entityCollisionEventHandlers += OnCrashHit;
            }
            catch (System.Exception e)
            {
                LogOnce("crash", $"couldn't make the crash burst: {e.Message}");
            }
            if (shield != null)
            {
                shield.Drop();
                shield = null;
            }
            if (floorToggled)
            {
                floorToggled = false;
                try { parent.ToggleEnemyFloorCollisions(); } catch { }
            }
            Effect("dust", () => PoolManager.GetPoolItem<DustEmitter>().EmitCircle(30 + Mathf.RoundToInt(40f * force), radius * 0.5f, -6f, -1f, new Vector3?(at), null));
            Effect("debris", () => PoolManager.GetPoolItem<RockDebrisEmitter>().EmitSingle(RockDebrisType.Default, new int?(6 + Mathf.RoundToInt(10f * force)),
                new Vector3?(at), RockDebrisEmitter.FloorExplosionOverrideR1p5, null, 0f, null));
            Effect("shake", () => CameraController.ShakeCamera(0.4f + 0.8f * force, false));
            if (force >= 0.999f)
            {
                try { TimeScaleController.FreezeForFrames(3, false); } catch { }
            }
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthExplosionShort", new Vector2?(at), null, 24f, -1f, 1.1f - 0.3f * force, false));
            Effect("sound", () => SoundManager.PlayAudioWithDistance("EarthPebble", new Vector2?(at), null, 24f, -1f, 1f, false));
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, 0.3f);
        }

        private void Recover()
        {
            recoverTime += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (recoverTime < RecoverTime)
                return;
            phase = Phase.Done;
            LogHits();
            // Past the run and exit thresholds, so ExitToSkillOrRunOrIdle lets you go at once.
            parent.anim.PlayDirectional(parent.GSlamAnimStr, -1, 0.65f);
            base.ExecuteSkill();
        }

        // Scales a bash or crash hit by the stored force, before its collider touches anything:
        // damage 1x to FullMultiplier, knockback Knockback to FullKnockback along the rush, and the
        // crash's hitstun from StunEnergy up. (The game's stats hold the base numbers.)
        private void Scale(Attack attack, bool crash)
        {
            attack.knockbackOverwriteVector = rushDir;
            AttackInfo info = attack.atkInfo;
            if (info == null)
                throw new System.Exception("the burst has no attack info");
            info.damage = ScaledDamage(info.damage);
            info.knockbackMultiplier = ScaledKnockback;
            if (crash && Stuns)
                info.hitStunDurationModifier *= StoneChargePlugin.StunMultiplier;
        }

        private void OnBashHit(Entity hit)
        {
            bashHits++;
            Remember(hit);
        }

        private void OnCrashHit(Entity hit)
        {
            crashHits++;
            Remember(hit);
        }

        private void Remember(Entity hit)
        {
            if (hitNames.Count < 6)
                hitNames.Add(hit != null ? hit.name : "?");
        }

        private void LogHits()
        {
            if (loggedHits)
                return;
            loggedHits = true;
            if (bashHits + crashHits == 0)
            {
                Log($"crash hit nothing ({energy:0}% force, {crashDamage} damage waiting)");
                return;
            }
            string names = string.Join(", ", hitNames.ToArray()) + (bashHits + crashHits > hitNames.Count ? ", ..." : "");
            Log($"crash: bash hit {bashHits}, crash hit {crashHits} for {crashDamage} damage each before armour ({names})");
        }

        // ---- Helpers ----

        // Runs a cosmetic effect; if it's unavailable it's skipped (and logged once) instead of
        // stopping the cast.
        private static void Effect(string name, System.Action effect)
        {
            if (failed.Contains(name))
                return;
            try
            {
                effect();
            }
            catch (System.Exception e)
            {
                failed.Add(name);
                Log($"effect '{name}' unavailable, skipping it: {e.Message}");
            }
        }

        public static void Log(string message) => StoneChargePlugin.Log("Stone Charge: " + message);

        public static void LogOnce(string key, string message)
        {
            if (failed.Add("log:" + key))
                Log(message);
        }
    }
}
