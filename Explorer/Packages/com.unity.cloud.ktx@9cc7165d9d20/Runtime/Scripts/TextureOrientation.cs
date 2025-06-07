// SPDX-FileCopyrightText: 2023 Unity Technologies and the KTX for Unity authors
// SPDX-License-Identifier: Apache-2.0

namespace KtxUnity
{

    /// <summary>
    /// See Section 5.2 in https://registry.khronos.org/KTX/specs/2.0/ktxspec.v2.html#_ktxorientation
    /// </summary>
    [System.Flags]
    public enum TextureOrientation
    {
        /// <summary>
        /// KTX defaults to X=right Y=down Z=out
        /// </summary>
        KtxDefault = 0x0,
        /// <summary>
        /// If present X=left, else X=right
        /// </summary>
        XLeft = 0x1,
        /// <summary>
        /// If present Y=up, else Y=down
        /// </summary>
        YUp = 0x2,
        /// <summary>
        /// If present Z=in, else Z=out
        /// </summary>
        ZIn = 0x4, // Not used at the moment
        /// <summary>
        /// Unity expects GPU textures to be X=right Y=up
        /// </summary>
        UnityDefault = YUp,
    }

    /// <summary>
    /// Extensions to check if a texture's orientation conforms to Unity's default.
    /// </summary>
    public static class TextureOrientationExtension
    {

        /// <summary>
        /// Evaluates if the texture's horizontal orientation conforms to Unity's default.
        /// If it's not aligned (=true; =flipped), the texture has to be applied mirrored horizontally.
        /// </summary>
        /// <param name="to"></param>
        /// <returns>True if the horizontal orientation is flipped, false otherwise</returns>
        public static bool IsXFlipped(this TextureOrientation to)
        {
            // Unity default == X_RIGHT
            return (to & TextureOrientation.XLeft) != 0;
        }

        /// <summary>
        /// Evaluates if the texture's vertical orientation conforms to Unity's default.
        /// If it's not aligned (=true; =flipped), the texture has to be applied mirrored vertically.
        /// </summary>
        /// <param name="to"></param>
        /// <returns>True if the vertical orientation is flipped, false otherwise</returns>
        public static bool IsYFlipped(this TextureOrientation to)
        {
            // Unity default == Y_UP
            return (to & TextureOrientation.YUp) == 0;
        }
    }
}
