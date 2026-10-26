using System.Collections.Generic;
using UnityEngine;

namespace WoLThunderhead
{
    // The drawn look of a standard or enhanced leap (the signature storm has its own, see
    // SignatureCinematic), layered on the game's bolts, sparks and dust, in the game's own
    // lightning style (ThunderArt) and with its own sprites where they fit:
    // - take-off: a ring of lightning bursting out over the ground from their feet;
    // - in the air: the game's little lightning frames (MiniLightning) crackling round the wizard
    //   (more when enhanced), lightning flickering inside the storm cloud (StormCloud), and a
    //   lightning circle on the ground wherever the next volley's bolts will land, closing up and
    //   brightening from yellow to white and flashing just before they strike;
    // - each strike: the game's lightning core bursting (LightningCoreLarge), the game's burn mark,
    //   and cracks glowing white over it as they cool (with a game lightning spark for debris);
    // - before the slam: a charged lightning circle on the ground the size of the slam;
    // - the slam: a shockwave of lightning racing out to the slam's edge and the game's bigger
    //   burn mark with cracks glowing over it.
    // Everything is a pooled sprite (Fx): the marks and rings play out by themselves and the
    // glyphs and the landing ring go back as soon as the cast ends, however it ends. A game
    // sprite that isn't loaded is drawn instead (logged once).
    public class ThunderVisuals
    {
        // The game's sprites used, by name (sprites.txt in the art dump), and its burn marks.
        private const string ArcSprites = "MiniLightning";       // 16x16 jagged bolts, 6 frames
        private const int ArcSpriteCount = 6;
        private const string FlashSprites = "LightningCoreLarge"; // 32x32 lightning core bursting, 6 frames
        private const int FlashSpriteCount = 6;
        private const string StrikeBurnMark = "BurnMarkXSmallEmitter"; // as the signature's strikes leave
        private const string SlamBurnMark = "BurnMarkMedEmitter";     // as the game's fire slams leave
        private const int FlashCap = 16;
        private const int FlashSlot = 4;

        // How many of each at once (past that the oldest is reused).
        private const int GlyphCap = 16;
        private const int ScorchCap = 30;
        private const int ArcCap = 6;
        private const int RingCap = 2;
        private const int WaveCap = 4;
        private const int CraterCap = 3;

        // Draw order within the floor and ground layers: newer effects over older ones.
        private const int ScorchSlot = 1;
        private const int CraterSlot = 2;
        private const int GlyphSlot = 1;
        private const int RingSlot = 2;
        private const int WaveSlot = 3;

        private const float GlyphFadeIn = 0.06f;     // seconds the glyphs come in at half strength
        private const float GlyphFlashLead = 0.08f;  // they flicker with the flash frame this long before the strike
        private const float FlickerTime = 0.03f;     // seconds per flicker
        private const float ScorchLife = 1.4f;       // the glowing cracks (the burn mark under them is the game's)
        private const float ScorchFade = 0.6f;
        private const float CraterLife = 2.4f;
        private const float CraterFade = 0.8f;
        private const float CraterShare = 0.5f;      // the crater's radius, of the slam's
        private const float LandingRingLead = 0.35f; // seconds before the dive the landing ring comes up
        private const float RingFlashFrom = 0.85f;   // through the dive it flickers from here
        private const float TakeOffWaveRadius = 0.9f;
        private const float ArcGapMin = 0.07f, ArcGapMax = 0.14f; // seconds between arcs
        private const float EnhancedArcGap = 0.7f;   // enhanced: that much of the gap (more arcs)
        private const float ArcMinLift = 0.3f;       // no arcs until they're this far up
        private const float ArcSpread = 0.25f;       // how far from the middle of the wizard an arc starts
        private const float BodyHeight = 0.55f;      // the middle of the wizard's picture, above their feet
        private const int StrikeSparks = 1;          // the game's lightning sparks at each strike
        private const int SlamSparks = 6;
        private const int TakeOffSparks = 3;

