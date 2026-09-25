using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.McpServer.Core;
using DCL.McpServer.Utils;
using DCL.SkyBox;
using ECS;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Newtonsoft.Json.Linq;
using SceneRunner.Scene;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using Utility;
using Utility.Arch;
using Utility.Multithreading;
using Object = UnityEngine.Object;

namespace DCL.McpServer.Tools
{
    /// <summary>
    ///     One-call map tile capture. Parks the player inside the requested parcel block so its scenes stream in,
    ///     waits for them to finish loading, optionally fixes the skybox time, then renders the block straight down
    ///     through an orthographic free camera at an exact pixels-per-parcel scale, independent of the window size
    ///     and field of view. Replaces the teleport / set_camera_pose / set_skybox_time / screenshot round-trips
    ///     and the loading screen, Cinemachine blend and settle polls that come with them.
    /// </summary>
    public class RenderTileTool : McpTool
    {
        private const int MIN_SIZE = 1;
        private const int MAX_SIZE = 8;
        private const int DEFAULT_PIXELS_PER_PARCEL = 256;
        private const int MIN_PIXELS_PER_PARCEL = 16;
        private const int MAX_PIXELS = 8192;

        private const float DEFAULT_CAMERA_HEIGHT = 150f;
        private const float MIN_CAMERA_HEIGHT = 20f;
        private const float MAX_CAMERA_HEIGHT = 1000f;

        private const float DEFAULT_TIMEOUT_SEC = 60f;
        private const float MIN_TIMEOUT_SEC = 5f;
        private const float MAX_TIMEOUT_SEC = 300f;
        private const float DEFAULT_SETTLE_SEC = 1f;
        private const float MAX_SETTLE_SEC = 30f;
        private const float SKYBOX_TIMEOUT_SEC = 10f;

        private const float CAMERA_SETTLE_TIMEOUT_SEC = 5f;
        private const float CAMERA_SETTLE_EPSILON = 0.1f;
        private const int CAMERA_APPLY_DELAY_FRAMES = 2;
        private const int POLL_INTERVAL_MS = 250;

        // Dropped slightly above the parcel plane so the character lands on the scene floor rather than inside it.
        private const float PLAYER_DROP_HEIGHT = 2f;

        private const string MIME_TYPE_PNG = "image/png";

        private readonly ICoroutineRunner coroutineRunner;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly IScenesCache scenesCache;
        private readonly IRealmData realmData;
        private readonly ExposedCameraData exposedCameraData;
        private readonly SkyboxSettingsAsset skyboxSettings;

        // Guarded like ScreenshotTool: tool execution is marshalled onto the main thread, so the check-and-set
        // runs without preemption before the first await.
        private bool capturing;

        public override string Name => "render_tile";

        public override string Description =>
            "Render a top-down orthographic map tile of a square block of parcels at an exact pixels-per-parcel scale. "
            + "Moves the player into the block without a loading screen, waits for the block's scenes to finish loading, "
            + "optionally fixes the skybox time, hides the avatar and returns a PNG whose pixel grid aligns with the parcel grid. "
            + "Leaves the camera in free mode over the block; restore with set_camera_mode third_person.";

        protected override McpJsonSchema DescribeInput(McpJsonSchema schema) =>
            schema.Integer("x", "Parcel X of the block's minimum (south-west) corner.", isRequired: true)
                  .Integer("y", "Parcel Y of the block's minimum (south-west) corner.", isRequired: true)
                  .Integer("size", "Block side in parcels, 1-8. Default 1.")
                  .Integer("pixelsPerParcel", "Output pixels per parcel side. Default 256; size*pixelsPerParcel is capped at 8192.")
                  .Number("hour", "Optional skybox hour, 0-23 (requires minute). Leaves the skybox untouched when omitted.")
                  .Number("minute", "Optional skybox minute, 0-59 (requires hour).")
                  .Number("cameraHeight", "Camera height in meters above the parcel plane; content above it is clipped. Default 150.")
                  .Number("settleSec", "Extra seconds to wait after the scenes report loaded, for streaming textures. Default 1.")
                  .Number("timeoutSec", "Seconds to wait for the block's scenes to load before capturing anyway. Default 60, max 300.");

