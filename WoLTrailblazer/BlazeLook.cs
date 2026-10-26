using UnityEngine;

namespace WoLTrailblazer
{
    // Trailblazer's drawn fire (FireArt, in the game's own fire style), on top of the game's flames
    // and Searing Rush's columns that FlameTrail already shows (those stay as they are), using the
    // game's own sprites and effects where they fit:
    // - the cast: a ring of flame bursting out over the floor from the stamp, the game's flame
    //   circle burst and embers flying, and the game's own scorch mark under it;
    // - while ablaze: flame tongues licking up round the wizard (two at a walk, up to five and
    //   bigger as the speed level climbs; they lean back as the wizard runs), embers flicking off
    //   behind them (more the faster they go), and the ring of fire drawn on the floor at the
    //   size of its hit circle;
    // - the trail: each patch gets the game's own flames (its meteor's burning flame, bigger at
    //   higher levels) and leaves the game's burn mark with a glow in it that cools and fades;
    //   when a patch goes out its flames burst into sparks as the game's do;
    // - burning out: a column of fire flares up round the wizard and breaks into flecks, with a
    //   burst of embers, the game's flame burst, smoke and burn mark.
    // Everything is a pooled sprite (Fx), capped, and put back when the fire's done with it (and
    // on a level load). A game sprite or effect that isn't loaded is drawn instead (logged once).
    public class BlazeLook
    {
        // The game's flames: the top row of its MeteorStrike particle sheet, 25x60 tiles (the
        // flame burning down from a tall tongue to a round one, then bursting into sparks). Each
        // tier flickers back and forth through a few of them.
        private const string FlameSheet = "MeteorStrike";
        private const string GameFlameName = "MeteorStrike flame tiles";
        private const int FlameTileWidth = 25, FlameTileHeight = 60;
        private const int FlameRowY = 452;                  // the row's bottom, in the sheet's pixels from the bottom
        private static readonly Vector2 FlamePivot = new Vector2(0.5f, 2.5f / 60f); // the flame's base
        private static readonly int[][] FlameTiles = { new[] { 6, 7, 8, 7 }, new[] { 5, 6, 7, 8, 7, 6 }, new[] { 4, 5, 6, 7, 6, 5 } };
        private static readonly int[] DyingTiles = { 9, 10, 11 };
        // The game's burn marks for the small, medium and large char marks.
        private static readonly string[] BurnMarks = { "BurnMarkXSmallEmitter", "BurnMarkSmallEmitter", "BurnMarkMedEmitter" };
        private const float GlowAfter = 0.5f;        // seconds the glow stays after its patch (the game's burn mark lasts 2 s)
        private const float GlowFade = 0.4f;
        private const int IgniteBurstFlames = 16;    // the game's flame circle burst at the cast (as Flame Shield's)
        private static bool loggedMarks;

        // How many of each at once (past that the oldest is reused). The trail can hold up to
        // FlameTrail's 40 patches, each with up to two flames and a char mark.
        private const int BodyFlameCap = 12;
        private const int EmberCap = 36;
        private const int MarkCap = 56;
        private const int TrailFlameCap = 84;
        private const int DyingCap = 16;
        private const int RingCap = 4;
        private const int BurstCap = 3;
        private const int FlareCap = 3;

        // Draw order on the floor layer: the ring of fire over the char marks.
        private const int MarkSlot = 1;
        private const int RingSlot = 5;
        private const int BurstSlot = 1;

        // The flames on the wizard: where each stands (from their feet, in world units) and
        // whether it's drawn behind them. The first ones are lit first.
        private static readonly Vector2[] FlameSlots =
        {
            new Vector2(-0.24f, 0.06f), new Vector2(0.24f, 0.08f), new Vector2(-0.32f, 0.45f),
            new Vector2(0.3f, 0.5f), new Vector2(0f, 0.78f)
        };
        private static readonly bool[] FlameBehind = { false, false, true, true, true };
        private const float FlamePhaseStep = 0.13f;  // seconds between neighbouring flames' flickers
        private const float LeanSpeed = 1f;          // moving sideways faster than this, the flames lean back
        private const float JitterPixels = 1f;       // each flame shifts up to this each flicker (licking about)

