using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utility;
using FontStyles = TMPro.FontStyles;

namespace ECS.StreamableLoading.Fonts
{
    public class SceneFontAssets
    {
        /// <summary>
        ///     The atlas pages each font asset may reach: the bundled page plus the pages it adds at runtime for the glyphs the bundle
        ///     does not pre-fill. A page is atlasWidth x atlasHeight Alpha8 bytes: 1 MB at 1024², up to 16 MB at the 4096² that bundles may use.
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
        ///     Adds the glyphs of <paramref name="text" /> that <see cref="TextMeshProFont" /> misses, one at a time, opening a new atlas page
        ///     only while it holds fewer than <see cref="MAX_ATLAS_PAGES" />. Multi-atlas stays off otherwise, so laying the text out
        ///     never opens a page.
        /// </summary>
        public void AddTextMeshProGlyphs(string text)
        {
            TMP_FontAsset font = TextMeshProFont;
            font.isMultiAtlasTexturesEnabled = true;

            for (var i = 0; i < text.Length && font.atlasTextureCount < MAX_ATLAS_PAGES; i++)
            {
                uint unicode = char.IsSurrogatePair(text, i) ? (uint)char.ConvertToUtf32(text, i++) : text[i];
                TMP_FontAssetUtilities.GetCharacterFromFontAsset(unicode, font, false, FontStyles.Normal, FontWeight.Regular, out _);
            }

            font.isMultiAtlasTexturesEnabled = false;
        }

        /// <summary>
        ///     Adds the glyphs of <paramref name="text" /> that <see cref="UIToolkitFont" /> misses, one at a time, opening a new atlas page
        ///     only while it holds fewer than <see cref="MAX_ATLAS_PAGES" />. Multi-atlas stays off otherwise, so laying the text out
        ///     never opens a page.
        /// </summary>
        public void AddUiToolkitGlyphs(string text)
        {
            FontAsset font = UIToolkitFont;
            font.isMultiAtlasTexturesEnabled = true;

            for (var i = 0; i < text.Length && font.atlasTextureCount < MAX_ATLAS_PAGES; i++)
            {
                uint unicode = char.IsSurrogatePair(text, i) ? (uint)char.ConvertToUtf32(text, i++) : text[i];
                font.HasCharacter(unicode, tryAddCharacter: true);
            }

            font.isMultiAtlasTexturesEnabled = false;
        }

        public void Destroy()
        {
            // Pages added at runtime are not part of the bundle; clearing destroys them. It also drops the bundled pre-fill,
            // which the next adoption of the still-cached bundle rebuilds at runtime.
            if (TextMeshProFont != null && TextMeshProFont.atlasTextureCount > 1)
                TextMeshProFont.ClearFontAssetData();

            if (UIToolkitFont != null && UIToolkitFont.atlasTextureCount > 1)
                UIToolkitFont.ClearFontAssetData();

            TMP_ResourceManager.RemoveFontAsset(TextMeshProFont);
            UnityObjectUtils.SafeDestroy(textMeshProMaterial);
        }
    }
}
