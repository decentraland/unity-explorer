using System.Collections.Generic;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Utility.UIToolkit
{
    /// <summary>
    ///     Builds the sprite character tables of a panel's emoji sprite assets on the main thread. The text jobs otherwise
    ///     build them lazily from several worker threads at once, which corrupts the dictionaries and makes every later
    ///     text pass of the panel throw (Unity 6000.5.9, SpriteAsset.UpdateLookupTables). A table already built is only
    ///     read by the jobs.
    /// </summary>
    public static class PanelSpriteLookupTables
    {
        // Each Addressables bundle ships its own copy of the shared text settings, so the guard is per sprite asset instance
        private static readonly HashSet<SpriteAsset> BUILT = new ();

        public static void Build(PanelSettings panelSettings)
        {
            PanelTextSettings textSettings = panelSettings.textSettings;

            // A panel without text settings of its own renders with Unity's default ones, which carry no sprite assets
            if (textSettings == null) return;

            Build(textSettings.defaultSpriteAsset);

            foreach (TextAsset fallback in textSettings.emojiFallbackTextAssets)
                if (fallback is SpriteAsset spriteAsset)
                    Build(spriteAsset);
        }

        // The sprite assets are unassigned in a fresh text settings asset; the visited set also stops a fallback cycle
        private static void Build(SpriteAsset? spriteAsset)
        {
            if (spriteAsset == null || !BUILT.Add(spriteAsset)) return;

            _ = spriteAsset.spriteCharacterLookupTable;

            foreach (SpriteAsset fallback in spriteAsset.fallbackSpriteAssets)
                Build(fallback);
        }
    }
}
