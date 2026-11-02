using UnityEngine;

namespace WoLMomentumSpear
{
    public sealed class SpearState : Player.MeleeAttackState
    {
        public new static string staticID = "MomentumSpearBasic";
        private float openingMultiplier = 1f;
        private bool executed;
        private Attack preparedAttack;
        private float pendingMultiplier;
        private int pendingFrame;
        private bool pendingCapture;

        public SpearState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            attackPrefab = ChaosBundle.Get("Assets/Prefabs/PlayerSkills/IceDaggerBasic.prefab");
            setParent = true;
            destroyAttackObjectOnExit = true;
            maxComboCount = 3;
            animStartTime = 0.05f;
            animExecTime = 0.16f;
            baseCancelThreshold = 0.30f;
            finalCancelThreshold = 0.38f;
            baseRunThreshold = 0.45f;
            finalRunThreshold = 0.55f;
            baseExitThreshold = 0.65f;
            finalExitThreshold = 0.8f;
            baseAttackScale = penultimateAttackScale = 1f;
            finalAttackScale = 1.25f;
            flipObjectForBackhand = false;
            alwaysStartForehand = true;
            // A melee basic has to lunge: without it the first hit's knockback leaves the target out
            // of the next thrust's reach (0.1.0 used a ranged basic's 0.5, and hit 2 whiffed).
            // forceMovement lunges along the aim even with the mouse close to the wizard.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, SpearPlugin.Lunge, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, SpearPlugin.FinisherLunge, 10, VarStatModType.OverrideWithMods);
            forceMovement = true;
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic", "WindSlashBasic" };
        }

        internal void CaptureMomentum()
        {
            Vector2 velocity = parent != null && parent.rigidbody2D != null ? parent.rigidbody2D.velocity : Vector2.zero;
            pendingMultiplier = SpearRules.MomentumMultiplier(velocity.x, velocity.y, SpearPlugin.BonusPerSpeedUnit, SpearPlugin.MomentumCap);
            pendingFrame = Time.frameCount;
            pendingCapture = true;
        }

        public override void OnEnter()
        {
            if (!pendingCapture || pendingFrame != Time.frameCount) CaptureMomentum();
            float captured = pendingMultiplier;
            pendingCapture = false;
            base.OnEnter(); // Native code ends movement and resolves/reset the current combo step.
            openingMultiplier = comboCounter == 1 ? captured : 1f;
            preparedAttack = null;
            executed = false;
        }

        public override void ExecuteAttack()
        {
            if (executed) return;
            executed = true;
            base.ExecuteAttack();
            SpearPlugin.Trace($"thrust {comboCounter} of {maxComboCount}: level {currentLevel}, opening x{openingMultiplier:0.00}, t={Time.time:0.00}");
        }

        public override void HandleComboCount(int givenCount)
        {
            base.HandleComboCount(givenCount);
            // The native prefab points along local +Y; the base state rotates it to the aim.
            calcScaleVector = new Vector3(SpearPlugin.WidthScale,
                SpearPlugin.ReachScale * (IsEmpowered ? 1.15f : 1f), 1f);
            if (currentAttackObject == null || currentAttackObject.attack == null) return;
            // Switched off before its Start: IceDagger's script only adds ice shards at the start
            // and end of the attack. Its animator and hit box carry on, and OnExit destroys it.
            currentAttackObject.enabled = false;
            Attack attack = currentAttackObject.attack;
            if (preparedAttack == attack) return;
            preparedAttack = attack;
            // Attack.ExecuteAttack applies this multiplier to each target's fresh AttackInfo.
            // It preserves normal damage/crit/basic-item handling and does not edit shared stats.
            if (givenCount == 1) attack.damageModifier *= openingMultiplier;
            SpearLook.Attach(currentAttackObject, SpearRules.Step(givenCount), openingMultiplier);
        }
    }
}
