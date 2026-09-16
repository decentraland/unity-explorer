using TMPro;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class FontFamilyAssets
    {
        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public FontFamilyAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
        }

        public void Destroy() =>
            Destroy(TextMeshProFont, UIToolkitFont);

        internal static void Destroy(TMP_FontAsset? textMeshProFont, FontAsset? uiToolkitFont)
        {
            if (textMeshProFont != null)
                TMP_ResourceManager.RemoveFontAsset(textMeshProFont);

            UnityObjectUtils.SafeDestroy(textMeshProFont);
            UnityObjectUtils.SafeDestroy(uiToolkitFont);
        }
    }
}
