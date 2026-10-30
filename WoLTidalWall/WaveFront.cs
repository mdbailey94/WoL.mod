using System.Collections.Generic;
using UnityEngine;

namespace WoLTidalWall
{
    // Everything one cast's wave needs, worked out by the state when it's cast.
    public class WaveSettings
    {
        public string Variant;          // for the log: normal, enhanced, signature, both
        public bool Signature;
        public string SkillCategory;
        public string SkillID;
        public GameObject Caster;
        public Vector2 Center;          // where the wall rises (the middle of it)
        public Vector2 Direction;       // the aim; the wall stands across it and rolls along it
        public int Columns;             // water columns side by side
        public float Spacing;           // gap between columns
        public float ColumnScale;       // size of each column (the signature's tower over the wizard)
        public float Depth;             // how thick the wall's hit area is, along the aim
        public float HoldTime;          // seconds the wall stands before it rolls
        public float Speed;             // units per second while rolling
        public float RollTime;          // seconds it rolls before it crashes
        public float TickInterval;      // seconds between pushes
        public int TickLevel;           // skill level of the pushes
        public int CrashLevel;          // skill level of the crash at the end
        public int CinematicToken;      // the signature's camera/light changes (0 = none)

        public float Width => Columns * Spacing;
        public float Distance => Speed * RollTime;
    }

    // One cast's wave: a single standing wall of water that rises across the aim, stands for a
    // moment, then rolls forward as one front, pushing everything it touches along the aim on a
    // steady beat, and crashes in a splash at the end of its run (or when it meets a wall).
    //
    // The wall is the game's own WaterWall (Assets/Prefabs/AreaEffects/WaterWall.prefab: a Wave
    // that raises a row of water columns plus a "ReflectWall" that negates or reflects enemy
    // projectiles, NegateProjectiles; WaterWall.cs). The game never casts it (Player.UseWaterWall
    // is never added to the wizard), so its columns' own attacks are switched off and this
    // object carries it: the WaterWall is parented here and moves with it. The pushing is one
    // hit box (the game's Attack.BoxPrefab) spanning the wall, switched off and on every tick so
    // whatever is inside is hit again (as Gale Rider's ball and Tidal Blast's orbs do), always
    // knocking straight along the aim (knockbackOverwriteVector). If the WaterWall can't be made,
    // the wave still works, drawn with water spray only.
    //
    // It runs on its own object, so the wizard is free to move once it's cast.
    public class WaveFront : MonoBehaviour
    {
        // The timings, sizes and the signature's beats that are settings (TidalWallPlugin) are read
        // once, when the wave is launched, so a changed setting never takes effect mid-wave.
        private float MaxRiseTime;                   // the wall rolls after this even if columns are still rising (0.5)
        private const float RiseDelay = 0.05f;       // before the first column (so its attack is off before it shows)
        private float RiseInterval;                  // between columns rising (0.03)
        private float WidthMargin;                   // hit area a little wider than the columns (0.6)
        private const float FrontOffset = 0.35f;     // the wall's front edge, ahead of its middle line
        private const float WallLookahead = 0.3f;    // a level wall this close ahead crashes the wave
        private float CrashDepthScale;               // the crash hits a little deeper than the pushes (1.6)
        private float CrashActiveTime;               // seconds the crash's hit area stays on (0.1)
        private const float LingerTime = 1.1f;       // seconds after the crash before this object goes (WaterWall breaks in 1)
        private const float SprayInterval = 0.07f;   // spray thrown off the front while rolling
        private const float SignatureSprayInterval = 0.035f;
        private const float WallDestroyDelay = 30f;  // WaterWall's own timer; the crash ends it long before
        // Signature beats (the gather is in TidalWallState).
        private int RiseHitStopFrames;               // the wave erupting (4)
        private int TickHitStopFrames;               // a push that lands while rushing (2)
        private float RiseShake;                     // 1.2
        private float TickShake;                     // 0.3
        private float CrashShake;                    // 1.6
        private float CrashSlowMoTime;               // seconds of real time easing back from near-frozen (0.45)
        private float CrashSlowMoScale;              // 0.03
        private float CrashFlash;                    // the room light flashes to this multiple of normal (1.5)
        private const float FlashTime = 0.08f;       // seconds (real time) the flash holds
        private float EaseBackTime;                  // seconds (real time) camera and light ease back (0.6)
        private float crashZoom;                     // the pull-back the camera eases back from after the crash
        // Shared with the gather (TidalWallState).
        public static float SignatureZoom => TidalWallPlugin.CameraPullBack; // camera pulled back by this fraction (0.18)
        public static float SignatureDim => TidalWallPlugin.RoomDim;         // room light dimmed to this multiple (0.55)

