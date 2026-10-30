using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace UUAV.Tests
{
    /// <summary>
    /// Samples the player's output surface so tests can assert on what was
    /// actually presented, not just on state flags and clocks.
    /// </summary>
    public static class VideoProbe
    {
        /// <summary>
        /// Presentation assertions need a real GPU; Assert.Inconclusive under
        /// -nographics instead of failing on an environmental limit.
        /// </summary>
        public static void RequireGraphics()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Inconclusive("no graphics device (-nographics); presentation cannot be sampled");
            }
        }

        public static Color ReadCenterPixel(RenderTexture surface)
        {
            RenderTexture? previous = RenderTexture.active;
            var readback = new Texture2D(1, 1, TextureFormat.RGBA32, mipChain: false);
            try
            {
                RenderTexture.active = surface;
                readback.ReadPixels(new Rect(surface.width / 2f, surface.height / 2f, 1, 1), 0, 0);
                readback.Apply();
                return readback.GetPixel(0, 0);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.Destroy(readback);
            }
        }

        /// <summary>
        /// True when <paramref name="channel"/> (0=r, 1=g, 2=b) clearly
        /// dominates: tolerant of yuv420 rounding and gamma differences,
        /// strict enough to tell the fixture's solid bands apart.
        /// </summary>
        public static bool IsDominantChannel(Color color, int channel)
        {
            float r = channel == 0 ? color.r : channel == 1 ? color.g : color.b;
            float otherA = channel == 0 ? color.g : color.r;
            float otherB = channel == 2 ? color.g : color.b;
            return r > 0.35f && r > otherA + 0.2f && r > otherB + 0.2f;
        }
    }
}
