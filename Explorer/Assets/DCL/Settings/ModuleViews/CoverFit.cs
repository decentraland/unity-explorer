using UnityEngine;
using UnityEngine.UI;

namespace DCL.Settings.ModuleViews
{
    /// <summary>
    ///     Shows a texture cropped to fill its masked frame; no texture hides the image.
    /// </summary>
    internal static class CoverFit
    {
        public static void Apply(RawImage image, AspectRatioFitter fitter, Texture? texture)
        {
            image.texture = texture;
            image.enabled = texture != null;

            if (texture != null)
                fitter.aspectRatio = texture.width / (float)texture.height;
        }
    }
}