        // Takes this wave's copy of the settings above.
        private void ReadSettings()
        {
            MaxRiseTime = TidalWallPlugin.MaxRiseTime;
            RiseInterval = TidalWallPlugin.ColumnRiseInterval;
            WidthMargin = TidalWallPlugin.HitAreaMargin;
            CrashDepthScale = TidalWallPlugin.CrashDepth;
            CrashActiveTime = TidalWallPlugin.CrashHitTime;
            RiseHitStopFrames = TidalWallPlugin.RiseHitStopFrames;
            TickHitStopFrames = TidalWallPlugin.PushHitStopFrames;
            RiseShake = TidalWallPlugin.RiseShake;
            TickShake = TidalWallPlugin.PushShake;
            CrashShake = TidalWallPlugin.CrashShake;
            CrashSlowMoTime = TidalWallPlugin.CrashSlowMoTime;
            CrashSlowMoScale = TidalWallPlugin.CrashSlowMoSpeed;
            CrashFlash = TidalWallPlugin.CrashFlash;
            EaseBackTime = TidalWallPlugin.EaseBackTime;
            crashZoom = SignatureZoom;
        }

        private enum Phase { Rising, Holding, Rolling, Crashed }

        private WaveSettings s;
        private Vector2 side;
        private Phase phase;
        private float phaseTime;
        private float nextPulse;
        private int pulse;
        private int lastHitStopPulse = -1;
        private bool hitThisPulse;
        private float sprayClock;
        private int sprays;
        private float rolled;
        private float easeClock;
        private bool restored;
        private bool reported;
        private string crashReason = "crashed";

        private WaterWall wall;
        private bool wallUp;      // its columns actually rose (otherwise the spray stands in for them)
        private int columnsUp;
        private NegateProjectiles barrier;
        private readonly List<Transform> columns = new List<Transform>();
        private readonly Dictionary<Transform, Vector3> scaled = new Dictionary<Transform, Vector3>();
        private Attack hitAttack;
        private Collider2D hitCollider;
        private BoxCollider2D hitBox;
        private int hitObjID;
        private AudioSource loop;
        // The normal and enhanced wave's drawn look (WaveLook.cs); null for the signature, which
        // keeps its own.
        private WaveLook look;

        // For the log line at the end.
        private readonly Dictionary<int, Entity> enemies = new Dictionary<int, Entity>();
        private readonly HashSet<int> objects = new HashSet<int>();
        private int hits;
        private int damage;
        private int blocked;
        private int columnAttacksOff;

        private static bool loggedWall;
        private static bool loggedBox;

        public static WaveFront Launch(WaveSettings settings)
        {
            var go = new GameObject("TidalWallWave");
            go.transform.position = settings.Center;
            WaveFront wave = go.AddComponent<WaveFront>();
            try
            {
                wave.Setup(settings);
            }
            catch
            {
                // Half made: take it away again (OnDestroy cleans up) and let the caster know.
                Destroy(go);
                throw;
            }
            return wave;
        }

