using System;
using UnityEngine;
using Utility;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Composites a presentation slide, its playing video and the presenter camera circle into one reusable
    ///     upright <see cref="RenderTexture" /> with a single blit of the <c>DCL/PresentationCompositor</c> shader,
    ///     through its own instance of the given material. Not thread-safe — main-thread only.
    /// </summary>
    public sealed class PresentationCompositor : IDisposable
    {
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
        private static readonly int VIDEO_TEX_LINEAR_DATA = Shader.PropertyToID("_VideoTexLinearData");
        private static readonly int CAMERA_TEX_LINEAR_DATA = Shader.PropertyToID("_CameraTexLinearData");

        private readonly Material material;

        private RenderTexture? composite;
        private Texture? lastSlide;
        private bool lastShowVideoRect;
        private Vector4 lastVideoRect;
        private Vector4 lastCameraRect;
        private bool lastDrewFrame;

#if UNITY_INCLUDE_TESTS
        internal int blitCount { get; private set; }
#endif

        public PresentationCompositor(Material material)
        {
            this.material = new Material(material);
        }

        public void Dispose()
        {
            Release();
            UnityObjectUtils.SafeDestroy(material);
        }

        /// <summary>
        ///     Draws <paramref name="slide" />, then the video rect (black until <paramref name="video" /> is given) and the
        ///     camera circle (skipped when <paramref name="camera" /> is null) into a BGRA32 render texture of
        ///     <paramref name="width" /> × <paramref name="height" />, scaled down with its aspect preserved to at most
        ///     <see cref="LiveKitMediaExtensions.MAX_LIVEKIT_TEXTURE_SIZE" /> on each side. Rects are normalized with a
        ///     top-left origin. Blits on every call while <paramref name="video" /> or <paramref name="camera" /> is given;
        ///     otherwise only when the slide or rects changed, or the last call drew a video or camera frame.
        /// </summary>
        /// <returns>The same render texture instance while the size is unchanged.</returns>
        public Texture Compose(int width, int height, Texture slide, bool showVideoRect, Vector4 videoRect, Texture? video, Texture? camera, Vector4 cameraRect)
        {
            float scale = Mathf.Min(1f, (float)LiveKitMediaExtensions.MAX_LIVEKIT_TEXTURE_SIZE / Mathf.Max(width, height));
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));

            if (composite == null || composite.width != targetWidth || composite.height != targetHeight)
            {
                Release();
                composite = new RenderTexture(targetWidth, targetHeight, 0, RenderTextureFormat.BGRA32) { name = "PresentationComposite" };
                composite.Create();
            }

            bool drawsFrame = video != null || camera != null;

            if (!drawsFrame && !lastDrewFrame && composite.IsCreated()
                && ReferenceEquals(slide, lastSlide)
                && showVideoRect == lastShowVideoRect && videoRect.Equals(lastVideoRect) && cameraRect.Equals(lastCameraRect))
                return composite;

            lastDrewFrame = drawsFrame;
            lastSlide = slide;
            lastShowVideoRect = showVideoRect;
            lastVideoRect = videoRect;
            lastCameraRect = cameraRect;

            material.SetVector(VIDEO_RECT, videoRect);
            material.SetFloat(VIDEO_ENABLED, showVideoRect ? 1f : 0f);
            material.SetFloat(VIDEO_TEX_ENABLED, showVideoRect && video != null ? 1f : 0f);
            material.SetTexture(VIDEO_TEX, video != null ? video : Texture2D.blackTexture);
            material.SetFloat(VIDEO_TEX_LINEAR_DATA, video != null && !video.isDataSRGB ? 1f : 0f);

            material.SetVector(CAMERA_RECT, cameraRect);
            material.SetFloat(CAMERA_ENABLED, camera != null ? 1f : 0f);
            material.SetTexture(CAMERA_TEX, camera != null ? camera : Texture2D.blackTexture);
            material.SetFloat(CAMERA_TEX_LINEAR_DATA, camera != null && !camera.isDataSRGB ? 1f : 0f);
            material.SetFloat(CAMERA_EDGE, CAMERA_EDGE_PX / Mathf.Max(1f, cameraRect.z * targetWidth));

            material.SetVector(SLIDE_SIZE, new Vector4(targetWidth, targetHeight, 0f, 0f));

            RenderTexture previous = RenderTexture.active;
            Graphics.Blit(slide, composite, material);
            RenderTexture.active = previous;
#if UNITY_INCLUDE_TESTS
            blitCount++;
#endif
            return composite;
        }

        /// <summary>
        ///     Frees the composite render texture; the next <see cref="Compose" /> recreates it.
        /// </summary>
        public void Release()
        {
            lastSlide = null;

            if (composite == null) return;

            composite.Release();
            UnityObjectUtils.SafeDestroy(composite);
            composite = null;
        }
    }
}
