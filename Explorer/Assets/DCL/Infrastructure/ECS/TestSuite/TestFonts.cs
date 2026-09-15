using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;

namespace ECS.TestSuite
{
    public static class TestFonts
    {
        public static readonly string PATH = Path.Combine(Application.dataPath, "DCL/SDKComponents/Fonts/LiberationSans-Regular.ttf");

        private const int SAMPLING_POINT_SIZE = 90;
        private const int ATLAS_PADDING = 9;
        private const int ATLAS_SIZE = 1024;

        public static TMP_FontAsset CreateTextMeshProFont() =>
            TMP_FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE);

        public static FontAsset CreateUIToolkitFont() =>
            FontAsset.CreateFontAsset(PATH, 0, SAMPLING_POINT_SIZE, ATLAS_PADDING, GlyphRenderMode.SDFAA, ATLAS_SIZE, ATLAS_SIZE);
    }
}
