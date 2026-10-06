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
        private const string VIRTUAL_CAMERA_NAME = "MapCaptureVirtualCamera";
        private const string NO_BLOOM_VOLUME_NAME = "MapCaptureNoBloom";
        private const float NO_BLOOM_VOLUME_PRIORITY = 1000f;

        private static readonly Quaternion TOP_DOWN = Quaternion.Euler(90f, 0f, 0f);

        private readonly ProvidedInstance<CinemachinePreset> rig;
        private readonly CinemachineVirtualCamera virtualCamera;
        private readonly MonoBehaviour coroutineRunner;

        public Camera Camera { get; }

        /// <summary>The entity carrying the camera component, for whatever reads the camera through the world.</summary>
        public Entity CameraEntity { get; }

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
            CameraEntity = world.Create(new CameraComponent(Camera), preset);
        }

        public static async UniTask<MapCaptureCamera> CreateAsync(IPluginSettingsContainer settingsContainer, IAssetsProvisioner assetsProvisioner, World world,
            MonoBehaviour coroutineRunner, bool keepBloom, CancellationToken ct)
        {
            CharacterCameraSettings cameraSettings = settingsContainer.GetSettings<CharacterCameraSettings>();
            ProvidedInstance<CinemachinePreset> rig = await assetsProvisioner.ProvideInstanceAsync(cameraSettings.cinemachinePreset, Vector3.zero, Quaternion.identity, ct: ct);
            var camera = new MapCaptureCamera(rig, world, coroutineRunner);

            if (!keepBloom)
                camera.DisableBloom();

            return camera;
        }

        /// <summary>
        ///     A global volume that overrides bloom to zero, on a layer the rig's camera evaluates. Emissives keep their
        ///     brightness; only the halo that would spread over neighbouring parcels goes.
        /// </summary>
        private void DisableBloom()
        {
            var volumeObject = new GameObject(NO_BLOOM_VOLUME_NAME);
            volumeObject.transform.SetParent(virtualCamera.transform);

            if (Camera.TryGetComponent(out UniversalAdditionalCameraData cameraData))
                volumeObject.layer = FirstLayer(cameraData.volumeLayerMask);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0f);

            Volume volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = NO_BLOOM_VOLUME_PRIORITY;
            volume.sharedProfile = profile;
        }

        private static int FirstLayer(LayerMask mask)
        {
            for (var layer = 0; layer < 32; layer++)
                if ((mask.value & (1 << layer)) != 0)
                    return layer;

            return 0;
        }

        public void Dispose()
        {
            Object.Destroy(virtualCamera.gameObject);
            rig.Dispose();
        }

        /// <summary>
        ///     An image under assembly: an HDR target the size of the full render that sub-blocks are copied into as
        ///     their scenes come and go, then written at the output size.
        /// </summary>
        public sealed class BlockImage : IDisposable
        {
            internal readonly RenderTexture Accumulator;
            internal readonly int RenderPixels;

            internal BlockImage(int renderPixels)
            {
                RenderPixels = renderPixels;
                Accumulator = RenderTexture.GetTemporary(renderPixels, renderPixels, 0, RenderTextureFormat.DefaultHDR);
            }

            public void Dispose() =>
                RenderTexture.ReleaseTemporary(Accumulator);
        }

        public BlockImage BeginBlock(int renderPixels) =>
            new (renderPixels);

        /// <summary>
        ///     Renders <paramref name="parcels" /> parcels from <paramref name="minParcel" /> and copies them into the
        ///     image at <paramref name="pixelOffset" /> (texture space, so a southern sub-block lands at the bottom).
        /// </summary>
        public async UniTask RenderIntoAsync(BlockImage image, Vector2Int minParcel, int parcels, Vector2Int pixelOffset, int pixels, float height, CancellationToken ct)
        {
            EnsureShadowDistance(height);

            float footprint = parcels * ParcelMathHelper.PARCEL_SIZE;
            Vector3 corner = ParcelMathHelper.GetPositionByParcelPosition(minParcel);

            virtualCamera.transform.SetPositionAndRotation(new Vector3(corner.x + (footprint * 0.5f), height, corner.z + (footprint * 0.5f)), TOP_DOWN);
            virtualCamera.m_Lens.OrthographicSize = footprint * 0.5f;
            virtualCamera.m_Lens.FarClipPlane = height + FAR_CLIP_BELOW_GROUND;

            // The brain pushes the pose on its next manual update; the frame after that is the one captured.
            await UniTask.DelayFrame(POSE_APPLY_DELAY_FRAMES, cancellationToken: ct);

            for (var frame = 0; frame < LIVE_TIMEOUT_FRAMES && !rig.Value.Brain.IsLive(virtualCamera); frame++)
                await UniTask.NextFrame(ct);

            // HDR target for the same reason as the screenshot tool: an LDR target downgrades the whole URP render
            // and clamps emissives. The final blit into the sRGB target performs the linear-to-sRGB conversion.
            RenderTexture worldRender = RenderTexture.GetTemporary(pixels, pixels, 24, RenderTextureFormat.DefaultHDR);
            RenderTexture? previousTarget = Camera.targetTexture;

            try
            {
                // A square target makes the aspect 1, so the orthographic half-height spans exactly half the footprint.
                Camera.targetTexture = worldRender;
                await UniTask.WaitForEndOfFrame(coroutineRunner, ct);
                Camera.targetTexture = previousTarget;

                Graphics.CopyTexture(worldRender, 0, 0, 0, 0, pixels, pixels, image.Accumulator, 0, 0, pixelOffset.x, pixelOffset.y);
            }
            finally
            {
                Camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(worldRender);
            }
        }

        /// <summary>Writes the assembled image at <paramref name="outputPixels" />; the downscale rides on the sRGB blit.</summary>
        public async UniTask<byte[]> EndBlockAsync(BlockImage image, int outputPixels, int? jpegQuality, CancellationToken ct)
        {
            RenderTexture output = RenderTexture.GetTemporary(new RenderTextureDescriptor(outputPixels, outputPixels)
            {
                graphicsFormat = OutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits = 0, mipCount = 1, useMipMap = false,
            });

            try
            {
                Graphics.Blit(image.Accumulator, output);

                AsyncGPUReadbackRequest readback = await AsyncGPUReadback.Request(output).WithCancellation(ct);

                if (readback.hasError)
                    return EncodeViaReadPixels(output, outputPixels, jpegQuality);

                using NativeArray<byte> encoded = jpegQuality.HasValue
                    ? ImageConversion.EncodeNativeArrayToJPG(readback.GetData<byte>(), output.graphicsFormat, (uint)outputPixels, (uint)outputPixels, quality: jpegQuality.Value)
                    : ImageConversion.EncodeNativeArrayToPNG(readback.GetData<byte>(), output.graphicsFormat, (uint)outputPixels, (uint)outputPixels);

                return encoded.ToArray();
            }
            finally
            {
                RenderTexture.ReleaseTemporary(output);
                image.Dispose();
            }
        }

        /// <summary>One load, one image: the whole block rendered and written in a single pass.</summary>
        public async UniTask<byte[]> RenderBlockAsync(Vector2Int minParcel, int blockSize, int renderPixels, int outputPixels, int? jpegQuality, float height, CancellationToken ct)
        {
            BlockImage image = BeginBlock(renderPixels);
            await RenderIntoAsync(image, minParcel, blockSize, Vector2Int.zero, renderPixels, height, ct);
            return await EndBlockAsync(image, outputPixels, jpegQuality, ct);
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

        private static byte[] EncodeViaReadPixels(RenderTexture source, int pixels, int? jpegQuality)
        {
            var buffer = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
            RenderTexture? previousActive = RenderTexture.active;

            try
            {
                RenderTexture.active = source;
                buffer.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0);
                buffer.Apply();
                return jpegQuality.HasValue ? buffer.EncodeToJPG(jpegQuality.Value) : buffer.EncodeToPNG();
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
