using UnityEngine;

namespace WoLSkybreaker
{
    // The cast: a quick forehand that marks the spot Range ahead along the aim. The bolt itself is
    // a SkyStrike in the scene, so the wizard is free again at once while it gathers and lands.
    public class SkybreakerState : Player.SkillState
    {
        public new static string staticID = "Skybreaker";

        private bool signature;
        private bool fired;
        private Vector2 aim;

        public override string OnEnterAnimStr => parent.ForehandAnimStr;

        public SkybreakerState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.3f,  // execute: the hand comes forward and the spot is marked
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // The game sets isUltimate for a signature cast and clears it once the state exits.
            signature = isUltimate;
            fired = false;
            aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
            parent.movement.EndMovement();
        }

        public override void ExecuteSkill()
        {
            if (!fired)
            {
                fired = true;
                Vector2 from = parent.attackOriginTrans != null ? (Vector2)parent.attackOriginTrans.position : (Vector2)parent.transform.position;
                Vector2 spot = from + aim * SkybreakerPlugin.Range;
                SkyStrike.Call(parent, skillID, spot, signature ? SkyStrike.Kind.Signature : IsEmpowered ? SkyStrike.Kind.Enhanced : SkyStrike.Kind.Standard);
            }
            base.ExecuteSkill();
        }
    }
}