        public override McpToolAnnotations Annotations => McpToolAnnotations.Mutating(destructive: false, idempotent: true);

        public RenderTileTool(ICoroutineRunner coroutineRunner, World world, Entity playerEntity, IScenesCache scenesCache,
            IRealmData realmData, ExposedCameraData exposedCameraData, SkyboxSettingsAsset skyboxSettings)
        {
            this.coroutineRunner = coroutineRunner;
            this.world = world;
            this.playerEntity = playerEntity;
            this.scenesCache = scenesCache;
            this.realmData = realmData;
            this.exposedCameraData = exposedCameraData;
            this.skyboxSettings = skyboxSettings;
        }

        public override async UniTask<McpToolResult> ExecuteAsync(JObject arguments, CancellationToken ct)
        {
            if (!arguments.TryGetInt("x", out int x) || !arguments.TryGetInt("y", out int y))
                return McpToolResult.Error("x and y parcel coordinates of the block's minimum corner are required.");

            int size = Mathf.Clamp(arguments.GetInt("size", 1), MIN_SIZE, MAX_SIZE);
            int pixelsPerParcel = Mathf.Max(arguments.GetInt("pixelsPerParcel", DEFAULT_PIXELS_PER_PARCEL), MIN_PIXELS_PER_PARCEL);
            int pixels = size * pixelsPerParcel;

            if (pixels > MAX_PIXELS)
                return McpToolResult.Error($"size * pixelsPerParcel = {pixels} exceeds the {MAX_PIXELS} px cap.");

            bool hasHour = arguments.TryGetFloat("hour", out float hour);
            bool hasMinute = arguments.TryGetFloat("minute", out float minute);

            if (hasHour != hasMinute)
                return McpToolResult.Error("hour and minute must be provided together.");

            float cameraHeight = Mathf.Clamp(arguments.GetFloat("cameraHeight", DEFAULT_CAMERA_HEIGHT), MIN_CAMERA_HEIGHT, MAX_CAMERA_HEIGHT);
            float settleSec = Mathf.Clamp(arguments.GetFloat("settleSec", DEFAULT_SETTLE_SEC), 0f, MAX_SETTLE_SEC);
            float timeoutSec = Mathf.Clamp(arguments.GetFloat("timeoutSec", DEFAULT_TIMEOUT_SEC), MIN_TIMEOUT_SEC, MAX_TIMEOUT_SEC);

            if (capturing)
                return McpToolResult.Error("Another tile capture is already in progress; retry when it completes.");

            capturing = true;

            try { return await CaptureAsync(new Vector2Int(x, y), size, pixels, hasHour ? SkyboxTimeControl.Normalize(hour, minute) : (float?)null, cameraHeight, settleSec, timeoutSec, ct); }
            finally { capturing = false; }
        }

