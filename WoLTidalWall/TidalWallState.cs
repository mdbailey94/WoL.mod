using System.Collections.Generic;
using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLTidalWall
{
    // Tidal Wall (Water, standard arcana): a wall of water rises in front of the wizard and surges
    // forward. The wall is rows of the game's own Tidal Blast water orbs (WaterBlast): each row
    // swells up, then bursts a moment later as the next row rises a step further out, so the
    // wave rolls forward and keeps shoving whatever it catches straight along your aim. The last
    // row is a bigger crest that hits harder (skill level 2). A lane of the wave that runs into a
    // wall stops there, so the water breaks against walls instead of passing through them.
    //
    // Cast from the signature slot with the gauge full (isUltimate, the game's flag for a signature
    // cast; it's set on the state just before the cast and cleared when the state exits) it's a
    // tidal WAVE: 5 wide like the enhanced wall, rolling on twice as far, with a little larger rows
    // that freeze everything they catch (skill levels 3 and 4, which have a 100% freeze chance).
    // Enhanced and signature are separate flags and stack: a signature cast of the enhanced
    // arcana (the usual case, the game empowers whatever sits in the signature slot) rolls 10 rows.
    public class TidalWallState : Player.SkillState
    {
        public new static string staticID = "TidalWall";

        private const int Lanes = 3;                   // orbs side by side in each row
        private const int EnhancedLanes = 5;           // enhanced and signature
        private const int Rows = 4;                    // rows rolling forward, the last one is the crest
        private const int EnhancedRows = 5;
        private const int SignatureRowsMultiplier = 2; // the signature wave rolls this many times as far (8, 10 enhanced)
        private const float FirstRowDistance = 1.75f;  // from the wizard (Water Wall's spawn distance)
        private const float RowSpacing = 1.4f;         // how much further out each row rises
        private const float LaneSpacing = 1.3f;        // gap between the orbs in a row
        private const float RowInterval = 0.1f;       // seconds between rows; a row bursts as the next one rises
        private const float SurgeScale = 1f;           // size of the orbs in the rolling rows
        private const float CrestScale = 1.3f;         // size of the crest (Tidal Blast's enhanced orb is 1.25)
        private const float SignatureSurgeScale = 1.15f; // the signature wave's rows and crest are a little larger
        private const float SignatureCrestScale = 1.5f;
        // Skill levels (SkillStats in the plugin): the rolling rows, the crest, and the signature
        // wave's rows and crest, which hit harder and always freeze.
        private const int RowLevel = 1;
        private const int CrestLevel = 2;
        private const int SignatureRowLevel = 3;
        private const int SignatureCrestLevel = 4;
        private const float FinishDelay = 0.15f;       // seconds after the crest before the wizard can act
        private const float WallBackoff = 0.3f;        // an orb that would land in a wall stops this far short
        private const float MinWallGap = 0.5f;         // a blocked orb closer than this to the last one isn't made
        private const int SwellDrops = 4;              // water drops thrown up as each orb rises
        private const float RowShake = 0.15f;
        private const float CrestShake = 0.6f;

        private Vector2 origin;
        private Vector2 direction;
        private Vector2 side;
        private int lanes;
        private int rows;
        private bool signature;   // this cast is the signature wave (isUltimate at cast time)
        private float time;
        private int ticks;
        private bool started;
        private bool finished;

        // Per lane: the orb waiting to burst, where the lane's last orb was, and whether a wall
        // has stopped it.
        private WaterBlast[] waiting;
        private Vector2[] lanePosition;
        private bool[] laneBlocked;

        // For the log line at the end of each cast.
        private int blasts;
        private int hits;
        private int lanesBroken;
        private readonly List<int> targetIDs = new List<int>();
        private readonly List<Health> targets = new List<Health>();
        private static bool loggedOrb;

        public override string OnEnterAnimStr => parent.ForehandAnimStr;

        public TidalWallState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            // Lets the arcana into the signature slot: the spellbook's Signature page and the
            // saved loadout both require it (SpellBookUI.LoadEleSkillDict, Player.InitSkills).
            hasSignatureVariant = true;
            applyStopElementStatus = true;
            SetAnimTimes(
                0.1f,  // start
                0.15f, // hold
                0.2f,  // execute
                0.5f,  // cancel
                0.6f,  // run
                0.7f); // exit
        }

        public override void OnEnter()
        {
            base.OnEnter();

            // Lock the wave's start and direction on cast so it doesn't swing if the player turns.
            direction = inputVector;
            if (direction == Vector2.zero)
                direction = Vector2.right;
            direction.Normalize();
            side = Globals.GetPerpendicular(direction);
            origin = parent.attackOriginTrans.position;

            // The game sets isUltimate for a signature cast (the signature slot's arcana with the
            // gauge full) and clears it once the state exits, so it's read once here.
            signature = isUltimate;
            lanes = IsEmpowered || signature ? EnhancedLanes : Lanes;
            rows = (IsEmpowered ? EnhancedRows : Rows) * (signature ? SignatureRowsMultiplier : 1);
            waiting = new WaterBlast[lanes];
            lanePosition = new Vector2[lanes];
            laneBlocked = new bool[lanes];
            for (int i = 0; i < lanes; i++)
                lanePosition[i] = origin;

            time = 0f;
            ticks = 0;
            started = false;
            finished = false;
            blasts = 0;
            hits = 0;
            lanesBroken = 0;
            targetIDs.Clear();
            targets.Clear();

            TidalWallPlugin.Log($"Tidal Wall: cast ({Variant()}), " +
                $"{rows} rows x {lanes} wide toward ({direction.x:0.00}, {direction.y:0.00})");
        }

        // Which version of the arcana this cast is, for the log: normal, enhanced (the relic-based
        // empowerment, IsEmpowered), signature (the gauge cast, isUltimate) or both at once.
        private string Variant()
        {
            if (IsEmpowered && signature)
                return "both: enhanced + signature";
            if (signature)
                return "signature";
            return IsEmpowered ? "enhanced" : "normal";
        }

        public override void ExecuteSkill()
        {
            if (finished)
            {
                base.ExecuteSkill();
                return;
            }
            if (CancelToDash(false))
                return;

            // The first row rises with the kick, like Tidal Blast's.
            if (!started)
            {
                started = true;
                parent.FaceTarget(origin + direction);
                parent.anim.PlayDirectional(parent.KickAnimStr, -1, 0.1f);
                Tick();
                return;
            }

            time += Time.deltaTime;
            while (ticks <= rows && time >= ticks * RowInterval)
                Tick();

            if (ticks > rows && time >= rows * RowInterval + FinishDelay)
            {
                finished = true;
                Report();
                base.ExecuteSkill();
            }
        }

        public override void OnExit()
        {
            // Cut short mid-wave (a dash, or getting hit): the water that hasn't burst yet just
            // drains away.
            if (started && !finished)
            {
                int dropped = 0;
                for (int i = 0; i < waiting.Length; i++)
                {
                    if (waiting[i] != null && !waiting[i].destroyQueued)
                    {
                        waiting[i].Dissipate();
                        dropped++;
                    }
                    waiting[i] = null;
                }
                TidalWallPlugin.Log($"Tidal Wall: cancelled after {System.Math.Max(0, ticks - 1)} of {rows} rows " +
                    $"({dropped} orbs drained), {hits} hits on {targetIDs.Count} targets");
            }
            base.OnExit();
        }

        // Each tick bursts the row that's waiting and raises the next one a step further out.
        private void Tick()
        {
            int tick = ticks++;
            if (tick > 0)
                BurstRow(tick - 1);
            if (tick < rows)
                RaiseRow(tick);
        }

        private void RaiseRow(int row)
        {
            bool crest = row == rows - 1;
            float distance = FirstRowDistance + RowSpacing * row;
            for (int lane = 0; lane < lanes; lane++)
            {
                waiting[lane] = null;
                if (laneBlocked[lane])
                    continue;

                float across = (lane - (lanes - 1) * 0.5f) * LaneSpacing;
                Vector2 target = origin + direction * distance + side * across;
                Vector2 from = lanePosition[lane];
                // Same wall check Tidal Blast uses for its orbs: stop short of the first wall.
                Vector2 reached = Globals.GetLinecastVector(from, target, ChaosCollisions.layerUpperWallAndObst);
                if ((reached - target).sqrMagnitude > 0.0001f)
                {
                    laneBlocked[lane] = true;
                    lanesBroken++;
                    reached -= (target - from).normalized * WallBackoff;
                    if ((reached - from).magnitude < MinWallGap)
                        continue;
                    target = reached;
                }
                lanePosition[lane] = target;
                waiting[lane] = RaiseOrb(target, crest);
            }
            if (!crest)
                CameraController.ShakeCamera(RowShake);
        }

        // One of Tidal Blast's water orbs, set up as this skill. It can't hit anything until it
        // bursts.
        private WaterBlast RaiseOrb(Vector2 position, bool crest)
        {
            if (WaterBlast.Prefab == null)
            {
                if (!loggedOrb)
                {
                    loggedOrb = true;
                    TidalWallPlugin.Log("Tidal Wall: the game's WaterBlast prefab is missing, the wave can't be made");
                }
                return null;
            }

            WaterBlast orb = ChaosInst<WaterBlast>(WaterBlast.Prefab, position, Globals.GetRotationQuaternion(direction));
            if (orb == null || orb.attack == null)
                return null;

            if (signature)
                orb.transform.localScale *= crest ? SignatureCrestScale : SignatureSurgeScale;
            else
                orb.transform.localScale *= crest ? CrestScale : SurgeScale;
            int level = crest ? (signature ? SignatureCrestLevel : CrestLevel) : (signature ? SignatureRowLevel : RowLevel);
            // A signature cast's hits are marked as the game marks its own (isUltimate), so they
            // don't refill the gauge they just spent.
            orb.attack.SetAttackInfo(parent.skillCategory, skillID, level, signature);
            orb.attack.knockbackOverwriteVector = direction;
            orb.blastSkillLevel = level;
            orb.attack.attackCollisionEventHandlers += OnHit;
            LogOrbOnce(orb);
            if (orb.attack.collider != null)
                orb.attack.collider.enabled = false;

            PoolManager.GetPoolItem<WaterDropEmitter>().Emit(new int?(SwellDrops), new Vector3?(position),
                WaterDropEmitter.MedWaterExplosionOverride, null, 0f, null, null);
            return orb;
        }

        private void BurstRow(int row)
        {
            for (int lane = 0; lane < lanes; lane++)
            {
                WaterBlast orb = waiting[lane];
                waiting[lane] = null;
                if (orb == null || orb.destroyQueued)
                    continue;
                if (orb.attack != null && orb.attack.collider != null)
                    orb.attack.collider.enabled = true;
                // Blast() switches the hit area off and on again so it hits whatever is inside.
                orb.Blast();
                blasts++;
            }
            if (row == rows - 1)
                CameraController.ShakeCamera(CrestShake);
        }

        private void OnHit(Collider2D col)
        {
            if (col == null)
                return;
            hits++;
            Health health = col.GetComponent<Health>();
            if (health == null && col.transform.parent != null)
                health = col.transform.parent.GetComponent<Health>();
            int id = health != null ? health.GetInstanceID() : col.GetInstanceID();
            if (targetIDs.Contains(id))
                return;
            targetIDs.Add(id);
            if (health != null)
                targets.Add(health);
        }

        private void Report()
        {
            int frozen = 0;
            foreach (Health health in targets)
            {
                if (health != null && health.parentFSM != null && health.parentFSM.currentStateName != null
                    && health.parentFSM.currentStateName.Contains("Frozen"))
                    frozen++;
            }
            string walls = lanesBroken > 0 ? $", {lanesBroken} lane(s) broke on walls" : string.Empty;
            TidalWallPlugin.Log($"Tidal Wall: {(signature ? "signature " : string.Empty)}wave done, {blasts} bursts, " +
                $"{hits} hits on {targetIDs.Count} targets, {frozen} frozen{walls}");
        }

        private static void LogOrbOnce(WaterBlast orb)
        {
            if (loggedOrb)
                return;
            loggedOrb = true;
            Collider2D col = orb.attack.collider;
            if (col == null)
            {
                TidalWallPlugin.Log("Tidal Wall: using the game's Tidal Blast water orbs (no hit area found on them!)");
                return;
            }
            TidalWallPlugin.Log($"Tidal Wall: using the game's Tidal Blast water orbs (hit area {col.GetType().Name} " +
                $"on '{col.gameObject.name}', about {col.bounds.size.x:0.0} x {col.bounds.size.y:0.0})");
        }
    }
}
