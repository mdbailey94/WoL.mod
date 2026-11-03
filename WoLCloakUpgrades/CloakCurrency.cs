using System;

namespace WoLCloakUpgrades
{
    // What upgrades are paid in. Chaos gems (Player.platWallet) by default; Chaos Depths sets these
    // (by reflection, so neither mod needs the other) to pay in the wizard's Chaos Dust instead
    // (#80, Matt: 50 / 120 / 250 / 500 for tiers 1-4). Each takes the wizard at the wardrobe.
    public static class CloakCurrency
    {
        // (wizard, current tier) -> price of the next tier. Null: gems at CloakTiers.Cost.
        public static Func<Player, int, int> Cost;
        public static Func<Player, int> Balance;
        public static Func<Player, int, bool> Spend;
        // The currency's name in the wardrobe's text ("gems", "dust").
        public static Func<string> Unit;

        private static bool Custom => Cost != null && Balance != null && Spend != null;

        public static int Price(Player p, int tier) => Custom ? Math.Max(0, Cost(p, tier)) : CloakTiers.Cost(tier);

        public static int Have(Player p) => Custom ? Balance(p) : Player.platWallet != null ? Player.platWallet.balance : 0;

        public static bool Pay(Player p, int cost) => Custom ? Spend(p, cost) : Player.platWallet != null && Player.platWallet.Withdraw(cost);

        public static string Name => Custom && Unit != null ? Unit() : "gems";
    }
}
