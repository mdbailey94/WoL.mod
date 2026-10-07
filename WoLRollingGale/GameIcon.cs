using UnityEngine;

namespace WoLRollingGale
{
    // The arcana shop shows icons as sprites in the world, where their size comes from pixels per
    // unit. Unity's default (100) drew ours far too small to see there, so match the size of the
    // game's own arcana icons instead (the spellbook and HUD aren't affected either way).
    internal static class GameIcon
    {
        private const float FallbackPixelsPerUnit = 16f;
        private const float FallbackIconWidth = 18f;
        private static float worldWidth = -1f;

        public static Sprite Create(Texture2D texture, BepInEx.Logging.ManualLogSource log)
        {
            float ppu = texture.width / WorldWidth(log);
            return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), ppu);
        }

        // How wide a game arcana icon is in the world.
        private static float WorldWidth(BepInEx.Logging.ManualLogSource log)
        {
            if (worldWidth > 0f)
                return worldWidth;
            try
            {
                foreach (var pair in IconManager.SkillIcons)
                {
                    Sprite game = pair.Value;
                    // Skip anything a mod made at Unity's default size.
                    if (game == null || game.rect.width < 4f || Mathf.Approximately(game.pixelsPerUnit, 100f))
                        continue;
                    worldWidth = game.rect.width / game.pixelsPerUnit;
                    log?.LogInfo($"Icon sized like the game's arcana icons ({game.rect.width}px at {game.pixelsPerUnit} px per unit)");
                    return worldWidth;
                }
            }
            catch (System.Exception e)
            {
                log?.LogInfo($"Couldn't read the game's arcana icons ({e.Message}), using a standard size");
            }
            worldWidth = FallbackIconWidth / FallbackPixelsPerUnit;
            return worldWidth;
        }
    }
}
