using System;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Projects an equirectangular texture into the six faces of a cube render texture with the DCL/EquirectToCube
    ///     shader, the same mapping the reflection cubemap uses. The shader draws a procedural fullscreen triangle, so the
    ///     faces are drawn through a command buffer, not a blit.
    /// </summary>
    public class EquirectCubemapConverter : IDisposable
    {
        private const string SHADER_NAME = "DCL/EquirectToCube";
        private const string CUBEMAP_NAME = "SceneCloudsCubemap";
        private const int MIN_FACE_SIZE = 256;
        private const int MAX_FACE_SIZE = 1024;

        private static readonly int MAIN_TEX = Shader.PropertyToID("_MainTex");
        private static readonly int CUBEMAP_FACE = Shader.PropertyToID("_CubemapFace");

        private static readonly CubemapFace[] FACES =
        {
            CubemapFace.PositiveX, CubemapFace.NegativeX,
            CubemapFace.PositiveY, CubemapFace.NegativeY,
            CubemapFace.PositiveZ, CubemapFace.NegativeZ,
        };

        private readonly Material material;
        private readonly MaterialPropertyBlock propertyBlock = new ();
        private readonly CommandBuffer commandBuffer = new () { name = nameof(EquirectCubemapConverter) };

        private RenderTexture? cubemap;

        private EquirectCubemapConverter(Shader shader)
        {
            material = new Material(shader);
        }

        /// <summary>
        ///     Null without a graphics device or when the shader is not available.
        /// </summary>
        public static EquirectCubemapConverter? TryCreate()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return null;

            Shader shader = Shader.Find(SHADER_NAME);

            return shader == null ? null : new EquirectCubemapConverter(shader);
        }

        public void Dispose()
        {
            ReleaseCubemap();
            commandBuffer.Dispose();
            UnityObjectUtils.SafeDestroy(material);
        }

        /// <summary>
        ///     Draws the source into the cube render texture and returns it. The face size is the power of two nearest to a
        ///     quarter of the source width, clamped to [256, 1024]; the cubemap is reallocated when it changes. No mips:
        ///     the cloud layer samples level 0.
        /// </summary>
        public RenderTexture Convert(Texture source)
        {
            int faceSize = Mathf.Clamp(Mathf.ClosestPowerOfTwo(source.width / 4), MIN_FACE_SIZE, MAX_FACE_SIZE);

            if (cubemap == null || cubemap.width != faceSize)
            {
                ReleaseCubemap();
                cubemap = CreateCubemap(faceSize);
            }

            material.SetTexture(MAIN_TEX, source);
            commandBuffer.Clear();

            for (var i = 0; i < FACES.Length; i++)
            {
                CubemapFace face = FACES[i];
                commandBuffer.SetRenderTarget(cubemap, 0, face);
                propertyBlock.SetFloat(CUBEMAP_FACE, (int)face);
                CoreUtils.DrawFullScreen(commandBuffer, material, propertyBlock);
            }

            Graphics.ExecuteCommandBuffer(commandBuffer);
            return cubemap;
        }

        public void ReleaseCubemap()
        {
            if (cubemap == null) return;

            cubemap.Release();
            UnityObjectUtils.SafeDestroy(cubemap);
            cubemap = null;
        }

        private static RenderTexture CreateCubemap(int faceSize)
        {
            var renderTexture = new RenderTexture(faceSize, faceSize, 0, RenderTextureFormat.ARGB32)
            {
                name = CUBEMAP_NAME,
                dimension = TextureDimension.Cube,
                useMipMap = false,
                autoGenerateMips = false,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };

            renderTexture.Create();
            return renderTexture;
        }
    }
}
