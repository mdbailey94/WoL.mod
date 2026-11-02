using UnityEngine;

namespace WoLMomentumSpear
{
    // Cosmetic only: the game's native melee object owns collision timing and damage.
    public sealed class SpearLook : MonoBehaviour
    {
        private LineRenderer shaft, head;
        private float elapsed;
        private int layer, order = 10;

        internal static void Attach(MeleeAttackObject attack, int step, float momentum)
        {
            if (SpearPlugin.Material == null) return; // Preserve native visuals when unavailable.
            try
            {
                var go = new GameObject("MomentumSpear.Visual");
                go.transform.SetParent(attack.transform, false);
                var look = go.AddComponent<SpearLook>();
                // Drawn where the dagger's sprites were: on Unity's default layer it would sit
                // behind the game's art, with those sprites switched off below.
                foreach (SpriteRenderer sprite in attack.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    look.layer = sprite.sortingLayerID;
                    look.order = sprite.sortingOrder + 1;
                    break;
                }
                Color tip = step == 1 && momentum > 1f ? new Color(1f, 0.85f, 0.4f, 1f) : new Color(0.45f, 0.9f, 1f, 1f);
                look.shaft = look.Line("Shaft", 2, 0.06f, new Color(0.7f, 0.45f, 0.25f, 1f));
                look.head = look.Line("Spearhead", 5, 0.045f, tip);
                look.Draw(0f);
                foreach (SpriteRenderer sprite in attack.GetComponentsInChildren<SpriteRenderer>(true)) sprite.enabled = false;
            }
            catch (System.Exception) { /* The native hitbox and game animation remain authoritative. */ }
        }

        private LineRenderer Line(string name, int points, float thickness, Color color)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = SpearPlugin.Material; line.useWorldSpace = false;
            line.positionCount = points; line.startWidth = line.endWidth = thickness;
            line.startColor = line.endColor = color;
            line.sortingLayerID = layer; line.sortingOrder = order;
            return line;
        }

        private void Update() { elapsed += Time.deltaTime; Draw(elapsed); }

        private void Draw(float time)
        {
            float offset = Mathf.Sin(Mathf.Clamp01(time / 0.18f) * Mathf.PI) * 0.2f;
            if (shaft == null || head == null) return;
            shaft.SetPosition(0, new Vector3(0f, -0.1f + offset, -0.15f));
            shaft.SetPosition(1, new Vector3(0f, 1.2f + offset, -0.15f));
            head.SetPosition(0, new Vector3(0f, 1.05f + offset, -0.15f));
            head.SetPosition(1, new Vector3(-0.14f, 1.15f + offset, -0.15f));
            head.SetPosition(2, new Vector3(0f, 1.5f + offset, -0.15f));
            head.SetPosition(3, new Vector3(0.14f, 1.15f + offset, -0.15f));
            head.SetPosition(4, new Vector3(0f, 1.05f + offset, -0.15f));
        }
    }
}