        private void Setup(WaveSettings settings)
        {
            s = settings;
            ReadSettings();
            side =Globals.GetPerpendicular(s.Direction);
            phase = Phase.Rising;
            phaseTime = 0f;
            MakeWall();
            MakeHitBox();
            Health.globalTakeDamageActualHandlers += OnDamage;

            // The wall rising.
            for (int i = 0; i < s.Columns; i++)
                Effects.Drops(ColumnPoint(i), s.Signature ? 8 : 4, s.Signature);
            if (s.Signature)
            {
                // Beat 2: the wave erupts out of the gathered water.
                Effects.HitStop(RiseHitStopFrames);
                Effects.Shake(RiseShake, true);
                Effects.Ring(s.Center, s.Width * 0.5f);
                Effects.Twirl(ColumnPoint(0));
                Effects.Twirl(ColumnPoint(s.Columns - 1));
                Effects.BigSplash(s.Center, 1.2f);
                Effects.Sound("AquaVortex", s.Center, 0.8f);
                Effects.Sound("DelugeBomb", s.Center, 0.7f);
                TidalWallPlugin.Log($"Tidal Wall: signature beat 2/4 rise: {s.Columns} columns x{s.ColumnScale:0.0} tall, " +
                    $"{s.Width:0.0} wide, hit-stop {RiseHitStopFrames} frames, shake {RiseShake:0.0}");
            }
            else
            {
                Effects.Sound("AquaBeamStart", s.Center, 0.9f);
                Effects.Shake(0.2f);
                look = new WaveLook(s);
                var points = new List<Vector2>();
                for (int i = 0; i < s.Columns; i++)
                    points.Add(ColumnPoint(i));
                Effects.Try("drawn rise", () => look.Rise(points));
            }
        }

        private Vector2 ColumnPoint(int index) =>
            s.Center + side * ((index - (s.Columns - 1) * 0.5f) * s.Spacing);

        // The game's WaterWall, set up the way Player.UseWaterWall would (Player.cs, UseWaterWall.Update),
        // laid out across the aim and parented here.
        private void MakeWall()
        {
            try
            {
                GameObject prefab = WaterWall.Prefab;
                if (prefab == null)
                {
                    LogWallOnce("the game's WaterWall prefab is missing, the wave is drawn with spray only");
                    return;
                }
                Vector2 start = ColumnPoint(0);
                GameObject go = Instantiate(prefab, start, Quaternion.identity, transform);
                wall = go.GetComponent<WaterWall>();
                if (wall == null)
                {
                    LogWallOnce("the WaterWall prefab has no WaterWall on it, the wave is drawn with spray only");
                    Destroy(go);
                    return;
                }
                wall.parentObject = s.Caster;
                wall.SetSkillData(s.SkillCategory, s.SkillID, s.TickLevel, s.Signature);
                wall.wavePoolSize = s.Columns;
                wall.maxWaveCount = s.Columns;
                wall.spacing = s.Spacing;
                wall.initSpawnRate = RiseDelay;
                wall.spawnRate = RiseInterval;
                wall.moveVector = side;
                wall.givenNormalVector = s.Direction;
                wall.destroyDelay = WallDestroyDelay;
                // Don't trade damage with enemy waves (Wave.OnWaveCollision), and keep raising
                // columns past one that lands in a level wall instead of stopping there.
                wall.ignoreWaveCollisions = true;
                wall.spawnWaveThroughStopLayermask = true;

                Transform reflect = go.transform.Find("ReflectWall");
                barrier = reflect != null ? reflect.GetComponent<NegateProjectiles>() : null;
                if (barrier != null)
                {
                    barrier.parentObject = s.Caster;
                    barrier.skillCategory = s.SkillCategory;
                    barrier.reflectEventHandlers += OnBlock;
                }
            }
            catch (System.Exception e)
            {
                LogWallOnce($"couldn't raise the game's WaterWall ({e.Message}), the wave is drawn with spray only");
                wall = null;
                barrier = null;
            }
        }

