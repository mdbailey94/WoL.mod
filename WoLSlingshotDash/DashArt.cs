using UnityEngine;

namespace WoLSlingshotDash
{
    // Each dash's visuals: the game's own frames for that element where they fit (GameArt.cs, by
    // the names in the game's sheets), otherwise the mod's drawing in the game's style
    // (PixelArt.cs). Each is built the first time it plays, and the log says which it got:
    // "<dash>: visual '<name>' ready (the game's ...)" or "(drawn by the mod ...)".
    //
    // Pivots of the game's frames were measured on its sheets (the art dump), so frames line up
    // as the game plays them: e.g. the whirlwind turns about its base, bolts start at their foot.
    internal static class AirArt
    {
        private const string Owner = "Vacuum Fist";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        // The game's whirlwind (WindEffects_0..12, 128x144 cells): forms (0-2), spins (3-5),
        // rises and breaks up (6-12). Its base sits 46 px up a cell, 61 px across.
        private static readonly Vector2 WhirlPivot = new Vector2(61f / 128f, 46f / 144f);
        private const int WhirlSplit = 46;

        // Charging: the whirlwind forming round your feet then spinning, its far half behind the
        // wizard and its near half in front.
        public static readonly ArtSlot WhirlBack = new ArtSlot(
            () => GameArt.Named(Owner, "whirlwind (far half)", "WindEffects", GameArt.Seq("WindEffects_", 0, 5), WhirlPivot, 0.06f, true,
                loopStart: 3, cropBottom: WhirlSplit),
            () => FxAnim.Build(Owner, "wind ring (far half)", 48, 24, 8, 0.045f, true, Centre, Palettes.Air, (c, f, n) => PixelArt.WindRing(c, f, n, 1)));
        public static readonly ArtSlot WhirlFront = new ArtSlot(
            () => GameArt.Named(Owner, "whirlwind (near half)", "WindEffects", GameArt.Seq("WindEffects_", 0, 5), WhirlPivot, 0.06f, true,
                loopStart: 3, cropTop: WhirlSplit),
            () => FxAnim.Build(Owner, "wind ring (near half)", 48, 24, 8, 0.045f, true, Centre, Palettes.Air, (c, f, n) => PixelArt.WindRing(c, f, n, 2)));
        // Released: the whirlwind left at the launch spot rises and breaks up.
        public static readonly ArtSlot WhirlOut = new ArtSlot(
            () => GameArt.Named(Owner, "whirlwind breaking up", "WindEffects", GameArt.Seq("WindEffects_", 6, 12), WhirlPivot, 0.06f, false,
                fadeFrom: 0.7f),
            null);
        // The rush: the game's wind-trail wisps (WindTrail, 8 frames; rows 4 and 5 make one tall wisp between them, so they're left out).
        public static readonly ArtSlot Trail = new ArtSlot(
            () => GameArt.Grid(Owner, "wind wisps", "WindTrail", 256, 256, 32, 32, new[] { 0, 1, 2, 3, 6, 7 }, 0, 8,
                new Vector2(16f, 16f), 0.035f, false, fadeFrom: 0.65f),
            () => FxAnim.Build(Owner, "gust streak", 28, 9, 5, 0.04f, false, Centre, Palettes.Air, PixelArt.GustStreak));
        // The launch: the game's wind twirl spreading where you take off.
        public static readonly ArtSlot Twirl = new ArtSlot(
            () => GameArt.Grid(Owner, "launch twirl", "WindTwirl", 768, 96, 96, 96, new[] { 0 }, 0, 8,
                new Vector2(49f, 50f), 0.04f, false, fadeFrom: 0.6f),
            () => FxAnim.Build(Owner, "launch gust", 40, 32, 6, 0.045f, false, new Vector2(0.1f, 0.5f), Palettes.Air, PixelArt.GustCone));
        // The punch: the game's wind slash crescent (its arch points up the sprite).
        public static readonly ArtSlot Slash = new ArtSlot(
            () => GameArt.Named(Owner, "punch slash", "WindSlash", GameArt.Seq("WindSlash_", 0, 6), null, 0.045f, false,
                fadeFrom: 0.6f, baseAngle: 90f),
            () => FxAnim.Build(Owner, "punch impact", 32, 32, 5, 0.04f, false, Centre, Palettes.Air, PixelArt.ImpactStar));
        // The vacuum: the game's air vortex, wisps drawn in to a point.
        public static readonly ArtSlot Vortex = new ArtSlot(
            () => GameArt.Grid(Owner, "vacuum vortex", "AirVortex", 1024, 128, 128, 128, new[] { 0 }, 0, 8,
                new Vector2(64f, 64f), 0.05f, false, fadeFrom: 0.8f),
            () => FxAnim.Build(Owner, "vacuum implosion", 48, 48, 8, 0.05f, false, Centre, Palettes.Air, PixelArt.Implode));
        // The floor drawn in: the game's thin air shockwave ring (white, tinted; played backwards).
        public static readonly ArtSlot Ring = new ArtSlot(
            () => GameArt.Grid(Owner, "vacuum ring", "AirWaveShockwave", 784, 98, 98, 98, new[] { 0 }, 0, 4,
                new Vector2(49f, 46f), 0.07f, false, fadeFrom: 0.5f, white: true),
            () => FxAnim.Build(Owner, "vacuum ring", 64, 32, 6, 0.05f, false, Centre, Palettes.Air, PixelArt.GroundRing));
    }

