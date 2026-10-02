using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    /// <summary>
    ///     The font assets of a converted font bundle and the TMP material made for them. Only the material is
    ///     destroyed here; the font assets belong to their bundle.
    /// </summary>
    public class FontFamilyAssets
    {
        private readonly Material textMeshProMaterial;

        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public FontFamilyAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, Material textMeshProMaterial)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
            this.textMeshProMaterial = textMeshProMaterial;
        }

        public void Destroy()
        {
            TMP_ResourceManager.RemoveFontAsset(TextMeshProFont);
            UnityObjectUtils.SafeDestroy(textMeshProMaterial);
        }
    }
}
