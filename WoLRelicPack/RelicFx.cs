using UnityEngine;

namespace WoLRelicPack
{
    // Shared helpers: telling arcana hits apart, finding enemies, and the look of the relics'
    // extra hits (all visual only: the damage itself is dealt straight to the target, the way the
    // game's Double Attack relic does it, so nothing here can hit anything).
    internal static class RelicFx
    {
        private static bool loggedBolt;

        // A hit by this player's arcana: not a basic or dash arcana, not a status-effect tick, and
        // not a relic's own attack (relic attacks use the relic's ID). Uses the skill's own flags
        // when the hit's ID is a skill, otherwise the game's own naming rule ("Basic"/"Dash" in
        // the ID, as Item.IgnoreBasicDashAndStatusCond does).
        public static bool IsArcanaHit(Player player, AttackInfo info)
        {
            if (player == null || info == null || info.isStatusEffect || string.IsNullOrEmpty(info.skillID))
                return false;
            if (IsOurs(info))
                return false;
            Player.SkillState skill;
            if (player.skillsDict != null && player.skillsDict.TryGetValue(info.skillID, out skill) && skill != null)
                return !skill.isBasic && !skill.isDash;
            if (LootManager.completeItemDict.ContainsKey(info.skillID))
                return false;
            return !info.skillID.Contains("Basic") && !info.skillID.Contains("Dash");
        }

        // One of this mod's own extra hits (so the relics never trigger off each other).
        public static bool IsOurs(AttackInfo info) =>
            info != null && ((info.attacker != null && info.attacker.StartsWith(RelicPackPlugin.IDPrefix))
                || (info.skillID != null && info.skillID.StartsWith(RelicPackPlugin.IDPrefix)));

        public static bool IsAlive(Entity entity) =>
            entity != null && entity.gameObject != null && entity.health != null && entity.health.CurrentHealthValue > 0;

        // Where to aim at an entity (its hurtbox if it has one).
        public static Vector2 Center(Entity entity) =>
            entity.hurtBoxTransform != null ? (Vector2)entity.hurtBoxTransform.position : (Vector2)entity.transform.position;

        // ---- Look ----

        // The game's lightning bolt from the sky, stripped to its animation (as Thunderhead does).
        public static void Bolt(Vector2 position, float size)
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
            }
            catch (System.Exception e)
            {
                if (!loggedBolt)
                {
                    loggedBolt = true;
                    RelicPackPlugin.Log($"The game's lightning bolt is unavailable, sparks only: {e.Message}");
                }
            }
        }

        // A line of lightning sparks from a to b, like the game's lightning chain leaves behind.
        public static void Arc(Vector2 from, Vector2 to)
        {
            try
            {
                ParticleEffect sparks = PoolManager.GetPoolItem<ParticleEffect>("LightningHitEffect");
                for (int i = 1; i < 8; i++)
                    sparks.Emit(3, Vector2.Lerp(from, to, i * 0.125f));
                SoundManager.PlayWithDistAndSPR("Shockwave", to);
            }
            catch
            {
            }
        }

        // The game's elemental burst for an element, as a harmless flash: its attack is switched
        // off before it can touch anything.
        public static void ElementFlash(ElementType element, ElementType subElement, Vector2 position)
        {
            try
            {
                ElementalBurst burst;
                if (subElement == ElementType.Ice)
                    element = ElementType.Ice;
                switch (element)
                {
                    case ElementType.Fire:
                        burst = FlameBurst.CreateBurst(position, (AttackInfo)null, 1f);
                        break;
                    case ElementType.Water:
                    case ElementType.Ice:
                        burst = IceBurst.CreateBurst(position, (AttackInfo)null, 1.2f);
                        break;
                    case ElementType.Air:
                        burst = WindBurst.CreateBurst(position, (AttackInfo)null, 1.5f);
                        break;
                    case ElementType.Lightning:
                        burst = LightningBurst.CreateBurst(position, (AttackInfo)null, 1f);
                        break;
                    case ElementType.Earth:
                    case ElementType.Plant:
                    case ElementType.Poison:
                        burst = EarthBurst.CreateBurst(position, (AttackInfo)null, 1.2f);
                        break;
                    case ElementType.Chaos:
                        burst = ChaoticBurst.CreateBurst((Vector3)position, (AttackInfo)null, 1.2f);
                        break;
                    default:
                        burst = NeutralBurst.CreateBurst(position, (AttackInfo)null, 1.2f);
                        break;
                }
                if (burst != null && burst.attack != null)
                    burst.attack.gameObject.SetActive(false);
            }
            catch
            {
            }
        }
    }
}
