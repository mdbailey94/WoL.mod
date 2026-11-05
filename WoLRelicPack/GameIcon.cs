using UnityEngine;

namespace WoLRelicPack
{
    // Relic shops show icons as sprites in the world, where their size comes from pixels per unit.
    // Unity's default (100) draws them far too small to see there, so match the size of the game's
    // own relic icons instead (16 px at 16 px per unit in ItemIcons.png; the inventory and HUD
    // aren't affected either way). Same helper as the arcana mods, reading relic icons instead.
    internal static class GameIcon
    {
        private const float FallbackPixelsPerUnit = 16f;
        private const float FallbackIconWidth = 16f;
        private static float worldWidth = -1f;

        public static Sprite Create(Texture2D texture, BepInEx.Logging.ManualLogSource log)
        {
            float ppu = texture.width / WorldWidth(log);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu);
        }

        // How wide a game relic icon is in the world.
        private static float WorldWidth(BepInEx.Logging.ManualLogSource log)
        {
            if (worldWidth > 0f)
                return worldWidth;
            try
            {
                foreach (var pair in IconManager.ItemIcons)
                {
                    Sprite game = pair.Value;
                    // Skip anything a mod made at Unity's default size.
                    if (game == null || game.rect.width < 4f || Mathf.Approximately(game.pixelsPerUnit, 100f))
                        continue;
                    worldWidth = game.rect.width / game.pixelsPerUnit;
                    log?.LogInfo($"Icons sized like the game's relic icons ({game.rect.width}px at {game.pixelsPerUnit} px per unit)");
                    return worldWidth;
                }
            }
            catch (System.Exception e)
            {
                log?.LogInfo($"Couldn't read the game's relic icons ({e.Message}), using a standard size");
            }
            worldWidth = FallbackIconWidth / FallbackPixelsPerUnit;
            return worldWidth;
        }
    }
}
