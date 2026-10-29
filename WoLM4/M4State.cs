using UnityEngine;

namespace WoLM4
{
    // The M4: a basic of three trigger pulls, each a burst of brass bullets down the aim line (3,
    // 3, then 5 on the last pull). Built like the game's Air Dart basic (its dart is the bullet,
    // tinted, thinner and faster), so the game's basic relics (auto combo, full auto, basic
    // damage and so on) work on it as they do on Air Dart.
    public class M4State : Player.MeleeAttackState
    {
        public new static string staticID = "M4Basic";

        private static readonly Color Brass = new Color(1f, 0.82f, 0.35f);

        private float roundGap = 0.055f;
        private int extraRounds;
        private bool burstStarted;
        private int rounds, fired;

        public M4State(FSM parentFSM, Player parentEntity)
            : base(staticID, parentFSM, parentEntity)
        {
            isBasic = true;
            isProjectileSkill = true;
            attackPrefab = AirDart.Prefab;
            setParent = false;
            destroyAttackObjectOnExit = false;
            maxComboCount = 3;
            animStartTime = 0.1f;
            animExecTime = 0.2f;
            baseCancelThreshold = 0.35f;
            finalCancelThreshold = 0.4f;
            baseRunThreshold = 0.5f;
            finalRunThreshold = 0.6f;
            baseExitThreshold = 0.7f;
            finalExitThreshold = 0.8f;
            flipObjectForBackhand = false;
            alwaysStartBackhand = true;
            // Barely moves while shooting, like Air Dart.
            attackMoveSpeedMod = new NumVarStatMod(atkMoveModStr, 0.125f, 10, VarStatModType.OverrideWithMods);
            finalAttackMoveSpeedMod = attackMoveSpeedMod;
            autoHoldStat.BaseValue = true;
        }

        // Enhanced: one more round a burst, fired faster.
        public override void SetEmpowered(bool givenStatus, BoolVarStatMod givenMod)
        {
            base.SetEmpowered(givenStatus, givenMod);
            roundGap = IsEmpowered ? 0.04f : 0.055f;
            extraRounds = IsEmpowered ? 1 : 0;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            parent.ToggleEnemyFloorCollisions(status: false);
            burstStarted = false;
            animExecTime = 0.2f;
        }

        public override void OnExit()
        {
            base.OnExit();
            parent.ToggleEnemyFloorCollisions();
        }

        // Called once per round: the base Update calls it again each time attackExecuted is
        // cleared and the animation reaches the pushed-back exec time.
        public override void ExecuteAttack()
        {
            if (!burstStarted)
            {
                burstStarted = true;
                rounds = (comboCounter >= maxComboCount ? M4Plugin.FinalBurst : M4Plugin.Burst) + extraRounds;
                fired = 0;
                HandleComboCount(comboCounter);
                HandleSelfTransition(comboCounter);
                AnnounceSkillExecuteEvent();
            }
            FireRound(comboCounter >= maxComboCount);
            fired++;
            if (fired < rounds)
            {
                animExecTime += roundGap;
                cancelThreshold += roundGap;
                runThreshold += roundGap;
                exitThreshold += roundGap;
                attackExecuted = false;
            }
        }

        private void FireRound(bool finalBurst)
        {
            float spread = M4Plugin.Spread * (finalBurst ? 1.6f : 1f);
            Vector2 aim = Quaternion.Euler(0f, 0f, Random.Range(-spread, spread)) * inputVector;
            Vector2 muzzle = (Vector2)parent.attackOriginTrans.position + inputVector * 0.6f;
            SoundManager.PlayAudioWithDistance("ArcherArrow", muzzle);
            AirDart bullet = (AirDart)Projectile.CreateProjectile(parent, attackPrefab, muzzle);
            bullet.attackBox.SetAttackInfo(parent.skillCategory, skillID, currentLevel);
            bullet.moveVector = aim;
            bullet.attackBox.knockbackOverwriteVector = aim;
            bullet.moveSpeed *= M4Plugin.BulletSpeed;
            bullet.transform.localScale = bullet.transform.localScale * 0.7f;
            foreach (SpriteRenderer sprite in bullet.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.color = Brass;
        }
    }
}
