using DCL.PluginSystem;
using DCL.SDKComponents.TextShape.Fonts.Settings;
using System;
using TMPro;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.TextShape.Fonts
{
    [Serializable]
    public class FontsSettings : IDCLPluginSettings, IFontsStorage
    {
        [field: SerializeField]
        public SoFontList FontList { get; private set; }

        public TMP_FontAsset Font(Font font) =>
            FontList!.Font(font);
    }
}
