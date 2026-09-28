using Arch.Core;
using Cinemachine;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterCamera.Settings;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using System;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Utility;
using Object = UnityEngine.Object;

namespace Global.MapCapture
{
    /// <summary>
    ///     The client's own camera rig (URP settings, post-processing and all) driven by one orthographic virtual
    ///     camera that looks straight down at a block of parcels and renders it into a square texture at an exact
    ///     pixels-per-parcel scale.
    /// </summary>
    public class MapCaptureCamera : IDisposable
    {
        private const int CAPTURE_PRIORITY = 1000;
        private const float NEAR_CLIP = 0.3f;
        private const float FAR_CLIP_BELOW_GROUND = 100f;
        private const float SHADOW_DISTANCE_MARGIN = 50f;
        private const int POSE_APPLY_DELAY_FRAMES = 2;
        private const int LIVE_TIMEOUT_FRAMES = 60;
        private const int JPEG_QUALITY = 90;
        private const string VIRTUAL_CAMERA_NAME = "MapCaptureVirtualCamera";

        private static readonly Quaternion TOP_DOWN = Quaternion.Euler(90f, 0f, 0f);

        private readonly ProvidedInstance<CinemachinePreset> rig;
        private readonly CinemachineVirtualCamera virtualCamera;
        private readonly MonoBehaviour coroutineRunner;

        public Camera Camera { get; }

        private MapCaptureCamera(ProvidedInstance<CinemachinePreset> rig, World world, MonoBehaviour coroutineRunner)
        {
            this.rig = rig;
            this.coroutineRunner = coroutineRunner;

            ICinemachinePreset preset = rig.Value;
            Camera = preset.Brain.OutputCamera;

            // Every pose change must land on the very next frame: no blend between the rig's cameras and this one.
            preset.Brain.m_DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Style.Cut, 0f);
            preset.Brain.m_CustomBlends = null;

            virtualCamera = new GameObject(VIRTUAL_CAMERA_NAME).AddComponent<CinemachineVirtualCamera>();
            virtualCamera.Priority = CAPTURE_PRIORITY;
            virtualCamera.m_Lens.ModeOverride = LensSettings.OverrideModes.Orthographic;
            virtualCamera.m_Lens.NearClipPlane = NEAR_CLIP;

            // The landscape systems find the camera through these two components on the camera entity.
            world.Create(new CameraComponent(Camera), preset);
        }

        public static async UniTask<MapCaptureCamera> CreateAsync(IPluginSettingsContainer settingsContainer, IAssetsProvisioner assetsProvisioner, World world,
            MonoBehaviour coroutineRunner, CancellationToken ct)
        {
            CharacterCameraSettings cameraSettings = settingsContainer.GetSettings<CharacterCameraSettings>();
            ProvidedInstance<CinemachinePreset> rig = await assetsProvisioner.ProvideInstanceAsync(cameraSettings.cinemachinePreset, Vector3.zero, Quaternion.identity, ct: ct);
            return new MapCaptureCamera(rig, world, coroutineRunner);
        }

        public void Dispose()
        {
            Object.Destroy(virtualCamera.gameObject);
            rig.Dispose();
        }

        /// <summary>
        ///     Renders the block at <paramref name="renderPixels" /> and writes it at <paramref name="outputPixels" />; the
        ///     downscale rides on the same blit that converts the HDR render to sRGB.
        /// </summary>
        public async UniTask<byte[]> RenderBlockAsync(Vector2Int minParcel, int blockSize, int renderPixels, int outputPixels, bool jpeg, float height, CancellationToken ct)
        {
            EnsureShadowDistance(height);

            float footprint = blockSize * ParcelMathHelper.PARCEL_SIZE;
            Vector3 corner = ParcelMathHelper.GetPositionByParcelPosition(minParcel);

            virtualCamera.transform.SetPositionAndRotation(new Vector3(corner.x + (footprint * 0.5f), height, corner.z + (footprint * 0.5f)), TOP_DOWN);
            virtualCamera.m_Lens.OrthographicSize = footprint * 0.5f;
            virtualCamera.m_Lens.FarClipPlane = height + FAR_CLIP_BELOW_GROUND;

            // The brain pushes the pose on its next manual update; the frame after that is the one captured.
            await UniTask.DelayFrame(POSE_APPLY_DELAY_FRAMES, cancellationToken: ct);

            for (var frame = 0; frame < LIVE_TIMEOUT_FRAMES && !rig.Value.Brain.IsLive(virtualCamera); frame++)
                await UniTask.NextFrame(ct);

            // HDR target for the same reason as the screenshot tool: an LDR target downgrades the whole URP render
            // and clamps emissives. The blit into the sRGB target below performs the linear-to-sRGB conversion.
            RenderTexture worldRender = RenderTexture.GetTemporary(renderPixels, renderPixels, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture? output = null;
            RenderTexture? previousTarget = Camera.targetTexture;

            try
            {
                // A square target makes the aspect 1, so the orthographic half-height spans exactly half the block.
                Camera.targetTexture = worldRender;
                await UniTask.WaitForEndOfFrame(coroutineRunner, ct);
                Camera.targetTexture = previousTarget;

                output = RenderTexture.GetTemporary(new RenderTextureDescriptor(outputPixels, outputPixels)
                {
                    graphicsFormat = OutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits = 0, mipCount = 1, useMipMap = false,
                });

                Graphics.Blit(worldRender, output);

                AsyncGPUReadbackRequest readback = await AsyncGPUReadback.Request(output).WithCancellation(ct);

                if (readback.hasError)
                    return EncodeViaReadPixels(output, outputPixels, jpeg);

                using NativeArray<byte> encoded = jpeg
                    ? ImageConversion.EncodeNativeArrayToJPG(readback.GetData<byte>(), output.graphicsFormat, (uint)outputPixels, (uint)outputPixels, quality: JPEG_QUALITY)
                    : ImageConversion.EncodeNativeArrayToPNG(readback.GetData<byte>(), output.graphicsFormat, (uint)outputPixels, (uint)outputPixels);

                return encoded.ToArray();
            }
            finally
            {
                Camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(worldRender);

                if (output != null)
                    RenderTexture.ReleaseTemporary(output);
            }
        }

        /// <summary>
        ///     URP measures the shadow distance from the camera, so a top-down camera far above the ground would
        ///     render it shadowless. Raising it is the one deliberate departure from the client's render settings.
        /// </summary>
        private static void EnsureShadowDistance(float height)
        {
            UniversalRenderPipelineAsset? pipeline = UniversalRenderPipeline.asset;

            if (pipeline != null && pipeline.shadowDistance < height + SHADOW_DISTANCE_MARGIN)
                pipeline.shadowDistance = height + SHADOW_DISTANCE_MARGIN;
        }

        private static byte[] EncodeViaReadPixels(RenderTexture source, int pixels, bool jpeg)
        {
            var buffer = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            RenderTexture? previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = source;
                buffer.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0);
                buffer.Apply();
                return jpeg ? buffer.EncodeToJPG(JPEG_QUALITY) : buffer.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previousActive;
                Object.Destroy(buffer);
            }
        }

        private static GraphicsFormat OutputGraphicsFormat()
        {
            var preferred = GraphicsFormat.R8G8B8A8_SRGB;

            return SystemInfo.IsFormatSupported(preferred, GraphicsFormatUsage.Render)
                ? preferred
                : SystemInfo.GetCompatibleFormat(preferred, GraphicsFormatUsage.Render);
        }
    }
}
