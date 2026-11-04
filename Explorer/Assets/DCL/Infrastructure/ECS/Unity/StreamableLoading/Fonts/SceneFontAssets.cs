using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class SceneFontAssets
    {
        /// <summary>
        ///     The atlas pages each font asset may reach: the bundled page plus the pages it adds at runtime for the glyphs the bundle
        ///     does not pre-fill. A 1024x1024 Alpha8 page is 1 MB.
        /// </summary>
        internal const int MAX_ATLAS_PAGES = 4;

        private readonly Material textMeshProMaterial;

        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public SceneFontAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, Material textMeshProMaterial)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
            this.textMeshProMaterial = textMeshProMaterial;
        }

        /// <summary>
        ///     Lets each font asset add atlas pages only while it holds fewer than <see cref="MAX_ATLAS_PAGES" />.
        ///     A font asset adds its pages while text is laid out, which this cannot interrupt, so it holds at most
        ///     <see cref="MAX_ATLAS_PAGES" /> - 1 pages plus the pages for the new glyphs laid out between two calls.
        /// </summary>
        public void LimitAtlasPages()
        {
            TextMeshProFont.isMultiAtlasTexturesEnabled = TextMeshProFont.atlasTextureCount < MAX_ATLAS_PAGES;
            UIToolkitFont.isMultiAtlasTexturesEnabled = UIToolkitFont.atlasTextureCount < MAX_ATLAS_PAGES;
        }

        public void Destroy()
        {
            // The font assets belong to their bundle, so they are kept, but the pages they added at runtime are not part of it.
            // Clearing a grown asset destroys those pages and leaves it with its first page only, emptied of the glyphs the bundle
            // pre-filled. The null checks skip the assets Unity has already destroyed on quit.
            if (TextMeshProFont != null && TextMeshProFont.atlasTextureCount > 1)
                TextMeshProFont.ClearFontAssetData();

            if (UIToolkitFont != null && UIToolkitFont.atlasTextureCount > 1)
                UIToolkitFont.ClearFontAssetData();

            TMP_ResourceManager.RemoveFontAsset(TextMeshProFont);
            UnityObjectUtils.SafeDestroy(textMeshProMaterial);
        }
    }
}