        private async UniTask<McpToolResult> CaptureAsync(Vector2Int minParcel, int size, int pixels, float? skyboxTime, float cameraHeight, float settleSec, float timeoutSec, CancellationToken ct)
        {
            float startTime = UnityEngine.Time.realtimeSinceStartup;

            SetAvatarHiddenTool.SetHidden(world, playerEntity, true);
            bool playerMoved = ParkPlayerInBlock(minParcel, size);

            List<Vector2Int> pendingParcels = await WaitForBlockLoadedAsync(minParcel, size, startTime + timeoutSec, ct);

            if (settleSec > 0f)
                await UniTask.Delay((int)(settleSec * 1000f), cancellationToken: ct);

            bool? skyboxSettled = null;

            if (skyboxTime.HasValue)
                skyboxSettled = await SkyboxTimeControl.SetAndWaitAsync(skyboxSettings, skyboxTime.Value, SKYBOX_TIMEOUT_SEC, ct);

            SingleInstanceEntity cameraEntity = world.CacheCamera();

            if (cameraEntity.GetCameraComponent(world).Mode != CameraMode.Free)
            {
                string? blockReason = SetCameraModeTool.TrySwitchMode(world, CameraMode.Free, out CameraMode _);

                if (blockReason != null)
                    return McpToolResult.Error(blockReason);

                // Let ControlCinemachineVirtualCameraSystem activate the free vcam (and apply its default
                // spawn position, which the pose below overrides).
                await UniTask.DelayFrame(CAMERA_APPLY_DELAY_FRAMES, cancellationToken: ct);
            }

            if (!world.TryGet(cameraEntity, out ICinemachinePreset? cinemachinePreset) || cinemachinePreset == null)
                return McpToolResult.Error("The camera rig is not initialized yet.");

            float footprint = size * ParcelMathHelper.PARCEL_SIZE;
            Vector3 corner = ParcelMathHelper.GetPositionByParcelPosition(minParcel);
            var cameraPosition = new Vector3(corner.x + (footprint / 2f), cameraHeight, corner.z + (footprint / 2f));

            // A square target makes the camera aspect 1, so the orthographic half-height spans exactly half the block.
            FreeCameraProjectionState previousProjection = cinemachinePreset.ForceFreeCameraTopDownOrthographic(cameraPosition, footprint / 2f);

            try
            {
                bool cameraSettled = await WaitForCameraAsync(cameraPosition, ct);
                await UniTask.DelayFrame(CAMERA_APPLY_DELAY_FRAMES, cancellationToken: ct);

                byte[] png = await RenderAsync(cameraEntity.GetCameraComponent(world).Camera, pixels, ct);

                var structured = new JObject
                {
                    ["parcel"] = minParcel.ToParcel(),
                    ["size"] = size,
                    ["pixels"] = pixels,
                    ["loaded"] = pendingParcels.Count == 0,
                    ["pendingParcels"] = new JArray(pendingParcels.ConvertAll(p => p.ToParcel())),
                    ["playerMoved"] = playerMoved,
                    ["cameraSettled"] = cameraSettled,
                    ["skyboxSettled"] = skyboxSettled,
                    ["elapsedSec"] = UnityEngine.Time.realtimeSinceStartup - startTime,
                };

                // Base64 conversion of the PNG happens off the main thread.
                await DCLTask.SwitchToThreadPool();
                return McpToolResult.ImageWithStructured(png, MIME_TYPE_PNG, structured);
            }
            finally
            {
                await UniTask.SwitchToMainThread();
                cinemachinePreset.RestoreFreeCameraProjection(in previousProjection);
            }
        }

        /// <summary>
        ///     Scenes stream in around the player, not the camera, so the player must stand inside the block. An
        ///     instant teleport intent (the same path scenes use for movePlayerTo) skips the loading screen.
        /// </summary>
        private bool ParkPlayerInBlock(Vector2Int minParcel, int size)
        {
            Vector2Int playerParcel = world.Get<CharacterTransform>(playerEntity).Position.ToParcel();

            if (playerParcel.x >= minParcel.x && playerParcel.x < minParcel.x + size
                && playerParcel.y >= minParcel.y && playerParcel.y < minParcel.y + size)
                return false;

            var anchorParcel = new Vector2Int(minParcel.x + (size / 2), minParcel.y + (size / 2));
            Vector3 anchor = ParcelMathHelper.GetPositionByParcelPosition(anchorParcel) + new Vector3(ParcelMathHelper.HALF_PARCEL_SIZE, PLAYER_DROP_HEIGHT, ParcelMathHelper.HALF_PARCEL_SIZE);

            world.AddOrSet(playerEntity, new PlayerTeleportIntent(null, anchorParcel, anchor, CancellationToken.None, isPositionSet: true, landOnParcel: true));
            return true;
        }

        private async UniTask<List<Vector2Int>> WaitForBlockLoadedAsync(Vector2Int minParcel, int size, float deadline, CancellationToken ct)
        {
            var pending = new List<Vector2Int>();

            while (true)
            {
                pending.Clear();

                for (int dx = 0; dx < size; dx++)
                for (int dy = 0; dy < size; dy++)
                {
                    var parcel = new Vector2Int(minParcel.x + dx, minParcel.y + dy);

                    if (!IsParcelReady(parcel))
                        pending.Add(parcel);
                }

                if (pending.Count == 0 || UnityEngine.Time.realtimeSinceStartup >= deadline)
                    return pending;

                await UniTask.Delay(POLL_INTERVAL_MS, cancellationToken: ct);
            }
        }

