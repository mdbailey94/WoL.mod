using UnityEngine;

namespace WoLStoneCharge
{
    // The stone shield held up in front of the wizard: the game's own Aegis Charge shield
    // (ShieldCharge: a slab of rock a unit ahead that follows them, drawn from one of five angles
    // and flipped for left/right). Aegis Charge only ever points it one way; here it's turned to
    // face wherever the wizard is heading, using the same sprite choice its Start() makes. Its own
    // hit box stays off: the hits are the bursts StoneChargeState makes. Forming it fires the
    // game's shove burst at the shield's spot (ShieldCharge.Start, stat level 1), and dropping it
    // bursts it into rock debris (ShieldCharge.Reset).
    public class StoneShield
    {
        // How far ahead of the wizard the shield sits (what ShieldCharge does itself).
        private const float Reach = 1f;

        private ShieldCharge shield;
        private int sector = -1;
        private static bool loggedAim;
        // A shield of the signature's phalanx (0.5.1) stands this far to the side of the middle
        // one (along the line across the way you're facing; 0 = the middle one).
        private float side;

        public bool Alive => shield != null && shield.gameObject != null;

        // The game's shield sprite (for the drawn cracks laid over it), or null.
        public SpriteRenderer Renderer => Alive ? shield.spriteRenderer : null;

        public Vector2 Position(Player player) =>
            Alive ? (Vector2)shield.transform.position : (Vector2)player.attackOriginTrans.position;

        // Raises the shield facing direction. Null (logged) if the game's shield can't be made.
        public static StoneShield Raise(Player player, string skillID, Vector2 direction, float side = 0f)
        {
            try
            {
                var raised = new StoneShield { side = side };
                ShieldCharge sc = Globals.ChaosInst<ShieldCharge>(ShieldCharge.Prefab, player.transform.parent,
                    (Vector3)((Vector2)player.attackOriginTrans.position + direction * Reach + Across(direction) * side));
                if (sc == null || sc.attack == null)
                    throw new System.Exception("the prefab has no ShieldCharge or attack on it");
                sc.followTrans = player.attackOriginTrans;
                sc.atkVector = direction;
                // Its Start() shove burst (level 1) and its hit box are this arcana's.
                sc.SetAttackVars(player.skillCategory, skillID);
                sc.attack.knockbackOverwriteVector = direction;
                // The shield itself never hits; the bursts do.
                sc.SetAttackStatus(false);
                raised.shield = sc;
                raised.sector = Sector(direction);
                return raised;
            }
            catch (System.Exception e)
            {
                StoneChargeState.LogOnce("shield", $"couldn't raise the game's stone shield ({e.Message}); charging without the look");
                return null;
            }
        }

        // Turns the shield to face direction (8 ways; nothing happens until it changes sector).
        public void Aim(Vector2 direction)
        {
            if (!Alive || direction == Vector2.zero)
                return;
            int next = Sector(direction);
            // A phalanx shield keeps its place in the line every frame (the game sets the
            // follow offset itself when the shield starts).
            if (side != 0f)
            {
                Vector2 snapped = SectorVector(next);
                try { shield.followOffset = snapped * Reach + Across(snapped) * side; } catch { }
            }
            if (next == sector)
                return;
            sector = next;
            Vector2 dir = SectorVector(next);
            try
            {
                shield.atkVector = dir;
                shield.followOffset = dir * Reach + Across(dir) * side;
                shield.attack.knockbackOverwriteVector = dir;
                shield.attack.transform.localEulerAngles = Globals.GetRotationVector(-dir);
                shield.attack.transform.localPosition = -dir * 1.5f;
                SpriteRenderer sprite = shield.spriteRenderer;
                if (sprite != null && shield.spriteVars != null && shield.spriteVars.Length >= 5)
                {
                    // ShieldCharge.Start's choice: 0 = down, 1 = diagonal down, 2 = sideways,
                    // 3 = diagonal up, 4 = up; left-facing is the mirrored sprite.
                    int index = Mathf.Clamp((int)((195f - Vector2.Angle(Vector2.up, dir)) * 0.033f), 0, 6);
                    int[] variant = { 0, 1, 1, 2, 3, 3, 4 };
                    sprite.sprite = shield.spriteVars[variant[index]];
                    Vector3 scale = sprite.transform.localScale;
                    scale.x = dir.x < 0f ? -Mathf.Abs(scale.x) : Mathf.Abs(scale.x);
                    sprite.transform.localScale = scale;
                }
                if (!loggedAim)
                {
                    loggedAim = true;
                    StoneChargeState.Log("the shield turns with you");
                }
            }
            catch (System.Exception e)
            {
                StoneChargeState.LogOnce("aim", $"couldn't turn the shield ({e.Message}); it keeps facing the cast direction");
            }
        }

        // Bursts the shield into rock debris (the game's own break-up).
        public void Drop()
        {
            if (!Alive)
                return;
            try
            {
                shield.Reset();
            }
            catch
            {
                Object.Destroy(shield.gameObject);
            }
            shield = null;
        }

        // The line across a facing (to its left).
        private static Vector2 Across(Vector2 direction) => new Vector2(-direction.y, direction.x).normalized;

        private static int Sector(Vector2 direction)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            return ((Mathf.RoundToInt(angle / 45f) % 8) + 8) % 8;
        }

        private static Vector2 SectorVector(int sector)
        {
            float angle = sector * 45f * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
        }
    }
}