        private readonly SpriteRenderer body;
        private readonly bool enhanced;
        private readonly float ppu;
        private readonly int strikeRadius;   // in pixels
        private readonly float slamRadius;   // in world units
        private readonly List<FxHandle> glyphs = new List<FxHandle>();
        private readonly List<Vector2> glyphOffsets = new List<Vector2>();
        private float glyphFrom, glyphUntil;
        private FxHandle ring;
        private float ringFrom = -1f;
        private float nextArc;
        private bool landed;

        private ThunderVisuals(Player player, bool enhanced, float strikeSize, float slamSize)
        {
            body = player.spriteRenderer;
            this.enhanced = enhanced;
            ppu = Fx.Ppu(body);
            ThunderArt.Use(ppu);
            // The hit circles are the bursts' scale in world units (ElementalBurst sets its
            // collider's radius to it).
            strikeRadius = ThunderArt.Pixels(strikeSize, 5, 40);
            slamRadius = slamSize;
        }

        public static ThunderVisuals On(Player player, bool enhanced, float strikeSize, float slamSize)
        {
            try
            {
                return player != null && player.spriteRenderer != null && ThunderheadPlugin.Drawn
                    ? new ThunderVisuals(player, enhanced, strikeSize, slamSize)
                    : null;
            }
            catch (System.Exception e)
            {
                Fx.Fail("drawn look", e);
                return null;
            }
        }

        public void TakeOff(Vector2 ground)
        {
            Wave(ground, TakeOffWaveRadius, "take-off shockwave");
            Fx.Effect("LightningHitEffect", TakeOffSparks, ground);
        }

