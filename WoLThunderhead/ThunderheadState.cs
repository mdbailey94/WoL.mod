using Chaos.AnimatorExtensions;
using UnityEngine;

namespace WoLThunderhead
{
    // Thunderhead (Lightning, standard arcana), a leap like Heroic Leap's: after a very short
    // wind-up on the ground (still vulnerable), the wizard leaps high, straight up. While up there
    // they can't be hit, the way the game's jumping arcana work (their hurtbox is switched off, so
    // attacks and projectiles pass through, and they count as airborne), and lightning crashes down
    // around the spot below them in volleys (level 1, can shock). Then they come crashing down in a
    // lightning slam (level 2) that throws enemies back.
    //
    // The hits are the game's lightning bursts; each strike also shows the game's own lightning
    // bolt from the sky (stripped down to its animation, so it can't hit anything itself).
    public class ThunderheadState : Player.SkillState
    {
        public new static string staticID = "Thunderhead";

        // Timings and sizes come from the config (ThunderheadPlugin), read as each cast starts.
        private float WindupTime, RiseTime, CrashTime, RecoverTime, Height;
        private int VolleySize;

        private float time;
        private float nextVolley;
        private int volleyCount;
        private float spiralTurn;
        private bool done;
        private bool tookOff;
        private bool landed;
        private bool airborne;
        private bool hurtBoxWasOn;
        private Levitator levitator;
        private ElectricLook electric;
        private static bool loggedBolt;
        private const string AirborneModID = "Thunderhead_Airborne";

        public ThunderheadState(FSM newFSM, Player newEnt) : base(staticID, newFSM, newEnt)
        {
            applyStopElementStatus = true;
            SetAnimTimes(
                0.05f, // start
                0.1f,  // hold
                0.05f, // execute
                0.4f,  // cancel
                0.5f,  // run
                0.6f); // exit
        }

        private float HoverTimeNow => IsEmpowered ? ThunderheadPlugin.EnhancedAirTime : ThunderheadPlugin.AirTime;
        private float RadiusNow => IsEmpowered ? ThunderheadPlugin.EnhancedRadius : ThunderheadPlugin.Radius;
        private float VolleyInterval => IsEmpowered ? ThunderheadPlugin.EnhancedVolleyInterval : ThunderheadPlugin.VolleyInterval;
        private float SlamScaleNow => IsEmpowered ? ThunderheadPlugin.EnhancedSlamSize : ThunderheadPlugin.SlamSize;

        public override void OnEnter()
        {
            base.OnEnter();
            time = 0f;
            nextVolley = 0f;
            volleyCount = 0;
            spiralTurn = Random.value * 2f * Mathf.PI;
            done = false;
            tookOff = false;
            landed = false;
            WindupTime = ThunderheadPlugin.WindupTime;
            RiseTime = ThunderheadPlugin.RiseTime;
            CrashTime = ThunderheadPlugin.CrashTime;
            RecoverTime = ThunderheadPlugin.RecoverTime;
            Height = ThunderheadPlugin.Height;
            VolleySize = ThunderheadPlugin.VolleySize;
            levitator = Levitator.On(parent);
        }

        public override void ExecuteSkill()
        {
            if (done)
            {
                base.ExecuteSkill();
                return;
            }
            // Dash-cancelling only before take-off, like committing to a leap.
            if (!tookOff && CancelToDash(false))
                return;

            time += Time.deltaTime;
            if (parent.rigidbody2D != null)
                parent.rigidbody2D.velocity = Vector2.zero;
            Vector2 ground = parent.transform.position;

            // A moment's crouch on the ground (still vulnerable)...
            if (time < WindupTime)
            {
                parent.anim?.PlayDirectional(parent.JumpAnimStr, -1, 0f);
                return;
            }
            // ...then up and away.
            if (!tookOff)
            {
                tookOff = true;
                TakeOff(ground);
            }
            float t = time - WindupTime;
            float hoverEnd = RiseTime + HoverTimeNow;
            float crashEnd = hoverEnd + CrashTime;

            float lift;
            if (t < RiseTime)
            {
                float r = t / RiseTime;
                lift = 1f - (1f - r) * (1f - r);     // fast off the ground, easing to the top
                parent.anim?.PlayDirectional(parent.JumpAnimStr, -1, 0.3f);
            }
            else if (t < hoverEnd)
            {
                lift = 1f;
                parent.anim?.PlayDirectional(parent.ChargeAnimStr, -1, 0.5f);
                if (t >= nextVolley)
                {
                    nextVolley = t + VolleyInterval;
                    Volley(ground);
                }
            }
            else if (t < crashEnd)
            {
                float c = (t - hoverEnd) / CrashTime;
                lift = 1f - c * c;                     // slamming down, faster and faster
                parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.2f);
            }
            else
            {
                lift = 0f;
                if (!landed)
                {
                    landed = true;
                    Slam(ground);
                }
                parent.anim?.PlayDirectional(parent.GSlamAnimStr, -1, 0.6f);
            }
            electric?.Update(lift);
            if (levitator != null)
                levitator.height = Height * lift + (t >= RiseTime && t < hoverEnd ? Mathf.Sin(t * 7f) * 0.06f : 0f);

