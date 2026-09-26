using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.TextShape.Fonts.Settings
{
    [CreateAssetMenu(fileName = "Font List", menuName = "DCL/SDKComponents/Font List")]
    public class SoFontList : ScriptableObject, IFontsStorage
    {
        [SerializeField]
        private List<FontPair> list = new ();

        public TMP_FontAsset? Font(Font font) =>
            list.FirstOrDefault(e => e.font == font)?.asset;
    }
}