    internal static class FireArt
    {
        private const string Owner = "Blazing Kick";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Foot = new Vector2(0.5f, 0.05f);

        // Charging: the game's Blazing Combo flame tongues (32x48, their base 5 px up), looping.
        public static readonly ArtSlot Tongue = new ArtSlot(
            () => GameArt.Named(Owner, "flame tongue", "FireProjectiles", GameArt.Seq("BlazingComboProjectile", 0, 3),
                new Vector2(0.5f, 5f / 48f), 0.07f, true),
            () => FxAnim.Build(Owner, "flame tongue", 12, 22, 6, 0.06f, true, Foot, Palettes.Fire, PixelArt.FlameTongue));
        // The rush: the game's fire particle flames (white, tinted orange and yellow), 4 variants.
        public static readonly ArtSlot Wisp = new ArtSlot(
            () => GameArt.Grid(Owner, "flame wisps", "FireParticles", 96, 64, 16, 16, new[] { 0, 1, 2, 3 }, 0, 6,
                new Vector2(8f, 4f), 0.05f, false, fadeFrom: 0.4f, white: true),
            () => FxAnim.Build(Owner, "flame wisp", 12, 22, 6, 0.055f, false, Foot, Palettes.Fire, PixelArt.FlameWisp));
        // Embers: nothing in the game's sheets is this small, so they're drawn.
        public static readonly ArtSlot Ember = new ArtSlot(null,
            () => FxAnim.Build(Owner, "ember", 5, 5, 4, 0.07f, false, Centre, Palettes.Fire, PixelArt.Ember));
        // The launch: the game's swirling fireball puff.
        public static readonly ArtSlot Puff = new ArtSlot(
            () => GameArt.Named(Owner, "launch flare", "FireProjectiles", GameArt.Seq("VacuumFlame", 0, 2), Centre, 0.06f, false,
                fadeFrom: 0.5f),
            () => FxAnim.Build(Owner, "fire blast", 64, 48, 7, 0.05f, false, Centre, Palettes.Fire, PixelArt.FireBlast));
        // The blast: the game's explosion (fireball to smoke), its expanding ring, the burning
        // fissure left on the floor and the star flash of the kick.
        public static readonly ArtSlot Blast = new ArtSlot(
            () => GameArt.Named(Owner, "explosion", "NewExplosion", GameArt.Seq("Explosion", 0, 10), null, 0.05f, false,
                fadeFrom: 0.8f),
            () => FxAnim.Build(Owner, "fire blast", 64, 48, 7, 0.05f, false, Centre, Palettes.Fire, PixelArt.FireBlast));
        public static readonly ArtSlot Ring = new ArtSlot(
            () => GameArt.Named(Owner, "blast ring", "NewExplosion", GameArt.Seq("ExplosionRing", 0, 5), null, 0.045f, false,
                fadeFrom: 0.5f, white: true),
            () => FxAnim.Build(Owner, "scorch ring", 64, 32, 6, 0.05f, false, Centre, Palettes.Fire, PixelArt.GroundRing));
        public static readonly ArtSlot Fissure = new ArtSlot(
            () => GameArt.Named(Owner, "burning fissure", "FireProjectiles", GameArt.Seq("FireFissure", 0, 3), Centre, 0.1f, false,
                fadeFrom: 0.6f),
            null);
        public static readonly ArtSlot Flash = new ArtSlot(
            () => GameArt.Named(Owner, "kick flash", "FireExplosion", new[] { "FireExplosion1" }, null, 0.15f, false,
                fadeFrom: 0.3f, white: true),
            () => FxAnim.Build(Owner, "kick impact", 32, 32, 5, 0.04f, false, Centre, Palettes.Fire, PixelArt.ImpactStar));
    }

    internal static class EarthArt
    {
        private const string Owner = "Vine Slingshot";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);

