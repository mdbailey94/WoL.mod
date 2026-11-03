using UnityEngine;

namespace WoLBuckler
{
    // The game's melee combo (MeleeAttackState: the 3-hit chain, its timing and animations), with
    // no attack object of its own (skipAtkObjSpawn, as Flaming Whip): each punch is the game's
    // earth burst just in front of the wizard, with this arcana's stats, so gear, crits and
    // talents apply as to any basic. Deliberate: a longer wind-up and pause than the game's basics.
    public sealed class BucklerState : Player.MeleeAttackState
    {
        public new static string staticID = "BucklerBasic";

        // 0.3.0 (Matt: 'a longer delay than normal between hits'): was 0.08 / 0.11 with windows
        // about half these.
        private const float PunchExecTime = 0.2f, FinisherExecTime = 0.26f;

        public BucklerState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            // 0.2.0 (Matt): a 2-hit combo, a punch and the heavier last punch.
            maxComboCount = 2;
            animStartTime = 0.03f;
            animExecTime = PunchExecTime;
            baseCancelThreshold = 0.45f;
            finalCancelThreshold = 0.55f;
            baseRunThreshold = 0.55f;
            finalRunThreshold = 0.65f;
            baseExitThreshold = 0.75f;
            finalExitThreshold = 0.90f;
            alwaysStartForehand = true;
            // Short reach: a step into each punch keeps the target in range.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 3f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 4f, 10, VarStatModType.OverrideWithMods);
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
            animExecTime = attackCount >= maxComboCount ? FinisherExecTime : PunchExecTime;
            base.PlayAnimation(attackCount);
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
                Buckler.Punch(parent, skillID, aim, comboCounter >= maxComboCount, IsEmpowered);
            }
            catch (System.Exception e)
            {
                BucklerPlugin.Log($"Buckler: punch {comboCounter} failed: {e.Message}");
            }
        }
    }
}