        // The wall's hit area: one box across the whole wall (the game's attack box), or a
        // circle as wide as the wall if the box isn't there.
        private void MakeHitBox()
        {
            float angle = Mathf.Atan2(s.Direction.y, s.Direction.x) * Mathf.Rad2Deg;
            try
            {
                GameObject go = Instantiate(Attack.BoxPrefab, s.Center, Quaternion.Euler(0f, 0f, angle), transform);
                hitAttack = go.GetComponent<Attack>() ?? go.GetComponentInChildren<Attack>();
                hitBox = go.GetComponent<BoxCollider2D>() ?? go.GetComponentInChildren<BoxCollider2D>();
                if (hitAttack == null || hitBox == null)
                {
                    Destroy(go);
                    hitAttack = null;
                    hitBox = null;
                }
            }
            catch
            {
                hitAttack = null;
                hitBox = null;
            }
            if (hitAttack == null)
            {
                try
                {
                    GameObject go = Instantiate(Attack.CirclePrefab, s.Center, Quaternion.identity, transform);
                    hitAttack = go.GetComponent<Attack>();
                    var circle = go.GetComponent<CircleCollider2D>();
                    if (circle != null)
                        circle.radius = s.Width * 0.5f;
                }
                catch (System.Exception e)
                {
                    TidalWallPlugin.Log($"Tidal Wall: couldn't make the wave's hit area, it can't push anything: {e.Message}");
                    hitAttack = null;
                    return;
                }
            }
            if (hitAttack == null)
                return;

            hitAttack.gameObject.SetActive(true);
            hitAttack.enabled = true;
            if (hitBox != null)
            {
                // Local x is the aim (the rotation above), local y runs along the wall.
                hitBox.size = new Vector2(s.Depth, s.Width + WidthMargin);
                hitBox.offset = new Vector2(FrontOffset * 0.5f, 0f);
            }
            // A signature cast's hits are marked as the game marks its own (isUltimate), so they
            // don't refill the gauge they just spent.
            hitAttack.SetAttackInfo(s.SkillCategory, s.SkillID, s.TickLevel, s.Signature);
            hitAttack.knockbackOverwriteVector = s.Direction;
            hitCollider = hitAttack.collider != null ? hitAttack.collider : hitAttack.GetComponent<Collider2D>();
            hitObjID = hitAttack.AttackerObjID;
            if (hitCollider != null)
                hitCollider.enabled = false;
            if (!loggedBox)
            {
                loggedBox = true;
                string area = hitCollider != null ? $"{hitCollider.GetType().Name} about {hitCollider.bounds.size.x:0.0} x {hitCollider.bounds.size.y:0.0}"
                    : "no collider!";
                TidalWallPlugin.Log($"Tidal Wall: hit area is the game's {(hitBox != null ? "attack box" : "attack circle")} ({area}, " +
                    $"rigidbody {(hitAttack.GetComponent<Rigidbody2D>() != null ? "yes" : "no")})");
            }
        }

        private void Update()
        {
            if (s == null)
                return;
            float dt = Time.deltaTime;
            phaseTime += dt;
            if (s.CinematicToken != 0 && !restored)
                Cinematic.KeepAlive(s.CinematicToken);
            if (look != null)
                Effects.Try("drawn churn", look.Tend);

            switch (phase)
            {
                case Phase.Rising:
                    TendColumns();
                    if (RiseDone() || phaseTime >= MaxRiseTime)
                        StartHold();
                    break;
                case Phase.Holding:
                    TendColumns();
                    if (phaseTime >= s.HoldTime)
                        StartRoll();
                    break;
                case Phase.Rolling:
                    Roll(dt);
                    break;
                case Phase.Crashed:
                    AfterCrash();
                    break;
            }
        }

        private bool RiseDone() => wall == null || wall.wavesElapsed >= wall.maxWaveCount || !wall.IsSpawning;

