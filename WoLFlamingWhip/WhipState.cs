using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLFlamingWhip
{
    // 0.8.0 (Matt 2026-11-04 13:13): a 2-hit combo, the second crack wider. Was #81 (Matt 12:00):
    // ONE big crack per press, no combo. The game's melee state runs the press
    // (its timing, autohold and the basic slot) with no attack object of its own
    // (skipAtkObjSpawn, as some of the game's basics do). The press marks the spot WindUp seconds
    // ahead of the crack: the wizard winds the whip up overhead, slowed for a heavy feel (#86),
    // and the crack lands on a 1-tile spot exactly 2 tiles out along the aim (Whip.Crack).
    public sealed class WhipState : Player.MeleeAttackState
    {
        public new static string staticID = "FlamingWhipBasic";

        private const string SlowModID = "FlamingWhip_WindUp";
        private bool executed, slowed;
        private NumVarStatMod slowMod;

        public WhipState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            // 0.8.0 (Matt 13:13): a 2-hit combo; the second crack has the bigger area.
            maxComboCount = 2;
            animStartTime = 0.05f;
            animExecTime = 0.1f;
            // The wizard stays in the swing through the wind-up and the crack.
            baseCancelThreshold = finalCancelThreshold = 0.8f;
            baseRunThreshold = finalRunThreshold = 0.9f;
            baseExitThreshold = finalExitThreshold = 1f;
            alwaysStartForehand = true;
            // Planted: no step forward.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = attackMoveSpeedMod;
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic" };
        }

        public override void OnEnter()
        {
            base.OnEnter();
            executed = false;
            Slow(true);
        }

        public override void OnExit()
        {
            Slow(false);
            base.OnExit();
        }

        // #86: the wind-up is slow and deliberate (the wizard's animation runs at WindUpPace).
        private void Slow(bool on)
        {
            try
            {
                if (parent == null || parent.animSpeedHandler == null || on == slowed)
                    return;
                if (slowMod == null)
                    slowMod = new NumVarStatMod(SlowModID, FlamingWhipPlugin.WindUpPace - 1f, 10, VarStatModType.Multiplicative);
                if (on)
                    slowMod.modValue = FlamingWhipPlugin.WindUpPace - 1f;   // read each press (#101 cleanup)
                parent.animSpeedHandler.Modify(slowMod, on);
                slowed = on;
            }
            catch (System.Exception e)
            {
                FlamingWhipPlugin.Log($"Flaming Whip: the wind-up pace failed: {e.Message}");
            }
        }

        // The wind-up pose: the game's point-blank AoE pose, arms up (as its own basics use for a
        // ring finisher).
        public override void PlayAnimation(int attackCount)
        {
            parent.anim.PlayDirectional(parent.PBAoEAnimStr, -1, animStartTime);
        }

        // Once per press, as Momentum Spear.
        public override void ExecuteAttack()
        {
            if (executed)
                return;
            executed = true;
            base.ExecuteAttack();
            Vector2 aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            Vector2 hand = parent.attackOriginTrans != null ? (Vector2)parent.attackOriginTrans.position : (Vector2)parent.transform.position;
            try
            {
                Whip.WindUp(parent, skillID, hand, aim, IsEmpowered, comboCounter >= 2);
            }
            catch (System.Exception e)
            {
                FlamingWhipPlugin.Log($"Flaming Whip: the crack failed: {e.Message}");
            }
        }
    }
}