        // The game's rock debris (16 small rocks); one is picked and held, spinning as it flies.
        public static readonly ArtSlot Rock = new ArtSlot(
            () => GameArt.Named(Owner, "rock debris", "Rocks", GameArt.Seq("Rocks_", 0, 15), Centre, 1f, false),
            () => FxAnim.Build(Owner, "tumbling rock", 9, 9, 4, 0.07f, true, Centre, Palettes.Earth, PixelArt.RockChunk));
        // The game's smoke puffs (white, tinted dust brown), 4 variants.
        public static readonly ArtSlot Dust = new ArtSlot(
            () => GameArt.Grid(Owner, "dust puffs", "Smoke", 64, 64, 16, 16, new[] { 0, 1, 2, 3 }, 0, 4,
                new Vector2(8f, 6f), 0.08f, false, fadeFrom: 0.3f, white: true),
            () => FxAnim.Build(Owner, "dust puff", 16, 12, 5, 0.06f, false, new Vector2(0.5f, 0.15f), Palettes.Earth, PixelArt.DustPuff));
        // The kick: the game's earth wave spikes bursting out of the floor and crumbling (8 frames,
        // 8 leanings), several along the kick; drawn, one burst of shards.
        public static readonly ArtSlot Spike = new ArtSlot(
            () => GameArt.Grid(Owner, "earth spikes", "EarthWave", 256, 256, 32, 32, new[] { 0, 1, 2, 3, 4, 5, 6, 7 }, 0, 8,
                new Vector2(17f, 11f), 0.045f, false, fadeFrom: 0.8f),
            () => FxAnim.Build(Owner, "rock burst", 64, 40, 7, 0.05f, false, new Vector2(12f / 64f, 0.5f), Palettes.Earth, PixelArt.EarthImpact));
        // The shockwave: the game's meteor-strike dust ring (grey, tinted).
        public static readonly ArtSlot Ring = new ArtSlot(
            () => GameArt.Named(Owner, "dust ring", "MeteorStrike", new[] { "MeteorRing0", "MeteorRing2", "MeteorRing3" }, null, 0.07f, false,
                fadeFrom: 0.35f, white: true),
            () => FxAnim.Build(Owner, "ground shockwave", 64, 32, 6, 0.05f, false, Centre, Palettes.Earth, PixelArt.GroundRing));
        public static readonly ArtSlot Flash = new ArtSlot(
            () => GameArt.Named(Owner, "kick flash", "FireExplosion", new[] { "FireExplosion1" }, null, 0.15f, false,
                fadeFrom: 0.3f, white: true),
            () => FxAnim.Build(Owner, "kick impact", 32, 32, 5, 0.04f, false, Centre, Palettes.Earth, PixelArt.ImpactStar));
    }

    internal static class StormArt
    {
        private const string Owner = "Charged Leap";
        private static readonly Vector2 Centre = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 Foot = new Vector2(0.5f, 0f);

        // The game's crackles (LightningEffect, 4 frames, 4 variants): charging and along the leap.
        public static readonly ArtSlot Crackle = new ArtSlot(
            () => GameArt.Grid(Owner, "crackles", "LightningEffect", 128, 128, 32, 32, new[] { 0, 1, 2, 3 }, 0, 4,
                new Vector2(16f, 16f), 0.05f, false, fadeFrom: 0.5f),
            () => FxAnim.Build(Owner, "spark", 9, 9, 4, 0.04f, false, Centre, Palettes.Storm, PixelArt.Zap));
        // Small zaps (MiniLightning).
        public static readonly ArtSlot Spark = new ArtSlot(
            () => GameArt.Named(Owner, "mini zaps", "Lightning", GameArt.Seq("MiniLightning", 0, 5), Centre, 0.035f, false,
                fadeFrom: 0.6f),
            () => FxAnim.Build(Owner, "spark", 9, 9, 4, 0.04f, false, Centre, Palettes.Storm, PixelArt.Zap));
        // A bolt from its foot (LightningBase, pointing up the sprite, 7 units long), turned along
        // the leap.
        public static readonly ArtSlot Bolt = new ArtSlot(
            () => GameArt.Named(Owner, "bolt", "Lightning", GameArt.Seq("LightningBase", 0, 4), Foot, 0.04f, false,
                fadeFrom: 0.6f, baseAngle: 90f),
            () => FxAnim.Build(Owner, "bolt", 64, 16, 5, 0.045f, false, new Vector2(0f, 0.5f), Palettes.Storm, PixelArt.Bolt));
        // The landing: the game's full lightning strike from the sky (Lightning_*), its ground
        // spark burst (LightningStart, 5 variants) and a shock ring (AirWaveShockwave, tinted).
        public static readonly ArtSlot Strike = new ArtSlot(
            () => GameArt.Named(Owner, "lightning strike", "Lightning", new[] { "Lightning_0", "Lightning_1", "Lightning_2", "Lightning_5", "Lightning_6", "Lightning_7" },
                Foot, 0.035f, false, fadeFrom: 0.5f),
            null);
        public static readonly ArtSlot Burst = new ArtSlot(
            () => GameArt.Grid(Owner, "strike sparks", "LightningStart", 240, 240, 48, 48, new[] { 0, 1, 2, 3, 4 }, 0, 5,
                new Vector2(24f, 14f), 0.05f, false, fadeFrom: 0.6f),
            () => FxAnim.Build(Owner, "thunder burst", 48, 48, 7, 0.045f, false, Centre, Palettes.Storm, PixelArt.ThunderBurst));
        public static readonly ArtSlot Ring = new ArtSlot(
            () => GameArt.Grid(Owner, "shock ring", "AirWaveShockwave", 784, 98, 98, 98, new[] { 0 }, 0, 4,
                new Vector2(49f, 46f), 0.06f, false, fadeFrom: 0.5f, white: true),
            () => FxAnim.Build(Owner, "shock ring", 64, 32, 6, 0.05f, false, Centre, Palettes.Storm, PixelArt.GroundRing));
    }
}