        private bool IsParcelReady(Vector2Int parcel)
        {
            if (scenesCache.TryGetByParcel(parcel, out ISceneFacade scene))
            {
                // A disposed/crashed scene will never conclude; nothing more will arrive for it, so stop waiting.
                return scene.SceneData.SceneLoadingConcluded || scene.SceneStateProvider.IsNotRunningState();
            }

            // Roads and baked LOD scenes register as non-real scenes once loaded. Parcels the world manifest lists as
            // unoccupied are never requested at all, so they count as ready outright; anything else is still being fetched.
            return scenesCache.ContainsNonRealScene(parcel) || realmData.WorldManifest.IsParcelKnownEmpty(parcel.x, parcel.y);
        }

        private async UniTask<bool> WaitForCameraAsync(Vector3 targetPosition, CancellationToken ct)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + CAMERA_SETTLE_TIMEOUT_SEC;

            while (UnityEngine.Time.realtimeSinceStartup < deadline)
            {
                if (Vector3.Distance(exposedCameraData.WorldPosition.Value, targetPosition) <= CAMERA_SETTLE_EPSILON)
                    return true;

                await UniTask.Delay(POLL_INTERVAL_MS / 2, cancellationToken: ct);
            }

            return false;
        }

        private async UniTask<byte[]> RenderAsync(Camera camera, int pixels, CancellationToken ct)
        {
            RenderTexture? worldRender = null;
            RenderTexture? output = null;
            Texture2D? readPixelsBuffer = null;

            try
            {
                // HDR target for the same reason as ScreenshotTool: an LDR target downgrades the whole URP render and
                // clamps emissives. The blit into the sRGB target below performs the linear-to-sRGB conversion.
                worldRender = RenderTexture.GetTemporary(pixels, pixels, 24, RenderTextureFormat.DefaultHDR);

                RenderTexture? previousTarget = camera.targetTexture;
                camera.targetTexture = worldRender;

                try { await WaitForEndOfFrameAsync(ct); }
                finally
                {
                    await UniTask.SwitchToMainThread();
                    camera.targetTexture = previousTarget;
                }

                var descriptor = new RenderTextureDescriptor(pixels, pixels)
                {
                    graphicsFormat = OutputGraphicsFormat(), sRGB = true, msaaSamples = 1, depthBufferBits = 0,
                    mipCount = 1, useMipMap = false,
                };

                output = RenderTexture.GetTemporary(descriptor);
                Graphics.Blit(worldRender, output);
                RenderTexture.ReleaseTemporary(worldRender);
                worldRender = null;

                AsyncGPUReadbackRequest readback = await AsyncGPUReadback.Request(output).WithCancellation(ct);

                if (!readback.hasError)
                {
                    NativeArray<byte> rawPixels = readback.GetData<byte>();

                    using NativeArray<byte> encoded = ImageConversion.EncodeNativeArrayToPNG(rawPixels, output.graphicsFormat, (uint)pixels, (uint)pixels);
                    return encoded.ToArray();
                }

                readPixelsBuffer = new Texture2D(pixels, pixels, TextureFormat.RGBA32, false);
                RenderTexture? previousActive = RenderTexture.active;
                RenderTexture.active = output;
                readPixelsBuffer.ReadPixels(new Rect(0, 0, pixels, pixels), 0, 0);
                readPixelsBuffer.Apply();
                RenderTexture.active = previousActive;
                return readPixelsBuffer.EncodeToPNG();
            }
            finally
            {
                await UniTask.SwitchToMainThread();

                if (worldRender != null) RenderTexture.ReleaseTemporary(worldRender);
                if (output != null) RenderTexture.ReleaseTemporary(output);
                if (readPixelsBuffer != null) Object.Destroy(readPixelsBuffer);
            }
        }

        private UniTask WaitForEndOfFrameAsync(CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource();
            coroutineRunner.StartCoroutine(SignalEndOfFrameCoroutine(completion));
            return completion.Task.AttachExternalCancellation(ct);
        }

        private static IEnumerator SignalEndOfFrameCoroutine(UniTaskCompletionSource completion)
        {
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;
            completion.TrySetResult();
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
