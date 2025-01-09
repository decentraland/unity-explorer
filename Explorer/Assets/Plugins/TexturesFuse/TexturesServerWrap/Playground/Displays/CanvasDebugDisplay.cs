using DCL.Utilities.Extensions;
using UnityEngine;
using UnityEngine.UI;

namespace Plugins.TexturesFuse.TexturesServerWrap.Playground.Displays
{
    public class CanvasDebugDisplay : AbstractDebugDisplay
    {
        [SerializeField] private RawImage image = null!;
        [SerializeField] private AspectRatioFitter aspectRatioFitter = null!;

        private void Start()
        {
            image.EnsureNotNull();
            aspectRatioFitter.EnsureNotNull();
        }

        public override void Display(Texture2D texture)
        {
            image.texture = texture;
            aspectRatioFitter.aspectRatio = (float)texture.width / texture.height;
        }
    }
}