        private const float EmberIdleRate = 3f;      // embers a second standing still
        private const float EmberRate = 4f;          // moving: this plus EmberRatePerLevel a level
        private const float EmberRatePerLevel = 2f;
        private const float EmberRise = 0.7f, EmberRiseMax = 1.4f;   // their upward speed
        private const float EmberBack = 0.4f, EmberBackMax = 1f;     // thrown back against the movement
        private const float EmberGravity = -0.9f;    // they slow as they rise
        private const float EmberDrag = 1.2f;
        private const float MovingSpeed = 0.5f;      // faster than this counts as moving

        private const float RingAlpha = 0.85f;       // the ring of fire lets the floor show through a little
        private const float TrailFlameSpread = 0.18f;
        private const float TrailFlameBackstop = 1f; // seconds past its patch a trail flame lasts if never put out
        private const float MarkAfter = 1.8f;        // seconds a char mark stays after its patch burns out
        private const float MarkFade = 0.8f;
        private const float MarkWarm = 0.5f;         // seconds a mark stays warm once its patch is out
        private const float IgniteRadius = 1.1f;     // the ignition burst, in world units
        private const int BurstEmbers = 10;
        private const float BurstEmberSpeed = 1.2f, BurstEmberSpeedMax = 2f;
        private const float FlareMarkHot = 0.4f;
        private const int FlareBurstFlames = 14;     // the game's flame burst at the flare-up
        private const int FlareSmoke = 3;
        private const float FlareSmokeHeight = 1.2f;

        // A patch of the trail's drawn fire.
        public class PatchLook
        {
            internal FxHandle[] flames;
            internal int tier;
            internal bool game;   // the game's own flame frames (so they die down as the game's do)
        }

        private readonly Player player;
        private readonly float ppu;
        private readonly FxHandle[] flames = new FxHandle[FlameSlots.Length];
        private int flameTier = -1, flameLean = 2;
        private FxHandle ring;
        private int ringRadius = -1;
        private float nextEmber;

        private SpriteRenderer Body => player != null ? player.spriteRenderer : null;

        private BlazeLook(Player player)
        {
            this.player = player;
            ppu = Fx.Ppu(player.spriteRenderer);
            FireArt.Use(ppu);
        }

        public static BlazeLook For(Player player)
        {
            try
            {
                return player != null && player.spriteRenderer != null && TrailblazerPlugin.Drawn ? new BlazeLook(player) : null;
            }
            catch (System.Exception e)
            {
                Fx.Fail("drawn look", e);
                return null;
            }
        }

        private static int Tier(int level) => level <= 3 ? 0 : level <= 7 ? 1 : 2;

        private Vector2 Feet => player.transform.position;

        private Vector2 Velocity
        {
            get
            {
                try
                {
                    return player.rigidbody2D != null ? player.rigidbody2D.velocity : Vector2.zero;
                }
                catch
                {
                    return Vector2.zero;
                }
            }
        }

        // ---- The cast ----

