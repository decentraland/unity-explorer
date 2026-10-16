using TMPro;
using UnityEngine;
using UnityEngine.TextCore.Text;
using Utility;

namespace ECS.StreamableLoading.Fonts
{
    public class SceneFontAssets
    {
        private readonly Material textMeshProMaterial;

        public TMP_FontAsset TextMeshProFont { get; }

        public FontAsset UIToolkitFont { get; }

        public SceneFontAssets(TMP_FontAsset textMeshProFont, FontAsset uiToolkitFont, Material textMeshProMaterial)
        {
            TextMeshProFont = textMeshProFont;
            UIToolkitFont = uiToolkitFont;
            this.textMeshProMaterial = textMeshProMaterial;
        }

        public void Destroy()
        {
            // The font assets belong to their bundle, so only the material is destroyed here
            TMP_ResourceManager.RemoveFontAsset(TextMeshProFont);
            UnityObjectUtils.SafeDestroy(textMeshProMaterial);
        }
    }
}
