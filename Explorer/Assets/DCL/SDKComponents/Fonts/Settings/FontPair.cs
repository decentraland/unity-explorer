using DCL.ECSComponents;
using System;
using TMPro;

namespace DCL.SDKComponents.Fonts.Settings
{
    [Serializable]
    public class FontPair
    {
        public Font font;
        public TMP_FontAsset asset = null!;
    }
}
