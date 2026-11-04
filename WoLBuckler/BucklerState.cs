using UnityEngine;

namespace WoLBuckler
{
    // The game's melee combo (MeleeAttackState: the 3-hit chain, its timing and animations), with
    // no attack object of its own (skipAtkObjSpawn, as Flaming Whip): each punch is the game's
    // earth burst just in front of the wizard, with this arcana's stats, so gear, crits and
    // talents apply as to any basic. 0.4.0 (Matt: 'hit hold, hit hold, toss disk'): punch, the
    // buckler held up through a long pause, punch, held up again, then the buckler tossed like a
    // disc.
    public sealed class BucklerState : Player.MeleeAttackState
    {
        public new static string staticID = "BucklerBasic";

        // 0.3.0 (Matt: 'a longer delay than normal between hits'): was 0.08 / 0.11 with windows
        // about half these.
        private const float PunchExecTime = 0.2f, TossExecTime = 0.24f;

        public BucklerState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.03f;
            animExecTime = PunchExecTime;
            baseCancelThreshold = 0.45f;
            finalCancelThreshold = 0.55f;
            baseRunThreshold = 0.55f;
            finalRunThreshold = 0.65f;
            baseExitThreshold = 0.75f;
            finalExitThreshold = 0.90f;
            alwaysStartForehand = true;
            // Short reach: a step into each punch keeps the target in range; the toss is thrown
            // standing.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 3f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0f, 10, VarStatModType.OverrideWithMods);
            forceMovement = true;
            autoHoldStat.BaseValue = true;
            sfxNames = new[] { "WindSlashBasic", "WindSlashBasic", "WindSlashBasic" };
        }

        private bool executed;

        public override void OnEnter()
        {
            base.OnEnter();
            executed = false;
        }

        public override void PlayAnimation(int attackCount)
        {
            animExecTime = attackCount >= maxComboCount ? TossExecTime : PunchExecTime;
            base.PlayAnimation(attackCount);
            // Log - Art 09:05: the pull-back frame during a punch's wind-up, in the wizard's facing.
            if (attackCount < maxComboCount)
            {
                try
                {
                    Vector2 aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
                    BucklerGuard.Pull(parent, aim == Vector2.zero ? Vector2.right : aim, IsEmpowered);
                }
                catch (System.Exception e)
                {
                    BucklerPlugin.Log($"Buckler: wind-up look failed: {e.Message}");
                }
            }
        }

        // Once per hit (ExecuteAttack can come twice), as Flaming Whip.
        public override void ExecuteAttack()
        {
            if (executed)
                return;
            executed = true;
            base.ExecuteAttack();
            Vector2 aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            try
            {
                if (comboCounter >= maxComboCount)
                    BucklerDisc.Toss(parent, skillID, aim, IsEmpowered);
                else
                    Buckler.Punch(parent, skillID, aim, IsEmpowered);
            }
            catch (System.Exception e)
            {
                BucklerPlugin.Log($"Buckler: hit {comboCounter} failed: {e.Message}");
            }
        }
    }
}
