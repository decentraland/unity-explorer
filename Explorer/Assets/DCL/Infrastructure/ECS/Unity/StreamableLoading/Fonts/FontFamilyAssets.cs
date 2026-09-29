using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class FontFamilyAssets
    {
        private readonly List<TMP_FontAsset> textMeshProAssets;
        private readonly List<FontAsset> uiToolkitAssets;

        // Bundled assets belong to their asset bundle, which destroys them; only the material made for them is ours.
        private readonly Material? bundledMaterial;

        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public FontFamilyAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, List<TMP_FontAsset> textMeshProAssets, List<FontAsset> uiToolkitAssets)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
            this.textMeshProAssets = textMeshProAssets;
            this.uiToolkitAssets = uiToolkitAssets;
        }

        public static FontFamilyAssets FromBundle(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, Material textMeshProMaterial) =>
            new (textMeshProFont, uiToolkitFont, textMeshProMaterial);

        private FontFamilyAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, Material textMeshProMaterial)
            : this(textMeshProFont, uiToolkitFont, new List<TMP_FontAsset>(), new List<FontAsset>())
        {
            bundledMaterial = textMeshProMaterial;
        }

        public void Destroy()
        {
            if (bundledMaterial != null)
            {
                TMP_ResourceManager.RemoveFontAsset(TextMeshProFont);
                UnityObjectUtils.SafeDestroy(bundledMaterial);
            }

            Destroy(textMeshProAssets, uiToolkitAssets);
        }

        public static void Destroy(List<TMP_FontAsset> textMeshProAssets, List<FontAsset> uiToolkitAssets)
        {
            foreach (TMP_FontAsset asset in textMeshProAssets)
            {
                TMP_ResourceManager.RemoveFontAsset(asset);
                UnityObjectUtils.SafeDestroy(asset);
            }

            foreach (FontAsset asset in uiToolkitAssets)
                UnityObjectUtils.SafeDestroy(asset);

            textMeshProAssets.Clear();
            uiToolkitAssets.Clear();
        }
    }
}