        // The WaterWall's columns: their own attacks off (the wave's hit box does the hitting) and,
        // for the signature, made taller.
        private void TendColumns()
        {
            if (wall == null)
                return;
            try
            {
                foreach (Attack attack in wall.GetComponentsInChildren<Attack>(true))
                {
                    if (attack.enabled)
                    {
                        attack.enabled = false;
                        columnAttacksOff++;
                    }
                }
                columns.Clear();
                foreach (Transform child in wall.transform)
                {
                    if (child.name == "ReflectWall" || child.GetComponent<Animator>() == null)
                        continue;
                    columns.Add(child);
                    if (s.ColumnScale != 1f && !scaled.ContainsKey(child))
                    {
                        scaled[child] = child.localScale;
                        child.localScale = Vector3.Scale(child.localScale, new Vector3(s.ColumnScale, s.ColumnScale, 1f));
                    }
                }
            }
            catch (System.Exception e)
            {
                LogWallOnce($"couldn't adjust the WaterWall's columns: {e.Message}");
            }
        }

        private void StartHold()
        {
            TendColumns();
            CenterWall();
            if (look != null)
                Effects.Try("drawn crests", () => look.Crests(columns, wallUp, transform));
            phase = Phase.Holding;
            phaseTime = 0f;
            if (hitCollider != null)
                hitCollider.enabled = true;
        }

        // The prefab decides where its first column goes; line the risen wall up on the aim.
        private void CenterWall()
        {
            if (wall == null)
                return;
            Vector2 sum = Vector2.zero;
            int up = 0;
            foreach (Transform column in columns)
            {
                if (column != null && column.gameObject.activeSelf)
                {
                    sum += (Vector2)column.position;
                    up++;
                }
            }
            Vector2 shift = Vector2.zero;
            wallUp = up > 0;
            columnsUp = up;
            if (wallUp)
            {
                shift = (Vector2)transform.position - sum / up;
                wall.transform.position += (Vector3)shift;
            }
            LogWallOnce($"the game's WaterWall: {up} of {s.Columns} columns up (centred by {shift.magnitude:0.00}), " +
                $"{columnAttacksOff} column attacks off, projectile barrier {(barrier != null ? (barrier.reflect ? "reflects" : "negates") : "missing")}");
        }

        private void StartRoll()
        {
            phase = Phase.Rolling;
            phaseTime = 0f;
            nextPulse = 0f;
            loop = Effects.FollowSound("WaterLoop", transform, s.Signature ? 0.7f : 0.85f);
            if (s.Signature)
                TidalWallPlugin.Log($"Tidal Wall: signature beat 3/4 rush: {s.Distance:0.0} at {s.Speed:0.0}/s, " +
                    $"a push every {s.TickInterval:0.00} s with hit-stop {TickHitStopFrames} frames on contact");
        }

        private void Roll(float dt)
        {
            if (hitThisPulse && pulse != lastHitStopPulse)
            {
                // A push landed: the wall shudders (the WaterWall's own hit reaction) and, for the
                // signature, the hit lands with weight.
                lastHitStopPulse = pulse;
                PlayColumns("Reflect");
                if (s.Signature)
                {
                    Effects.HitStop(TickHitStopFrames);
                    Effects.Shake(TickShake);
                }
            }

            if (phaseTime >= nextPulse)
            {
                nextPulse += s.TickInterval;
                Pulse();
            }

            Vector2 pos = transform.position;
            Vector2 front = pos + s.Direction * FrontOffset;
            float step = s.Speed * dt;
            if (WallAhead(front, front + s.Direction * (step + WallLookahead)))
            {
                Crash("broke on a wall");
                return;
            }
            transform.position = pos + s.Direction * step;
            rolled += step;
            if (look != null)
                Effects.Try("drawn trail", () => look.Rolled(step, transform.position));

            sprayClock += dt;
            float interval = s.Signature ? SignatureSprayInterval : SprayInterval;
            while (sprayClock >= interval)
            {
                sprayClock -= interval;
                Spray();
            }

            if (rolled >= s.Distance)
                Crash("crashed at the end of its run");
        }

        // Switches the hit area off and on again so it hits whatever is inside it now.
        private void Pulse()
        {
            pulse++;
            hitThisPulse = false;
            if (hitCollider != null)
            {
                hitCollider.enabled = false;
                hitCollider.enabled = true;
            }
        }

