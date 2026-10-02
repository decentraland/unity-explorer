using System;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Composites a presentation slide, its playing video and the presenter camera circle into one reusable
    ///     upright <see cref="RenderTexture" /> with a single blit of the <c>DCL/PresentationCompositor</c> shader.
    ///     Not thread-safe — main-thread only.
    /// </summary>
    public sealed class PresentationCompositor : IDisposable
    {
        public const int MAX_COMPOSITE_SIZE = 2048;

        private const float CAMERA_EDGE_PX = 1.5f;

        private static readonly int VIDEO_TEX = Shader.PropertyToID("_VideoTex");
        private static readonly int CAMERA_TEX = Shader.PropertyToID("_CameraTex");
        private static readonly int VIDEO_RECT = Shader.PropertyToID("_VideoRect");
        private static readonly int CAMERA_RECT = Shader.PropertyToID("_CameraRect");
        private static readonly int VIDEO_ENABLED = Shader.PropertyToID("_VideoEnabled");
        private static readonly int VIDEO_TEX_ENABLED = Shader.PropertyToID("_VideoTexEnabled");
        private static readonly int CAMERA_ENABLED = Shader.PropertyToID("_CameraEnabled");
        private static readonly int CAMERA_EDGE = Shader.PropertyToID("_CameraEdge");
        private static readonly int SLIDE_SIZE = Shader.PropertyToID("_SlideSize");

        private readonly Material material;

        private RenderTexture? composite;

        internal int blitCount => throw new NotImplementedException();

        public PresentationCompositor(Material material)
        {
            this.material = material;
        }

        public void Dispose()
        {
            ReleaseComposite();
        }

        /// <summary>
        ///     Draws <paramref name="slide" />, then the video rect (black until <paramref name="video" /> is given) and the
        ///     camera circle (skipped when <paramref name="camera" /> is null) into a <paramref name="width" /> ×
        ///     <paramref name="height" /> BGRA32 render texture. Rects are normalized with a top-left origin.
        /// </summary>
        /// <returns>The same render texture instance while the size is unchanged.</returns>
        public Texture Compose(int width, int height, Texture slide, bool showVideoRect, Vector4 videoRect, Texture? video, Texture? camera, Vector4 cameraRect)
        {
            if (composite == null || composite.width != width || composite.height != height)
            {
                ReleaseComposite();
                composite = new RenderTexture(width, height, 0, RenderTextureFormat.BGRA32) { name = "PresentationComposite" };
                composite.Create();
            }

            material.SetVector(VIDEO_RECT, videoRect);
            material.SetFloat(VIDEO_ENABLED, showVideoRect ? 1f : 0f);
            material.SetFloat(VIDEO_TEX_ENABLED, showVideoRect && video != null ? 1f : 0f);
            material.SetTexture(VIDEO_TEX, video != null ? video : Texture2D.blackTexture);

            material.SetVector(CAMERA_RECT, cameraRect);
            material.SetFloat(CAMERA_ENABLED, camera != null ? 1f : 0f);
            material.SetTexture(CAMERA_TEX, camera != null ? camera : Texture2D.blackTexture);
            material.SetFloat(CAMERA_EDGE, CAMERA_EDGE_PX / Mathf.Max(1f, cameraRect.z * width));

            material.SetVector(SLIDE_SIZE, new Vector4(width, height, 0f, 0f));

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(slide, composite, material);
            RenderTexture.active = previous;
            return composite;
        }

        /// <summary>
        ///     Frees the composite render texture; the next <see cref="Compose" /> recreates it.
        /// </summary>
        public void Release()
        {
            throw new NotImplementedException();
        }

        private void ReleaseComposite()
        {
            if (composite == null) return;

            composite.Release();
            UnityObjectUtils.SafeDestroy(composite);
            composite = null;
        }
    }
}