        // The glyphs for the next volley: where its bolts will land (offsets from the spot below
        // the wizard, as the volley places them), charging from `from` to the strike at `until`.
        public void Telegraph(List<Vector2> offsets, float from, float until, Vector2 ground)
        {
            ClearGlyphs();
            try
            {
                Sprite[] frames = ThunderArt.StrikeGlyph(strikeRadius);
                foreach (Vector2 offset in offsets)
                {
                    FxSprite glyph = Fx.Get("strike glyph", GlyphCap).Hold(frames).At(Spot(ground, offset));
                    Fx.OnGround(glyph.Renderer, GlyphSlot, body);
                    glyph.SetAlpha(0.5f);
                    glyphs.Add(new FxHandle(glyph));
                    glyphOffsets.Add(offset);
                }
                glyphFrom = from;
                glyphUntil = until;
                Fx.Ready("strike glyph", frames.Length, ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("strike glyph", e);
                ClearGlyphs();
            }
        }

        // Where a volley puts a strike (its circle squashed into the floor's perspective).
        private static Vector2 Spot(Vector2 ground, Vector2 offset) => ground + new Vector2(offset.x, offset.y * 0.7f);

        // Every frame from take-off to the landing: t is the time since take-off, lift 0 on the
        // ground to 1 at the top.
        public void Air(float t, Vector2 ground, float lift, float riseEnd, float hoverEnd, float crashEnd, Levitator levitator)
        {
            UpdateGlyphs(t, ground, hoverEnd);
            if (landed)
                return;
            UpdateRing(t, ground, riseEnd, hoverEnd, crashEnd);
            UpdateArcs(lift, levitator);
        }

        private void UpdateGlyphs(float t, Vector2 ground, float hoverEnd)
        {
            if (glyphs.Count == 0)
                return;
            // A volley that never came (the hover ended first).
            if (t >= hoverEnd)
            {
                ClearGlyphs();
                return;
            }
            try
            {
                int frame;
                if (glyphUntil - t <= GlyphFlashLead)
                    frame = Mathf.FloorToInt(Time.time / FlickerTime) % 2 == 0 ? ThunderArt.GlyphFlashFrame : ThunderArt.GlyphFrames - 1;
                else
                    frame = Mathf.Clamp(Mathf.FloorToInt((t - glyphFrom) / Mathf.Max(glyphUntil - glyphFrom, 0.01f) * ThunderArt.GlyphFrames),
                        0, ThunderArt.GlyphFrames - 1);
                float alpha = t - glyphFrom < GlyphFadeIn ? 0.5f : 1f;
                for (int i = 0; i < glyphs.Count; i++)
                {
                    FxSprite glyph = glyphs[i].Sprite;
                    if (glyph == null)
                        continue;
                    glyph.Show(frame);
                    glyph.SetAlpha(alpha);
                    glyph.At(Spot(ground, glyphOffsets[i]));
                }
            }
            catch (System.Exception e)
            {
                Fx.Fail("strike glyph", e);
                ClearGlyphs();
            }
        }

        // The volley struck: its glyphs go, and each strike leaves a flash (the game's lightning
        // core), the game's own burn mark, and cracks glowing over it as they cool.
        public void Struck(List<Vector2> spots)
        {
            ClearGlyphs();
            try
            {
                Sprite[] game = GameSprites.Named(FlashSprites, FlashSpriteCount);
                Sprite[] flash = game ?? ThunderArt.ImpactFlash(strikeRadius);
                Fx.Source("strike flash", game, FlashSprites + "0-" + (FlashSpriteCount - 1));
                Sprite[][] variants = ThunderArt.StrikeCracks(strikeRadius);
                foreach (Vector2 spot in spots)
                {
                    FxSprite burst = Fx.Get("strike flash", FlashCap)
                        .Play(flash, ThunderArt.FlashFrameTime, false, ThunderArt.FlashFrameTime * flash.Length).At(spot);
                    burst.Renderer.flipX = Random.value < 0.5f;
                    Fx.OnGround(burst.Renderer, FlashSlot, body);
                    Fx.Effect(StrikeBurnMark, 1, spot);
                    FxSprite scorch = Fx.Get("strike scorch", ScorchCap)
                        .Timeline(variants[Random.Range(0, variants.Length)], ThunderArt.CrackTimes, ScorchLife)
                        .Fade(ScorchFade).At(spot);
                    scorch.Renderer.flipX = Random.value < 0.5f;
                    Fx.OnFloor(scorch.Renderer, ScorchSlot, body);
                    Fx.Effect("LightningHitEffect", StrikeSparks, spot);
                }
                Fx.Ready("strike flash", flash.Length, ppu, body);
                Fx.Ready("strike scorch", ThunderArt.Count(variants), ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("strike scorch", e);
            }
        }

        private void UpdateRing(float t, Vector2 ground, float riseEnd, float hoverEnd, float crashEnd)
        {
            if (t < Mathf.Max(riseEnd, hoverEnd - LandingRingLead))
                return;
            try
            {
                Sprite[] frames = null;
                if (ringFrom < 0f)
                {
                    ringFrom = t;
                    frames = ThunderArt.LandingRing(ThunderArt.Pixels(slamRadius, 8, 60));
                    FxSprite made = Fx.Get("landing ring", RingCap).Hold(frames).At(ground);
                    Fx.OnGround(made.Renderer, RingSlot, body);
                    ring = new FxHandle(made);
                    Fx.Ready("landing ring", frames.Length, ppu, body);
                }
                FxSprite sprite = ring.Sprite;
                if (sprite == null)
                    return;
                float p = Mathf.Clamp01((t - ringFrom) / Mathf.Max(crashEnd - ringFrom, 0.01f));
                int frame = p >= RingFlashFrom
                    ? (Mathf.FloorToInt(Time.time / FlickerTime) % 2 == 0 ? ThunderArt.RingFlashFrame : ThunderArt.RingFrames - 1)
                    : Mathf.Min(Mathf.FloorToInt(p / RingFlashFrom * ThunderArt.RingFrames), ThunderArt.RingFrames - 1);
                sprite.Show(frame);
                sprite.At(ground);
            }
            catch (System.Exception e)
            {
                Fx.Fail("landing ring", e);
                ring.Release();
            }
        }

        private void UpdateArcs(float lift, Levitator levitator)
        {
            if (lift < ArcMinLift || Time.time < nextArc || body == null)
                return;
            nextArc = Time.time + Random.Range(ArcGapMin, ArcGapMax) * (enhanced ? EnhancedArcGap : 1f);
            try
            {
                // The game's own little lightning frames, or drawn ones in eight directions.
                Sprite[] game = GameSprites.Named(ArcSprites, ArcSpriteCount);
                Fx.Source("wizard arcs", game, ArcSprites + "0-" + (ArcSpriteCount - 1));
                Sprite[][] variants = game == null ? ThunderArt.Arcs() : null;
                Sprite[] frames = game ?? variants[Random.Range(0, variants.Length)];
                Vector2 start = (Vector2)body.transform.position + new Vector2(0f, BodyHeight) + Random.insideUnitCircle * ArcSpread;
                FxSprite arc = Fx.Get("wizard arcs", ArcCap)
                    .Play(frames, ThunderArt.ArcFrameTime, false, ThunderArt.ArcFrameTime * frames.Length)
                    .On(body.transform, start);
                arc.Renderer.flipX = Random.value < 0.5f;
                // Just in front of the wizard, wherever their lifted picture is drawn.
                arc.Renderer.sortingLayerID = levitator != null ? levitator.DrawLayer : body.sortingLayerID;
                arc.Renderer.sortingOrder = (levitator != null ? levitator.DrawOrder : body.sortingOrder) + 1;
                Fx.Ready("wizard arcs", game != null ? game.Length : ThunderArt.Count(variants), ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("wizard arcs", e);
            }
        }

        // Crashing down: the landing ring goes, a shockwave races out to the slam's edge and the
        // crater (the game's bigger burn mark) is left with its cracks glowing.
        public void Slam(Vector2 ground)
        {
            landed = true;
            ClearGlyphs();
            ring.Release();
            Wave(ground, slamRadius, "slam shockwave");
            Fx.Effect(SlamBurnMark, 1, ground, ParticleEffectOverrides.StartLifeTimeTwo);
            try
            {
                Sprite[] frames = ThunderArt.Crater(ThunderArt.Pixels(slamRadius * CraterShare, 6, 36));
                FxSprite crater = Fx.Get("slam crater", CraterCap).Timeline(frames, ThunderArt.CraterTimes, CraterLife).Fade(CraterFade).At(ground);
                Fx.OnFloor(crater.Renderer, CraterSlot, body);
                Fx.Ready("slam crater", frames.Length, ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail("slam crater", e);
            }
            Fx.Effect("LightningHitEffect", SlamSparks, ground);
        }

        private void Wave(Vector2 ground, float radius, string visual)
        {
            try
            {
                Sprite[] frames = ThunderArt.Shockwave(ThunderArt.Pixels(radius, 6, 60));
                FxSprite wave = Fx.Get("shockwave", WaveCap)
                    .Play(frames, ThunderArt.WaveFrameTime, false, ThunderArt.WaveFrameTime * frames.Length).At(ground);
                Fx.OnGround(wave.Renderer, WaveSlot, body);
                Fx.Ready(visual, frames.Length, ppu, body);
            }
            catch (System.Exception e)
            {
                Fx.Fail(visual, e);
            }
        }

        // The cast is over (landed, cut short, died): the glyphs and the ring go now; the scorch
        // marks and shockwaves finish by themselves.
        public void Off()
        {
            ClearGlyphs();
            ring.Release();
            landed = true;
        }

        private void ClearGlyphs()
        {
            foreach (FxHandle glyph in glyphs)
                glyph.Release();
            glyphs.Clear();
            glyphOffsets.Clear();
        }
    }
}
