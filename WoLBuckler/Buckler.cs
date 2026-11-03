using UnityEngine;

namespace WoLBuckler
{
    // The buckler (14x14, 16 px per unit, Point filter; a stand-in drawn by WoL Player until WoL
    // Art's buckler and punch frames are in).
    internal static class BucklerArt
    {
        public static Sprite Shield;

        public static void Load(BepInEx.Logging.ManualLogSource log)
        {
            Texture2D texture = BucklerPlugin.LoadTexture("buckler.png");
            if (texture != null)
                Shield = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 16f);
            log?.LogInfo($"Buckler art: {(Shield != null ? "loaded" : "missing")}");
        }
    }

    internal static class Buckler
    {
        // A punch: the game's earth burst where the buckler lands, and the buckler out in front
        // (breaking shots) for GuardTime.
        public static void Punch(Player owner, string skillID, Vector2 aim, bool finisher, bool enhanced)
        {
            Vector2 at = (Vector2)owner.transform.position + aim * BucklerPlugin.PunchReach;
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

    // The buckler out in front of the wizard for one punch: it shoots forward and back again,
    // and any enemy shot that reaches it breaks, with the checks of Stone Charge's shield
    // (WoLStoneCharge NegateShots): not one that ignores negation, is already returning, or is a
    // wizard's own; a boomerang is sent back instead. Enhanced: each break bursts into shrapnel.
    internal class BucklerGuard : MonoBehaviour
    {
        private const float ShotScanInterval = 0.1f;
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
            go.transform.localScale = new Vector3(1.15f, 1.15f, 1f);
            guard.look = go.AddComponent<SpriteRenderer>();
            guard.look.sprite = BucklerArt.Shield;
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
            Vector2 at = (Vector2)owner.transform.position + aim * d;
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
