using System.Collections.Generic;
using UnityEngine;

namespace WoLStoneCharge
{
    // Stone Charge (Earth, standard arcana). Since 0.6.0 (Matt) a throw: the wizard hurls a big
    // stone shield forward, and after a beat in the air it slams down in an earth blast
    // (ThrownShield, which carries on by itself, so the wizard is free again in a moment).
    // Enhanced: a bigger shield, a wider blast, a shorter wait. Signature (isUltimate, the game's
    // flag for a signature cast): a huge shield, a cinematic slam and a shockwave ring. Up to
    // 0.5.1 it was a charge (a shield raised and run behind, storing force, crashing at the end),
    // its signature a phalanx of shields.
    public class StoneChargeState : Player.SkillState
    {
        public new static string staticID = "StoneCharge";

        private const float CastTime = 0.22f;   // the throw; dash to cancel it (the shield flies on)

        private Vector2 aim;
        private bool signature, thrown, done;
        private float time;
        private static readonly HashSet<string> failed = new HashSet<string>();

        public override string OnEnterAnimStr => isUltimate ? parent.PBAoEAnimStr : parent.ForehandAnimStr;

        public StoneChargeState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            // Lets the arcana into the signature slot (the spellbook's Signature page); see
            // SignatureUnlock.cs for when that's unlocked.
            hasSignatureVariant = true;
            SetAnimTimes(
                0.05f, // start
                0.1f,  // hold
                0.15f, // execute: the shield leaves the wizard's hands
                0.4f,  // cancel
                0.5f,  // run
                0.6f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();
            // isUltimate is set before the state is entered (Player.HandleOverdrive).
            signature = isUltimate;
            thrown = done = false;
            time = 0f;
            aim = inputVector != Vector2.zero ? inputVector.normalized : Entity.GetFacingDirectionVector(parent.facingDirection).normalized;
            if (aim == Vector2.zero)
                aim = Vector2.right;
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            if (!thrown)
            {
                thrown = true;
                ThrownShield.Throw(parent, skillID, aim, IsEmpowered, signature);
            }
            if (CancelToDash(false))
                return;
            time += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            if (time >= CastTime)
            {
                done = true;
                base.ExecuteSkill();
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