        public void Ignite(Vector2 position)
        {
            try
            {
                Sprite[] frames = FireArt.IgniteBurst(FireArt.Pixels(IgniteRadius, 8, 40));
                FxSprite burst = Fx.Get("ignition burst", BurstCap).Play(frames, FireArt.BurstFrameTime, false, FireArt.BurstFrameTime * frames.Length).At(position);
                Fx.OnGround(burst.Renderer, BurstSlot, Body);
                EmberBurst(position, BurstEmbers);
                Fx.Ready("ignition burst", frames.Length, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("ignition burst", e);
            }
            // The game's own flame burst and scorch, as its fire arcana leave them (Flame Shield's
            // burst; FlameKnight's and FlakField's burn mark).
            Fx.Effect("FlameCircleBurstEmitter", IgniteBurstFlames, position, ParticleEffectOverrides.Radius0p75);
            Fx.Effect("BurnMarkMedEmitter", 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
        }

        // ---- While ablaze (every frame, after the wizard has moved) ----

        // level: the speed level (1 to 10); ringUnits: the ring of fire's hit radius.
        public void Lit(int level, float ringUnits)
        {
            if (player == null || Body == null)
                return;
            Vector2 velocity = Velocity;
            BodyFlames(level, velocity);
            Ring(ringUnits);
            Embers(level, velocity);
        }

        private void BodyFlames(int level, Vector2 velocity)
        {
            try
            {
                int tier = Tier(level);
                int count = Mathf.Clamp(2 + (level - 1) / 3, 2, FlameSlots.Length);
                // Flames trail back as the wizard runs.
                int lean = velocity.x > LeanSpeed ? -1 : velocity.x < -LeanSpeed ? 1 : 0;
                Sprite[] frames = FireArt.Tongue(tier, lean);
                bool restyle = tier != flameTier || lean != flameLean;
                flameTier = tier;
                flameLean = lean;
                float scale = 1f + 0.12f * tier;   // the bigger flames stand a little further out
                for (int i = 0; i < FlameSlots.Length; i++)
                {
                    if (i >= count)
                    {
                        flames[i].Release();
                        continue;
                    }
                    FxSprite flame = flames[i].Sprite;
                    if (flame == null)
                    {
                        flame = Fx.Get("body flames", BodyFlameCap).Play(frames, FireArt.TongueFrameTime, true, 0f).Phase(i * FlamePhaseStep);
                        flames[i] = new FxHandle(flame);
                    }
                    else if (restyle)
                        flame.Swap(frames);
                    // A pixel this way or that each flicker, so the flames lick about.
                    int cycle = Mathf.FloorToInt((Time.time + i * FlamePhaseStep) / (FireArt.TongueFrameTime * FireArt.TongueFrames));
                    float jx = Mathf.Round((PixelCanvas.Hash(cycle, i, 11) * 2f - 1f) * JitterPixels) / ppu;
                    flame.At(Feet + FlameSlots[i] * scale + new Vector2(jx, 0f));
                    Fx.NearBody(flame.Renderer, FlameBehind[i] ? -1 : 1, Body);
                }
                Fx.Ready("body flames", FireArt.TongueCount, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("body flames", e);
                ReleaseFlames();
            }
        }

        private void Ring(float units)
        {
            try
            {
                int radius = FireArt.Pixels(units, 8, 48);
                Sprite[] frames = FireArt.FireRing(radius);
                FxSprite sprite = ring.Sprite;
                if (sprite == null)
                {
                    sprite = Fx.Get("ring of fire", RingCap).Play(frames, FireArt.RingFrameTime, true, 0f).Alpha(RingAlpha);
                    Fx.OnFloor(sprite.Renderer, RingSlot, Body);
                    ring = new FxHandle(sprite);
                }
                else if (radius != ringRadius)
                    sprite.Swap(frames);
                ringRadius = radius;
                sprite.At(Feet);
                Fx.Ready("ring of fire", frames.Length, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("ring of fire", e);
                ring.Release();
            }
        }

        private void Embers(int level, Vector2 velocity)
        {
            if (Time.time < nextEmber)
                return;
            bool moving = velocity.sqrMagnitude > MovingSpeed * MovingSpeed;
            float rate = moving ? EmberRate + EmberRatePerLevel * level : EmberIdleRate;
            nextEmber = Time.time + 1f / rate;
            try
            {
                Vector2 back = moving ? -velocity.normalized * Random.Range(EmberBack, EmberBackMax) : Vector2.zero;
                Vector2 spot = Feet + new Vector2(Random.Range(-0.25f, 0.25f), Random.Range(0.1f, 0.9f));
                Ember(spot, back + new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(EmberRise, EmberRiseMax)), 2);
                Fx.Ready("embers", FireArt.EmberTimes.Length, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("embers", e);
            }
        }

        private void Ember(Vector2 spot, Vector2 velocity, int order)
        {
            Sprite[] frames = FireArt.Embers();
            float life = 0f;
            foreach (float t in FireArt.EmberTimes)
                life += t;
            FxSprite ember = Fx.Get("embers", EmberCap).Timeline(frames, FireArt.EmberTimes, life).At(spot);
            ember.Velocity = velocity;
            ember.Accel = new Vector2(0f, EmberGravity);
            ember.Drag = EmberDrag;
            Fx.NearBody(ember.Renderer, order, Body);
        }

        private void EmberBurst(Vector2 position, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) * PixelCanvas.Tau / count;
                Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.7f);
                Ember(position + dir * 0.2f, dir * Random.Range(BurstEmberSpeed, BurstEmberSpeedMax) + new Vector2(0f, EmberRise), 2);
            }
        }

        // ---- The trail ----

        // A patch was dropped: its flames (one over a Searing Rush column, two without) burn as long
        // as it does, the game's burn mark is left under it, and a glow in it cools as it burns out.
        public PatchLook Patch(Vector2 position, int level, float life, bool hasColumn)
        {
            var look = new PatchLook { tier = Tier(level), flames = new FxHandle[hasColumn ? 1 : 2] };
            Mark(position, look.tier, life * 0.55f, life * 0.45f, life);
            try
            {
                Sprite[] game = GameFlames(FlameTiles[look.tier]);
                Fx.Source("trail flames", game, GameFlameName);
                look.game = game != null;
                Sprite[] frames = game ?? FireArt.Tongue(look.tier, 0);
                for (int i = 0; i < look.flames.Length; i++)
                {
                    Vector2 spot = position + new Vector2(Random.Range(-TrailFlameSpread, TrailFlameSpread), Random.Range(-0.05f, 0.05f));
                    FxSprite flame = Fx.Get("trail flames", TrailFlameCap).Play(frames, FireArt.TongueFrameTime, true, life + TrailFlameBackstop)
                        .Phase(Random.value).At(spot);
                    flame.Renderer.flipX = Random.value < 0.5f;
                    // Sorted as Searing Rush's columns on the trail are: just behind the wizard as it was dropped.
                    Fx.NearBody(flame.Renderer, -1, Body);
                    look.flames[i] = new FxHandle(flame);
                }
                Fx.Ready("trail flames", game != null ? game.Length : FireArt.Tiers * FireArt.TongueFrames, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("trail flames", e);
            }
            return look;
        }

        // A char mark: the game's own burn mark (sized by the tier) with the drawn glow cooling in
        // it (hot, then cooling, for the times given, then warm a moment), or the drawn soot and
        // glow together if the game's burn marks can't be had.
        private void Mark(Vector2 position, int tier, float hot, float cooling, float life)
        {
            try
            {
                bool game = Fx.Effect(BurnMarks[tier], 1, position, ParticleEffectOverrides.StartLifeTimeTwo);
                int variant = Random.Range(0, FireArt.CharVariants);
                Sprite[] mark = game ? FireArt.CharGlow(tier, variant) : FireArt.CharMark(tier, variant);
                float after = game ? GlowAfter : MarkAfter;
                float[] times = { hot, cooling, MarkWarm, 60f };
                FxSprite sprite = Fx.Get("char marks", MarkCap).Timeline(mark, times, life + after).Fade(game ? GlowFade : MarkFade).At(position);
                sprite.Renderer.flipX = Random.value < 0.5f;
                Fx.OnFloor(sprite.Renderer, MarkSlot, Body);
                Fx.Ready("char marks", FireArt.CharCount, ppu, Body);
                if (!loggedMarks)
                {
                    loggedMarks = true;
                    TrailblazerPlugin.Log(game
                        ? "Trailblazer: char marks use the game's burn marks (" + string.Join(", ", BurnMarks) + ") with a drawn glow"
                        : "Trailblazer: the game's burn marks aren't to hand, char marks drawn instead");
                }
            }
            catch (System.Exception e)
            {
                Fx.Fail("char marks", e);
            }
        }

        // A patch went out: its flames die down (the game's flame bursting into sparks, or a drawn
        // flame shrinking to an ember); quiet: just put away.
        public void PatchOut(PatchLook look, bool quiet = false)
        {
            if (look == null || look.flames == null)
                return;
            foreach (FxHandle handle in look.flames)
            {
                FxSprite flame = handle.Sprite;
                if (flame == null)
                    continue;
                Vector2 spot = flame.transform.position;
                int order = flame.Renderer.sortingOrder, layer = flame.Renderer.sortingLayerID;
                bool flip = flame.Renderer.flipX;
                handle.Release();
                if (quiet)
                    continue;
                try
                {
                    Sprite[] game = look.game ? GameFlames(DyingTiles) : null;
                    Sprite[] frames = game ?? FireArt.Dying(look.tier);
                    FxSprite dying = Fx.Get("dying flames", DyingCap).Play(frames, FireArt.DyingFrameTime, false, FireArt.DyingFrameTime * frames.Length).At(spot);
                    dying.Renderer.flipX = flip;
                    dying.Renderer.sortingLayerID = layer;
                    dying.Renderer.sortingOrder = order;
                    Fx.Ready("dying flames", game != null ? game.Length : FireArt.Tiers * frames.Length, ppu, Body);
                }
                catch (System.Exception e)
                {
                    Fx.Fail("dying flames", e);
                }
            }
            look.flames = null;
        }

        // The game's flame frames: tiles of the top row of its MeteorStrike particle sheet (the
        // flame that burns down and bursts into sparks), or null if that sheet isn't loaded.
        private Sprite[] GameFlames(int[] tiles)
        {
            var rects = new Rect[tiles.Length];
            for (int i = 0; i < tiles.Length; i++)
                rects[i] = new Rect(tiles[i] * FlameTileWidth, FlameRowY, FlameTileWidth, FlameTileHeight);
            return GameSprites.Cut(FlameSheet, rects, FlamePivot, ppu);
        }

        // ---- Burning out ----

        public void BurntOut(Vector2 position, int level)
        {
            ReleaseFlames();
            ring.Release();
            ringRadius = -1;
            try
            {
                Sprite[] frames = FireArt.FlareUp();
                float life = 0f;
                foreach (float t in FireArt.FlareTimes)
                    life += t;
                FxSprite flare = Fx.Get("flare-up", FlareCap).Timeline(frames, FireArt.FlareTimes, life).At(position);
                Fx.NearBody(flare.Renderer, 1, Body);
                EmberBurst(position, BurstEmbers);
                Fx.Ready("flare-up", frames.Length, ppu, Body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("flare-up", e);
            }
            Mark(position, Tier(level), FlareMarkHot, FlareMarkHot, FlareMarkHot * 2f);
            // The game's flame burst and smoke, as a magma bomb or a doused torch gives off.
            Fx.Effect("FlameCircleBurstEmitter", FlareBurstFlames, position, ParticleEffectOverrides.Radius0p5);
            Fx.Effect("SmokeSmallEmitter", FlareSmoke, position + Vector2.up * FlareSmokeHeight);
        }
        // The wizard's own flames and ring go (the trail's look is put out patch by patch).
        public void Off()
        {
            ReleaseFlames();
            ring.Release();
            ringRadius = -1;
        }

        private void ReleaseFlames()
        {
            for (int i = 0; i < flames.Length; i++)
                flames[i].Release();
            flameTier = -1;
        }
    }
}
