using UnityEngine;

namespace WoLBuckler
{
    // WoL Art's buckler (Log - Art 13:48, 'Giant weapons (2026-11-03)'), 16 px per unit, Point
    // filter, centre pivot: the buckler (14x14) and the punch (4 x 14x14: pull back, the smash with
    // sparks on its right edge, hold, recover; flipped for a punch to the left).
    internal static class BucklerArt
    {
        public static Sprite Shield;
        public static Sprite[] Punch = new Sprite[0];

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Texture2D texture = BucklerPlugin.LoadTexture("buckler.png");
            if (texture != null)
                Shield = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
            Texture2D strip = BucklerPlugin.LoadTexture("buckler_punch.png");
            if (strip != null && strip.height > 0 && strip.width >= strip.height)
            {
                Punch = new Sprite[strip.width / strip.height];
                for (int i = 0; i < Punch.Length; i++)
                    Punch[i] = Sprite.Create(strip, new Rect(i * strip.height, 0, strip.height, strip.height), new Vector2(0.5f, 0.5f), 16f);
            }
            log?.LogInfo($"Buckler art: buckler {(Shield != null ? "loaded" : "missing")}, punch {Punch.Length} frames");
        }

        // The punch frame for this far through the guard (0-1): a quick pull back, the smash while
        // the buckler flies out, the hold, then the recover.
        public static Sprite PunchAt(float t)
        {
            if (Punch.Length < 4)
                return Shield;
            return Punch[t < 0.1f ? 0 : t < 0.45f ? 1 : t < 0.75f ? 2 : 3];
        }
    }

    internal static class Buckler
    {
        // Where the wizard's hands are (the game's attack origin); the feet were too low (Matt,
        // 0.2.0).
        public static Vector2 Hand(Player owner) =>
            owner.attackOriginTrans != null ? (Vector2)owner.attackOriginTrans.position : (Vector2)owner.transform.position;

        // A punch: the game's earth burst where the buckler lands, and the buckler out in front
        // (breaking shots) for GuardTime.
        public static void Punch(Player owner, string skillID, Vector2 aim, bool finisher, bool enhanced)
        {
            Vector2 at = Hand(owner) + aim * BucklerPlugin.PunchReach;
            float radius = finisher ? BucklerPlugin.FinisherRadius : BucklerPlugin.PunchRadius;
            try
            {
                EarthBurst.CreateBurst(at, owner.skillCategory, skillID, finisher ? 2 : 1, radius);
            }
            catch (System.Exception e)
            {
                BucklerPlugin.Log($"Buckler: a burst failed: {e.Message}");
            }
            Sound(finisher ? "EarthExplosionShort" : "ImpactEarthAlt", at, finisher ? 1f : 1.2f);
            BucklerGuard.Raise(owner, skillID, aim, enhanced);
        }

        internal static void Sound(string name, Vector2 at, float pitch)
        {
            try
            {
                SoundManager.PlayAudioWithDistance(name, at, null, 24f, -1f, pitch);
            }
            catch
            {
            }
        }
    }

    // The buckler out in front of the wizard's hands for one punch: it shoots forward and back
    // again, turned to face the punch, and any enemy shot that reaches it from the front (0.2.0:
    // the shield is directional, Matt) breaks, with the checks of Stone Charge's shield
    // (WoLStoneCharge NegateShots): not one that ignores negation, is already returning, or is a
    // wizard's own; a boomerang is sent back instead. Enhanced: each break bursts into shrapnel.
    internal class BucklerGuard : MonoBehaviour
    {
        private const float ShotScanInterval = 0.1f, ShieldScale = 2f;
        private static Projectile[] shots = new Projectile[0];
        private static float nextShotScan;
        private Player owner;
        private string skillID;
        private Vector2 aim;
        private bool enhanced;
        private float born;
        private SpriteRenderer look;

        public static void Raise(Player owner, string skillID, Vector2 aim, bool enhanced)
        {
            var go = new GameObject("Buckler Guard");
            BucklerGuard guard = go.AddComponent<BucklerGuard>();
            guard.owner = owner;
            guard.skillID = skillID;
            guard.aim = aim;
            guard.enhanced = enhanced;
            guard.born = Time.time;
            // Swing-sized (0.3.0): 14 px drawn about 1.75 u across.
            go.transform.localScale = new Vector3(ShieldScale, ShieldScale, 1f);
            guard.look = go.AddComponent<SpriteRenderer>();
            guard.look.sprite = BucklerArt.PunchAt(0f);
            // The smash's sparks are drawn on the right edge: turned to the aim, and mirrored
            // top to bottom when punching left so its light stays on top.
            go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg);
            guard.look.flipY = aim.x < -0.01f;
            SpriteRenderer wizard = owner.GetComponentInChildren<SpriteRenderer>();
            if (wizard != null)
            {
                guard.look.sortingLayerID = wizard.sortingLayerID;
                guard.look.sortingOrder = wizard.sortingOrder + (aim.y > 0.3f ? -1 : 1);
            }
            guard.Place();
        }

        // Out to the punch's reach in the first 40% of the guard, then most of the way back.
        private Vector2 Place()
        {
            float t = Mathf.Clamp01((Time.time - born) / BucklerPlugin.GuardTime);
            float reach = BucklerPlugin.PunchReach;
            float d = t < 0.4f ? Mathf.Lerp(0.3f, reach, t / 0.4f) : Mathf.Lerp(reach, reach * 0.6f, (t - 0.4f) / 0.6f);
            Vector2 at = Buckler.Hand(owner) + aim * d;
            transform.position = at;
            return at;
        }

        private void Update()
        {
            if (owner == null || Time.time - born >= BucklerPlugin.GuardTime)
            {
                Destroy(gameObject);
                return;
            }
            Vector2 at = Place();
            look.sprite = BucklerArt.PunchAt((Time.time - born) / BucklerPlugin.GuardTime);
            BreakShots(at);
        }

        private void BreakShots(Vector2 at)
        {
            if (Time.time >= nextShotScan)
            {
                nextShotScan = Time.time + ShotScanInterval;
                shots = FindObjectsOfType<Projectile>();
            }
            foreach (Projectile p in shots)
            {
                if (p == null || !p.gameObject.activeInHierarchy || p.ignoreNegate || p.forcedReturn
                    || p.parentObject == owner.gameObject || p.attackBox == null || p.attackBox.atkInfo == null)
                    continue;
                string category = p.attackBox.atkInfo.skillCategory;
                if (category != null && category.Contains("Player"))
                    continue;
                Vector2 spot = p.transform.position;
                if (Vector2.Distance(spot, at) > BucklerPlugin.GuardRadius + p.thickness)
                    continue;
                // Only the shield's face blocks: a shot behind the buckler (on the wizard's side
                // of it) gets through.
                if (Vector2.Dot(spot - Buckler.Hand(owner), aim) < 0.1f)
                    continue;
                try
                {
                    PoolManager.GetPoolItem<HitSparkEmitter>().EmitSingle(HitSparkType.Large, spot, spot);
                }
                catch
                {
                }
                Buckler.Sound("ImpactNegate", spot, 1f);
                try
                {
                    if (p.boomerang)
                        p.ForceProjectileToReturn();
                    else
                        p.ResetProjectile();
                }
                catch (System.Exception e)
                {
                    BucklerPlugin.Log($"Buckler: couldn't break a shot ({e.Message})");
                    continue;
                }
                BucklerPlugin.Log("Buckler: broke an enemy shot" + (enhanced ? ", shrapnel" : ""));
                if (enhanced)
                {
                    try
                    {
                        EarthBurst.CreateBurst(spot, owner.skillCategory, skillID, 3, BucklerPlugin.ShrapnelRadius);
                    }
                    catch
                    {
                    }
                }
            }
        }
    }
}
