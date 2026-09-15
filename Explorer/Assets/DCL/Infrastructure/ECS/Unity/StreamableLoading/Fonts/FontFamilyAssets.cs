using System.Collections.Generic;
using TMPro;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class FontFamilyAssets
    {
        private readonly List<TMP_FontAsset> textMeshProAssets;
        private readonly List<FontAsset> uiToolkitAssets;

        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public FontFamilyAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, List<TMP_FontAsset> textMeshProAssets, List<FontAsset> uiToolkitAssets)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
            this.textMeshProAssets = textMeshProAssets;
            this.uiToolkitAssets = uiToolkitAssets;
        }

        public void Destroy() =>
            Destroy(textMeshProAssets, uiToolkitAssets);

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