            if (landed && t >= crashEnd + RecoverTime)
            {
                done = true;
                Land();
                base.ExecuteSkill();
            }
        }

        public override void OnExit()
        {
            Land();
            base.OnExit();
        }

        // ---- Up and down ----

        // Leaving the ground: airborne, and nothing can touch you (hurtbox off, like a jump).
        private void TakeOff(Vector2 ground)
        {
            airborne = true;
            try
            {
                parent.airborneStat?.AddMod(new BoolVarStatMod(AirborneModID, true, 10));
            }
            catch
            {
            }
            if (parent.hurtBoxCollider != null)
            {
                hurtBoxWasOn = parent.hurtBoxCollider.enabled;
                parent.hurtBoxCollider.enabled = false;
            }
            if (parent.health != null)
                parent.health.invulnerable = true;
            electric?.Off();
            electric = ElectricLook.On(parent);
            Dust(ground, 30, 0.9f);
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(ground), null, 24f, -1f, 0.6f, false);
        }

        private void Touchdown()
        {
            if (!airborne)
                return;
            airborne = false;
            try
            {
                parent.airborneStat?.RemoveMod(AirborneModID);
            }
            catch
            {
            }
            if (parent.hurtBoxCollider != null && hurtBoxWasOn)
                parent.hurtBoxCollider.enabled = true;
            // Always back to hurtable (restoring "already invulnerable" could leave it on for good).
            if (parent.health != null)
                parent.health.invulnerable = false;
            electric?.Off();
            electric = null;
        }

        private void Land()
        {
            Touchdown();
            electric?.Off();
            electric = null;
            if (levitator != null)
            {
                levitator.Off();
                levitator = null;
            }
        }

        // ---- Lightning ----

        // A few strikes at once round the spot below you. Spread evenly (the default): all the
        // strikes of the whole leap are laid out as a sunflower spiral over the circle (even
        // coverage, nothing bunched up), turned a random way each cast, and each volley takes
        // every so-many of them, so every volley strikes near, middle and far all round you.
        // Otherwise each lands at random.
        private void Volley(Vector2 ground)
        {
            int size = VolleySize;
            float radius = RadiusNow;
            bool even = ThunderheadPlugin.EvenSpread;
            int volleys = Mathf.Max(1, Mathf.CeilToInt(HoverTimeNow / VolleyInterval - 0.001f));
            int total = volleys * size;
            for (int i = 0; i < size; i++)
            {
                Vector2 offset;
                if (even)
                {
                    int j = (i * volleys + volleyCount) % total;
                    float distance = radius * Mathf.Sqrt((j + 0.5f) / total);
                    float angle = spiralTurn + j * GoldenAngle;
                    offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance
                        + Random.insideUnitCircle * 0.08f * radius;
                }
                else
                    offset = Random.insideUnitCircle * radius;
                Vector2 spot = ground + new Vector2(offset.x, offset.y * 0.7f);
                LightningBurst.CreateBurst(spot, parent.skillCategory, skillID, 1, ThunderheadPlugin.StrikeSize, false);
                Bolt(spot, 1f);
            }
            volleyCount++;
            Spark(ground + new Vector2(0f, Height + 0.6f));
        }

        private const float GoldenAngle = 2.39996f; // radians

        // Crashing down: a big lightning slam that throws enemies back.
        private void Slam(Vector2 ground)
        {
            Touchdown();
            float scale = SlamScaleNow;
            LightningBurst.CreateBurst(ground, parent.skillCategory, skillID, 2, scale, false);
            Bolt(ground, 1.6f);
            Dust(ground, 80, scale);
            Shake(1.4f);
            try
            {
                TimeScaleController.FreezeForFrames(3, false);
            }
            catch
            {
            }
            SoundManager.PlayAudioWithDistance("StandardHeavySwing", new Vector2?(ground), null, 24f, -1f, 0.5f, false);
        }

        // The game's lightning bolt from the sky, just its animation.
        private static void Bolt(Vector2 position, float size)
        {
            try
            {
                if (LightningStrike.Prefab == null)
                    return;
                GameObject go = Object.Instantiate(LightningStrike.Prefab, position, Quaternion.identity);
                foreach (MonoBehaviour script in go.GetComponentsInChildren<MonoBehaviour>(true))
                    Object.Destroy(script);
                foreach (Collider2D collider in go.GetComponentsInChildren<Collider2D>(true))
                    Object.Destroy(collider);
                foreach (Animator anim in go.GetComponentsInChildren<Animator>(true))
                    anim.enabled = true;
                go.transform.localScale = new Vector3(size, size, 1f);
                if (!go.activeSelf)
                    go.SetActive(true);
                Object.Destroy(go, 0.7f);
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    ThunderheadPlugin.Log("Thunderhead: using the game's lightning bolts");
                }
            }
            catch (System.Exception e)
            {
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    ThunderheadPlugin.Log($"Thunderhead: the game's lightning bolt is unavailable, bursts only: {e.Message}");
                }
            }
        }

        private static void Spark(Vector2 position)
        {
            try
            {
                PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Medium, position, position, null, null, 0f);
            }
            catch
            {
            }
        }

        private static void Dust(Vector2 position, int count, float radius)
        {
            try
            {
                PoolManager.GetPoolItem<DustEmitter>().EmitCircle(count, radius, -6f, -1f, new Vector3?(position), null);
            }
            catch
            {
            }
        }

        private static void Shake(float intensity)
        {
            try
            {
                CameraController.ShakeCamera(intensity, false);
            }
            catch
            {
            }
        }
    }

    // Lifts the wizard's picture into the air (the wizard themself, their shadow and their hitbox
    // stay on the ground). Applied after the animation each frame and taken back off the next.
    //
    // The game draws things lower on screen in front, and it can judge the wizard by where their
    // picture is: lifted, they'd count as further back and slip behind statues and pillars they
    // stand in front of. So while lifted they keep the draw order they had on the ground (they
    // don't move sideways during the leap), set again just before each frame is drawn.
    public class Levitator : MonoBehaviour
    {
        public float height;
        private Vector3 applied;
        private Vector3 lastSet;
        private bool active;
        private SpriteRenderer body;
        private int groundOrder;
        private int groundLayer;
        private static bool loggedSorting;

        public static Levitator On(Player player)
        {
            try
            {
                if (player.spriteRenderer == null)
                    return null;
                GameObject body = player.spriteRenderer.gameObject;
                Levitator levitator = body.GetComponent<Levitator>();
                if (levitator == null)
                    levitator = body.AddComponent<Levitator>();
                levitator.enabled = true;
                levitator.height = 0f;
                levitator.body = player.spriteRenderer;
                levitator.groundOrder = player.spriteRenderer.sortingOrder;
                levitator.groundLayer = player.spriteRenderer.sortingLayerID;
                if (!loggedSorting)
                {
                    loggedSorting = true;
                    foreach (SortGenericLayer sorter in player.GetComponentsInChildren<SortGenericLayer>(true))
                        ThunderheadPlugin.Log($"Thunderhead: sorter on '{sorter.name}', by sprite transform={sorter.sortBySpriteTransform}, " +
                            $"order {player.spriteRenderer.sortingOrder}");
                }
                return levitator;
            }
            catch
            {
                return null;
            }
        }

        private void LateUpdate()
        {
            Vector3 position = transform.localPosition;
            if (active && position == lastSet)
                position -= applied;
            applied = new Vector3(0f, height, 0f);
            lastSet = position + applied;
            transform.localPosition = lastSet;
            active = true;
        }

        // Right before the frame is drawn, after the game's own sorting.
        private void OnWillRenderObject()
        {
            if (!active || body == null || height <= 0.01f)
                return;
            body.sortingLayerID = groundLayer;
            body.sortingOrder = groundOrder;
        }

        public void Off()
        {
            if (active && transform.localPosition == lastSet)
                transform.localPosition -= applied;
            applied = Vector3.zero;
            height = 0f;
            active = false;
            enabled = false;
        }
    }
}
