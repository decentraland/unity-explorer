using DCL.ECSComponents;
using System.Collections.Generic;
using TMPro;

namespace DCL.SDKComponents.TextShape.Fonts
{
    public class CachedFontsStorage : IFontsStorage
    {
        private readonly IFontsStorage origin;
        private readonly Dictionary<Font, TMP_FontAsset?> cache = new ();

        public CachedFontsStorage(IFontsStorage origin)
        {
            this.origin = origin;
        }

        public TMP_FontAsset? Font(Font font) =>
            cache.TryGetValue(font, out var result) == false
                ? cache[font] = origin.Font(font)
                : result;
    }
}
