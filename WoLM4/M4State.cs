using UnityEngine;

namespace WoLM4
{
    // 0.3.0 (Matt, 11-04): back from #43, harder (8 / 9 a bullet), 3x Air Dart's speed, 14 u range,
    // the rifle drawn in the hands and tracers for bullets.
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
            roundGap = IsEmpowered ? M4Plugin.EnhancedRoundGap : M4Plugin.RoundGap;
            extraRounds = IsEmpowered ? M4Plugin.EnhancedExtraRounds : 0;
        }

        public override void OnEnter()
        {
            base.OnEnter();
            parent.ToggleEnemyFloorCollisions(status: false);
            burstStarted = false;
            // 0.3.0: the rifle in the wizard's hands while firing.
            try { M4Look.Show(parent, inputVector != Vector2.zero ? inputVector : Entity.GetFacingDirectionVector(parent.facingDirection)); }
            catch (System.Exception e) { M4Plugin.Log($"M4: couldn't show the rifle: {e.Message}"); }
            animExecTime = 0.2f;
        }

        public override void OnExit()
        {
            base.OnExit();
            parent.ToggleEnemyFloorCollisions();
            M4Look.Hide(parent, 0.25f);   // lowered a moment after the last shot
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
            float spread = M4Plugin.Spread * (finalBurst ? M4Plugin.FinalSpread : 1f);
            Vector2 aim = Quaternion.Euler(0f, 0f, Random.Range(-spread, spread)) * inputVector;
            Vector2 muzzle = (Vector2)parent.attackOriginTrans.position + inputVector * 1.2f;
            SoundManager.PlayAudioWithDistance("ArcherArrow", muzzle);
            M4Look.Kick(parent, inputVector);
            AirDart bullet = (AirDart)Projectile.CreateProjectile(parent, attackPrefab, muzzle);
            bullet.attackBox.SetAttackInfo(parent.skillCategory, skillID, currentLevel);
            bullet.moveVector = aim;
            bullet.attackBox.knockbackOverwriteVector = aim;
            bullet.moveSpeed *= M4Plugin.BulletSpeed;
            // 0.3.0: long range. The game ends a dart's flight on its fly timer, so set it from
            // the range at this speed.
            if (bullet.moveSpeed > 0.01f)
            {
                bullet.flyTime = M4Plugin.Range / bullet.moveSpeed;
                bullet.lifeTime = Mathf.Max(bullet.lifeTime, bullet.flyTime + 0.5f);
                bullet.RefreshFlightTime();
            }
            // 0.3.0: a rifle tracer instead of the tinted dart (the dart's hit box stays).
            foreach (SpriteRenderer sprite in bullet.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.enabled = false;
            var tracer = new GameObject("M4 Tracer");
            tracer.transform.SetParent(bullet.transform, false);
            tracer.transform.localPosition = Vector3.zero;
            tracer.AddComponent<TracerPose>().angle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
            SpriteRenderer look = tracer.AddComponent<SpriteRenderer>();
            look.sprite = M4Look.Bullet;
            M4Look.Sort(look, parent, 4);
        }
    }

    // Keeps the tracer pointing along its flight at its native pixel size, whatever the dart
    // underneath does with its own rotation and scale.
    internal class TracerPose : MonoBehaviour
    {
        public float angle;

        private void LateUpdate()
        {
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
            Vector3 p = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(p.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(p.y)), 1f);
        }
    }
}
