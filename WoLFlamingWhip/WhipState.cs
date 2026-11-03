using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLFlamingWhip
{
    // The game's melee combo (MeleeAttackState: the 3-hit chain, its timing and animations), with
    // no attack object of its own (skipAtkObjSpawn, as some of the game's basics do): each hit is
    // the game's fire bursts on the enemies the whip reaches, with this arcana's stats, so burn,
    // gear and crits apply as to any basic.
    public sealed class WhipState : Player.MeleeAttackState
    {
        public new static string staticID = "FlamingWhipBasic";

        public WhipState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.05f;
            animExecTime = 0.16f;
            baseCancelThreshold = 0.30f;
            finalCancelThreshold = 0.4f;
            baseRunThreshold = 0.45f;
            finalRunThreshold = 0.6f;
            baseExitThreshold = 0.65f;
            finalExitThreshold = 0.85f;
            alwaysStartForehand = true;
            // A whip keeps its distance: only a small step forward.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0.5f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0f, 10, VarStatModType.OverrideWithMods);
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic", "WindSlashBasic" };
        }

        private bool executed;
        private const float ExecTime = 0.16f, CrackExecTime = 0.24f;

        public override void OnEnter()
        {
            base.OnEnter();
            executed = false;
        }

        // #78: the crack gets a clear wind-up: the game's point-blank AoE pose (as its own basics
        // use for a ring finisher), held a little longer before the whip comes down.
        public override void PlayAnimation(int attackCount)
        {
            if (attackCount >= maxComboCount)
            {
                animExecTime = CrackExecTime;
                parent.anim.PlayDirectional(parent.PBAoEAnimStr, -1, animStartTime);
                return;
            }
            animExecTime = ExecTime;
            base.PlayAnimation(attackCount);
        }

        // Once per hit, as Momentum Spear.
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
                if (comboCounter >= maxComboCount)
                    Whip.Crack(parent, skillID, parent.transform.position);
                else
                    Whip.Lash(parent, skillID, hand, aim, IsEmpowered);
            }
            catch (System.Exception e)
            {
                FlamingWhipPlugin.Log($"Flaming Whip: hit {comboCounter} failed: {e.Message}");
            }
        }
    }
}