        private void Spray()
        {
            sprays++;
            Vector2 at = (Vector2)transform.position + s.Direction * FrontOffset
                + side * Random.Range(-s.Width * 0.5f, s.Width * 0.5f);
            Effects.Drops(at, s.Signature ? 4 : 2, s.Signature);
            if (sprays % 3 == 0)
                Effects.Splash(at, s.Direction);
            if (look != null)
                Effects.Try("drawn spray", () => look.Spray(at));
            if (!wallUp)
            {
                // No WaterWall to look at: twice the spray.
                Vector2 more = (Vector2)transform.position + side * Random.Range(-s.Width * 0.5f, s.Width * 0.5f);
                Effects.Drops(more, 3, true);
            }
        }

        private bool WallAhead(Vector2 from, Vector2 to)
        {
            try
            {
                foreach (RaycastHit2D hit in Physics2D.LinecastAll(from, to, ChaosCollisions.layerAllWall))
                {
                    if (hit.collider != null && !hit.collider.transform.IsChildOf(transform))
                        return true;
                }
            }
            catch
            {
            }
            return false;
        }

        private void PlayColumns(string anim)
        {
            foreach (Transform column in columns)
            {
                try
                {
                    if (column != null && column.gameObject.activeSelf)
                        column.GetComponent<Animator>().Play(anim, -1, Random.Range(0f, 0.25f));
                }
                catch
                {
                }
            }
        }

        private void Crash(string why)
        {
            phase = Phase.Crashed;
            phaseTime = 0f;
            easeClock = 0f;
            crashReason = why;
            StopLoop();

            // The crash: one last, harder hit across a deeper area.
            if (hitAttack != null && hitCollider != null)
            {
                hitAttack.ChangeAttackInfoLevel(s.CrashLevel);
                hitAttack.knockbackOverwriteVector = s.Direction;
                if (hitBox != null)
                {
                    hitBox.size = new Vector2(s.Depth * CrashDepthScale, s.Width + WidthMargin * 2f);
                    hitBox.offset = new Vector2(s.Depth * (CrashDepthScale - 1f) * 0.5f, 0f);
                }
                hitCollider.enabled = false;
                hitCollider.enabled = true;
            }

            Vector2 center = (Vector2)transform.position + s.Direction * FrontOffset;
            int bursts = s.Signature ? 5 : 3;
            for (int i = 0; i < bursts; i++)
            {
                float t = bursts == 1 ? 0f : i / (bursts - 1f) - 0.5f;
                Vector2 at = center + side * (t * s.Width * 0.85f);
                Effects.BigSplash(at, s.Signature ? 1.5f : 0.8f);
                Effects.Burst(at);
                Effects.Drops(at, s.Signature ? 14 : 8, true);
                if (s.Signature)
                    Effects.Ice(at, 25);
                if (look != null)
                {
                    int burst = i;
                    Effects.Try("drawn crash", () => look.CrashBurst(at, burst));
                }
            }
            if (look != null)
                Effects.Try("drawn crash end", look.Crash);
            Effects.Sound("DelugeBomb", center, s.Signature ? 0.8f : 1f);
            if (wall != null)
            {
                try
                {
                    wall.DestroyWave();
                }
                catch
                {
                }
            }

            if (s.Signature)
            {
                // Beat 4: the crash. Time all but stops and eases back, the room flashes, the
                // camera and light ease back to normal in AfterCrash.
                Effects.DropRing(center, s.Width * 0.3f, 3);
                Effects.Sound("DelugeBomb", center, 0.6f);
                Effects.Sound("IceBreak", center, 0.8f);
                Effects.Shake(CrashShake, true);
                Effects.SlowMo(CrashSlowMoTime, CrashSlowMoScale);
                Cinematic.SetLight(s.CinematicToken, CrashFlash);
                TidalWallPlugin.Log($"Tidal Wall: signature beat 4/4 crash ({why}) after {rolled:0.0}: slow motion " +
                    $"{CrashSlowMoTime:0.00} s from x{CrashSlowMoScale:0.00}, light flash x{CrashFlash:0.0}, shake {CrashShake:0.0}, " +
                    $"camera and light easing back over {EaseBackTime:0.0} s");
            }
            else
            {
                Effects.Shake(0.6f);
            }
        }

