using TMPro;
using UnityEngine;
using Font = DCL.ECSComponents.Font;

namespace DCL.SDKComponents.TextShape.Fonts
{
    public interface IFontsStorage
    {
        TMP_FontAsset? Font(Font font);

        class Fake : IFontsStorage
        {
            private readonly TMP_FontAsset? staticFont;

            public Fake(TMP_FontAsset? staticFont = null)
            {
                this.staticFont = staticFont;
            }

            public TMP_FontAsset? Font(Font font)
            {
                Debug.LogWarning("Fake font provided");
                return staticFont;
            }
        }
    }
}
