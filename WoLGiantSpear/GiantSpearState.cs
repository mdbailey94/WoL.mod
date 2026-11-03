using UnityEngine;

namespace WoLGiantSpear
{
    // The game's melee combo (MeleeAttackState: the 3-hit chain, its timing and animations), with
    // no attack object of its own (skipAtkObjSpawn, as Flaming Whip): each stab is the game's wind
    // burst on the enemies the spear reaches, with this arcana's stats, so gear, crits and talents
    // apply as to any basic. Slow, as a giant weapon: a long wind-up on every hit, the throw
    // longest, and a late cancel. Gear activation speed shortens all of it.
    public sealed class GiantSpearState : Player.MeleeAttackState
    {
        public new static string staticID = "GiantAirSpearBasic";

        private const float StabExecTime = 0.24f, ThrowExecTime = 0.32f;

        public GiantSpearState(FSM fsm, Player player) : base(staticID, fsm, player)
        {
            isBasic = true;
            isMeleeSkill = true;
            skipAtkObjSpawn = true;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.05f;
            animExecTime = StabExecTime;
            baseCancelThreshold = 0.42f;
            finalCancelThreshold = 0.55f;
            baseRunThreshold = 0.55f;
            finalRunThreshold = 0.7f;
            baseExitThreshold = 0.75f;
            finalExitThreshold = 0.95f;
            alwaysStartForehand = true;
            // A heavy step into each stab; the throw is made standing.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 1.5f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0f, 10, VarStatModType.OverrideWithMods);
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
            animExecTime = attackCount >= maxComboCount ? ThrowExecTime : StabExecTime;
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
            Vector2 hand = parent.attackOriginTrans != null ? (Vector2)parent.attackOriginTrans.position : (Vector2)parent.transform.position;
            try
            {
                if (comboCounter >= maxComboCount)
                    ThrownSpear.Throw(parent, skillID, hand, aim, IsEmpowered);
                else
                    Spear.Stab(parent, skillID, hand, aim, IsEmpowered);
            }
            catch (System.Exception e)
            {
                GiantSpearPlugin.Log($"Giant Air Spear: hit {comboCounter} failed: {e.Message}");
            }
        }
    }
}