        private void AfterCrash()
        {
            if (hitCollider != null && hitCollider.enabled && phaseTime >= CrashActiveTime)
                hitCollider.enabled = false;

            if (s.CinematicToken != 0 && !restored)
            {
                // Real time, so the slow motion doesn't drag the ease out.
                easeClock += Time.unscaledDeltaTime;
                if (easeClock >= FlashTime)
                {
                    float t = Mathf.Clamp01((easeClock - FlashTime) / EaseBackTime);
                    float eased = t * t * (3f - 2f * t);
                    Cinematic.SetLight(s.CinematicToken, Mathf.Lerp(CrashFlash, 1f, eased));
                    Cinematic.SetZoom(s.CinematicToken, Mathf.Lerp(crashZoom, 0f, eased));
                    if (t >= 1f)
                    {
                        restored = true;
                        Cinematic.Restore("crash finished", s.CinematicToken);
                    }
                }
            }

            if (phaseTime >= LingerTime)
            {
                Report();
                Destroy(gameObject);
            }
        }

        // Every bit of damage this wave's hit area deals, as the game finally applies it
        // (Health.globalTakeDamageActualHandlers: after crits, resistances and overkill).
        private void OnDamage(AttackInfo info, Entity attacker, Entity hurt)
        {
            if (info == null || hitAttack == null || info.atkObjID != hitObjID)
                return;
            hits++;
            damage += info.damage;
            hitThisPulse = true;
            if (hurt == null)
                return;
            if (look != null)
                Effects.Try("drawn churn hit", () => look.Hit(hurt));
            int id = hurt.GetInstanceID();
            if (hurt is Enemy)
                enemies[id] = hurt;
            else
                objects.Add(id);
        }

        private void OnBlock(NegateProjectiles negate, Collider2D other)
        {
            blocked++;
        }

        private void StopLoop()
        {
            try
            {
                if (loop != null)
                    loop.Stop();
            }
            catch
            {
            }
            loop = null;
        }

        private void Report()
        {
            if (reported)
                return;
            reported = true;
            int frozen = 0;
            foreach (Entity enemy in enemies.Values)
            {
                try
                {
                    if (enemy != null && enemy.fsm != null && enemy.fsm.currentStateName.Contains("Frozen"))
                        frozen++;
                }
                catch
                {
                }
            }
            string others = objects.Count > 0 ? $" (+{objects.Count} objects)" : string.Empty;
            TidalWallPlugin.Log($"Tidal Wall: {s.Variant} wave {crashReason} after rolling {rolled:0.0} of {s.Distance:0.0}: " +
                $"swept {enemies.Count} enemies{others}, {hits} hits, {damage} damage, {frozen} frozen, " +
                $"{blocked} projectiles blocked ({columnsUp} of {s.Columns} water columns)");
            if (look != null)
                TidalWallPlugin.Log(look.Summary());
        }

        // However this object goes (the crash run out, a level load, anything else), nothing it
        // changed is left behind.
        private void OnDestroy()
        {
            Health.globalTakeDamageActualHandlers -= OnDamage;
            if (s == null)
                return;
            if (barrier != null)
                barrier.reflectEventHandlers -= OnBlock;
            StopLoop();
            if (look != null)
                Effects.Try("drawn end", look.End);
            if (phase != Phase.Crashed)
                crashReason = "was removed before it crashed";
            Report();
            if (s != null && s.CinematicToken != 0 && !restored)
            {
                restored = true;
                Cinematic.Restore("wave removed before the crash finished", s.CinematicToken);
            }
        }

        private static void LogWallOnce(string message)
        {
            if (loggedWall)
                return;
            loggedWall = true;
            TidalWallPlugin.Log("Tidal Wall: " + message);
        }
    }
}
