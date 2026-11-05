using System;
using System.Reflection;
using UnityEngine;

namespace WoLChaosDepths
{
    // #80 (Matt): in Chaos Depths, cloak upgrade tiers (the Cloak Upgrades mod) are bought with the
    // wizard's Chaos Dust instead of chaos gems: 50 / 120 / 250 / 500 for tiers 1-4. Cloak Upgrades
    // reads its price, balance and payment from WoLCloakUpgrades.CloakCurrency, set here by
    // reflection so neither mod needs the other. BepInEx may load Cloak Upgrades after this
    // plugin, so the hook is looked for again every second for a minute.
    public static class CloakDust
    {
        private static readonly int[] Costs = { 50, 120, 250, 500 };
        private static bool bound;

        public static void Install()
        {
            if (Bind())
                return;
            var host = new GameObject("ChaosDepths CloakDust");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.hideFlags = HideFlags.HideAndDontSave;
            host.AddComponent<Retry>();
        }

        private class Retry : MonoBehaviour
        {
            private float next;
            private int tries;

            private void Update()
            {
                if (Time.unscaledTime < next)
                    return;
                next = Time.unscaledTime + 1f;
                if (Bind() || ++tries >= 60)
                {
                    if (!bound)
                        ChaosDepthsPlugin.Log("Cloak dust: Cloak Upgrades isn't loaded; its upgrades stay on gems");
                    Destroy(gameObject);
                }
            }
        }

        private static bool Bind()
        {
            if (bound)
                return true;
            Type currency = null;
            foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                currency = asm.GetType("WoLCloakUpgrades.CloakCurrency", false);
                if (currency != null)
                    break;
            }
            if (currency == null)
                return false;
            try
            {
                Set(currency, "Cost", new Func<Player, int, int>((p, tier) => Costs[Mathf.Clamp(tier, 0, Costs.Length - 1)]));
                Set(currency, "Balance", new Func<Player, int>(p => SaveOf(p)?.dust ?? 0));
                Set(currency, "Spend", new Func<Player, int, bool>(Spend));
                Set(currency, "Unit", new Func<string>(() => "dust"));
                bound = true;
                ChaosDepthsPlugin.Log("Cloak dust: cloak upgrades are bought with Chaos Dust (50 / 120 / 250 / 500)");
            }
            catch (Exception e)
            {
                ChaosDepthsPlugin.LogError($"Cloak dust: couldn't set Cloak Upgrades' currency ({e.Message}); its upgrades stay on gems");
                bound = true;   // don't keep trying a hook that doesn't fit
            }
            return true;
        }

        private static void Set(Type type, string field, object value)
        {
            FieldInfo f = type.GetField(field, BindingFlags.Public | BindingFlags.Static);
            if (f == null)
                throw new MissingFieldException(type.Name, field);
            f.SetValue(null, value);
        }

        private static CharacterSave SaveOf(Player p) =>
            p != null && ChaosDepthsPlugin.Instance != null ? ChaosDepthsPlugin.Instance.SaveOf(p) : null;

        private static bool Spend(Player p, int cost)
        {
            CharacterSave save = SaveOf(p);
            if (save == null || cost < 0 || save.dust < cost)
                return false;
            save.dust -= cost;
            ChaosDepthsPlugin.Instance.SaveCharacter(p, save);
            ChaosDepthsPlugin.Log($"Cloak dust: player {p.playerID + 1} paid {cost} dust for a cloak upgrade ({save.dust} left)");
            return true;
        }
    }
}
