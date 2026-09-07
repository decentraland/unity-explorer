using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.CharacterCamera.Components;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.SkyBox;
using DCL.SkyBox.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using Global.Versioning;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Arch;
using Object = UnityEngine.Object;

namespace DCL.PerformanceBenchmark
{
    /// <summary>
    ///     Drives the deterministic "plaza recording" once the world has finished loading: freezes the
    ///     skybox time of day, takes over the free camera, runs the scripted camera path while sampling
    ///     per-frame CPU/GPU times, captures the three golden backbuffer PNGs, writes frames.csv +
    ///     results.json into the output directory and quits the application (exit code 0 on success).
    /// </summary>
    public class PlazaBenchRunner
    {
        private const string CSV_FILE_NAME = "frames.csv";
        private const string RESULTS_FILE_NAME = "results.json";

        private const float FROZEN_TIME_OF_DAY_NORMALIZED = 0.5f;
        private const float WARMUP_SECONDS = 5f;
        private const float WORLD_READY_TIMEOUT_SECONDS = 600f;
        private const float ANCHOR_SETTLE_SECONDS = 2f;
        private const float FIXED_FOV = 60f;
        private const float LOCKSTEP_DELTA_SECONDS = 1f / 60f;
        private const int CAMERA_APPLY_DELAY_FRAMES = 2;
        private const int SKYBOX_APPLY_DELAY_FRAMES = 2;
        private const int GOLDEN_SETTLE_FRAMES = 30;

        private static readonly (string name, float pathTime)[] GOLDEN_POSES =
        {
            ("P1", PlazaBenchCameraPath.POSE_P1_TIME),
            ("P2", PlazaBenchCameraPath.POSE_P2_TIME),
            ("P3", PlazaBenchCameraPath.POSE_P3_TIME),
        };

        private readonly string outputDirectory;
        private readonly World world;
        private readonly Entity playerEntity;
        private readonly Entity skyboxEntity;
        private readonly IReadOnlyLoadingStatus loadingStatus;
        private readonly IScenesCache scenesCache;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly IProfiler profiler;
        private readonly ICoroutineRunner coroutineRunner;
        private readonly DCLVersion dclVersion;
        private readonly bool lockstepTime;
        private readonly Vector3? anchorOverride;

        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private readonly List<double> frameTimingCpuSamples = new ();
        private readonly List<double> frameTimingGpuSamples = new ();
        private readonly List<double> recorderCpuSamples = new ();
        private readonly List<double> recorderGpuSamples = new ();

        private int invalidFrameTimingSamples;

        public PlazaBenchRunner(string outputDirectory,
            World world,
            Entity playerEntity,
            Entity skyboxEntity,
            IReadOnlyLoadingStatus loadingStatus,
            IScenesCache scenesCache,
            SkyboxSettingsAsset skyboxSettings,
            IProfiler profiler,
            ICoroutineRunner coroutineRunner,
            DCLVersion dclVersion,
            bool lockstepTime,
            string? anchorArg)
        {
            this.outputDirectory = outputDirectory;
            this.world = world;
            this.playerEntity = playerEntity;
            this.skyboxEntity = skyboxEntity;
            this.loadingStatus = loadingStatus;
            this.scenesCache = scenesCache;
            this.skyboxSettings = skyboxSettings;
            this.profiler = profiler;
            this.coroutineRunner = coroutineRunner;
            this.dclVersion = dclVersion;
            this.lockstepTime = lockstepTime;

            if (!string.IsNullOrEmpty(anchorArg))
            {
                string[] parts = anchorArg.Split(',');

                if (parts.Length == 3
                    && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float ax)
                    && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float ay)
                    && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float az))
                    anchorOverride = new Vector3(ax, ay, az);
                else
                    ReportHub.Log(ReportCategory.ALWAYS, $"[PlazaBench] Invalid --plaza-bench-anchor '{anchorArg}' - expected x,y,z; using the spawn position");
            }
        }

        public async UniTask RunAsync(CancellationToken ct)
        {
            var exitCode = 0;
            var quitApplication = true;

            var results = new PlazaBenchResults
            {
                StartedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                OriginalVSyncCount = QualitySettings.vSyncCount,
                OriginalTargetFrameRate = Application.targetFrameRate,
                LockstepTime = lockstepTime,
                RequestedTimeOfDayNormalized = FROZEN_TIME_OF_DAY_NORMALIZED,
                WarmupSeconds = WARMUP_SECONDS,
                StandSeconds = PlazaBenchCameraPath.STAND_SECONDS,
                OrbitSeconds = PlazaBenchCameraPath.ORBIT_SECONDS,
                PullBackSeconds = PlazaBenchCameraPath.PULL_BACK_SECONDS,
            };

            try
            {
                Directory.CreateDirectory(outputDirectory);

                await WaitUntilWorldReadyAsync(ct);
                await FreezeSkyboxAsync(results, ct);

                ICinemachinePreset cinemachinePreset = await EnterFreeCameraAsync(ct);

                await UniTask.Delay(TimeSpan.FromSeconds(ANCHOR_SETTLE_SECONDS), cancellationToken: ct);

                Vector3 anchor;

                if (anchorOverride.HasValue)
                {
                    // Pin the run to a fixed world position: scene spawn points randomize within
                    // an area, which shifts the camera path and changes the rendered workload.
                    anchor = anchorOverride.Value;
                    world.AddOrSet(playerEntity, new PlayerTeleportIntent(null, Vector2Int.zero, anchor, ct, isPositionSet: true));
                    await UniTask.Delay(TimeSpan.FromSeconds(ANCHOR_SETTLE_SECONDS), cancellationToken: ct);
                }
                else
                    anchor = world.Get<CharacterTransform>(playerEntity).Position;
                results.SpawnAnchor = ToArray(anchor);

                ApplyDeterminismOverrides();
                cinemachinePreset.ForceFreeCameraPose(PlazaBenchCameraPath.Evaluate(anchor, 0f).Position, FIXED_FOV);

                await RecordAsync(cinemachinePreset, anchor, ct);
                await CaptureGoldensAsync(cinemachinePreset, anchor, results, ct);

                FillEnvironmentInfo(results);
                results.DclVersion = dclVersion.Version;
                ComputeStats(results);
                results.FinishedAtUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                File.WriteAllText(Path.Combine(outputDirectory, RESULTS_FILE_NAME), JsonConvert.SerializeObject(results, Formatting.Indented));

                ReportHub.Log(ReportCategory.ALWAYS, $"[PlazaBench] Recording complete: {results.SampleCount} samples written to {outputDirectory}");
            }
            catch (OperationCanceledException)
            {
                // The application is already shutting down; do not force a second quit.
                quitApplication = false;
            }
            catch (Exception ex)
            {
                ReportHub.LogException(ex, ReportCategory.ALWAYS);
                exitCode = 1;
            }
            finally
            {
                if (quitApplication)
                    Application.Quit(exitCode);
            }
        }

        private async UniTask WaitUntilWorldReadyAsync(CancellationToken ct)
        {
            float deadline = UnityEngine.Time.realtimeSinceStartup + WORLD_READY_TIMEOUT_SECONDS;

            while (loadingStatus.CurrentStage.Value != LoadingStatus.LoadingStage.Completed)
            {
                ThrowIfTimedOut(deadline, "the loading flow to complete");
                await UniTask.Yield(ct);
            }

            while (scenesCache.CurrentScene.Value is not { } currentScene || !currentScene.IsSceneReady())
            {
                ThrowIfTimedOut(deadline, "the current scene to become ready");
                await UniTask.Yield(ct);
            }
        }

        private async UniTask FreezeSkyboxAsync(PlazaBenchResults results, CancellationToken ct)
        {
            skyboxSettings.IsUIControlled = true;
            skyboxSettings.UIOverrideTimeOfDayNormalized = FROZEN_TIME_OF_DAY_NORMALIZED;
            skyboxSettings.TargetTimeOfDayNormalized = FROZEN_TIME_OF_DAY_NORMALIZED;
            skyboxSettings.TimeOfDayNormalized = FROZEN_TIME_OF_DAY_NORMALIZED;
            skyboxSettings.IsDayCycleEnabled = false;

            // Let SkyboxTimeUpdateSystem push the pinned time into the render controller, then hard-freeze
            // the whole skybox state machine. A scene/SDK-controlled skybox may override the requested value
            // in that window; the value actually frozen is recorded either way (it is per-scene deterministic).
            await UniTask.DelayFrame(SKYBOX_APPLY_DELAY_FRAMES, cancellationToken: ct);
            world.AddOrGet(skyboxEntity, new PauseSkyboxTimeUpdate());

            results.FrozenTimeOfDayNormalized = skyboxSettings.TimeOfDayNormalized;
            results.SkyboxFrozenAtRequestedTime = Mathf.Approximately(skyboxSettings.TimeOfDayNormalized, FROZEN_TIME_OF_DAY_NORMALIZED);
        }

        private async UniTask<ICinemachinePreset> EnterFreeCameraAsync(CancellationToken ct)
        {
            SwitchCameraToFreeMode();

            // Let ControlCinemachineVirtualCameraSystem activate the free vcam before the pose overrides it.
            await UniTask.DelayFrame(CAMERA_APPLY_DELAY_FRAMES, cancellationToken: ct);

            SingleInstanceEntity cameraEntity = world.CacheCamera();

            if (!world.TryGet(cameraEntity, out ICinemachinePreset? cinemachinePreset) || cinemachinePreset == null)
                throw new InvalidOperationException("The camera rig is not initialized yet.");

            return cinemachinePreset;
        }

        private void SwitchCameraToFreeMode()
        {
            SingleInstanceEntity cameraEntity = world.CacheCamera();
            ref CameraComponent camera = ref cameraEntity.GetCameraComponent(world);

            if (camera.Mode == CameraMode.SdkCamera)
                throw new InvalidOperationException("A scene virtual camera controls the view; the benchmark cannot take the free camera.");

            if (!camera.CameraInputChangeEnabled)
                throw new InvalidOperationException($"Camera mode is locked by the scene (current mode: {camera.Mode}); the benchmark cannot take the free camera.");

            camera.Mode = CameraMode.Free;
        }

        private void ApplyDeterminismOverrides()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;

            if (lockstepTime)
                UnityEngine.Time.captureDeltaTime = LOCKSTEP_DELTA_SECONDS;
        }

        private async UniTask RecordAsync(ICinemachinePreset cinemachinePreset, Vector3 anchor, CancellationToken ct)
        {
            var warmupElapsed = 0f;

            while (warmupElapsed < WARMUP_SECONDS)
            {
                DrivePose(cinemachinePreset, PlazaBenchCameraPath.Evaluate(anchor, 0f));
                await UniTask.Yield(ct);
                warmupElapsed += UnityEngine.Time.unscaledDeltaTime;
            }

            using var csv = new StreamWriter(Path.Combine(outputDirectory, CSV_FILE_NAME), false, new UTF8Encoding(false));
            csv.NewLine = "\r\n"; // https://www.rfc-editor.org/rfc/rfc4180
            csv.WriteLine("\"Frame\",\"PathTime\",\"FtmCpuMs\",\"FtmGpuMs\",\"RecorderCpuMs\",\"RecorderGpuMs\",\"UnscaledDeltaMs\"");

            var pathTime = 0f;

            while (pathTime < PlazaBenchCameraPath.TOTAL_SECONDS)
            {
                DrivePose(cinemachinePreset, PlazaBenchCameraPath.Evaluate(anchor, pathTime));
                await UniTask.Yield(ct);
                pathTime += UnityEngine.Time.unscaledDeltaTime;
                WriteSample(csv, pathTime);
            }
        }

        private void WriteSample(StreamWriter csv, float pathTime)
        {
            FrameTimingManager.CaptureFrameTimings();
            uint availableTimings = FrameTimingManager.GetLatestTimings(1, frameTimings);

            double ftmCpuMs = availableTimings > 0 ? frameTimings[0].cpuFrameTime : 0d;
            double ftmGpuMs = availableTimings > 0 ? frameTimings[0].gpuFrameTime : 0d;

            if (availableTimings == 0 || ftmCpuMs <= 0d)
                invalidFrameTimingSamples++;

            double recorderCpuMs = profiler.LastFrameTimeValueNs * 1e-6;
            double recorderGpuMs = profiler.LastGpuFrameTimeValueNs * 1e-6;

            frameTimingCpuSamples.Add(ftmCpuMs);
            frameTimingGpuSamples.Add(ftmGpuMs);
            recorderCpuSamples.Add(recorderCpuMs);
            recorderGpuSamples.Add(recorderGpuMs);

            csv.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F4},{2:F4},{3:F4},{4:F4},{5:F4},{6:F4}",
                UnityEngine.Time.frameCount, pathTime, ftmCpuMs, ftmGpuMs, recorderCpuMs, recorderGpuMs, UnityEngine.Time.unscaledDeltaTime * 1000f));
        }

        private async UniTask CaptureGoldensAsync(ICinemachinePreset cinemachinePreset, Vector3 anchor, PlazaBenchResults results, CancellationToken ct)
        {
            var goldens = new PlazaBenchResults.GoldenCapture[GOLDEN_POSES.Length];

            for (var i = 0; i < GOLDEN_POSES.Length; i++)
            {
                (string name, float poseTime) = GOLDEN_POSES[i];
                PlazaBenchPose pose = PlazaBenchCameraPath.Evaluate(anchor, poseTime);

                for (var frame = 0; frame < GOLDEN_SETTLE_FRAMES; frame++)
                {
                    DrivePose(cinemachinePreset, pose);
                    await UniTask.Yield(ct);
                }

                var fileName = $"golden_{name}.png";
                await CaptureBackbufferPngAsync(Path.Combine(outputDirectory, fileName), ct);

                goldens[i] = new PlazaBenchResults.GoldenCapture
                {
                    Name = name,
                    File = fileName,
                    PathTime = poseTime,
                    Position = ToArray(pose.Position),
                    LookAt = ToArray(pose.LookAt),
                };
            }

            results.Goldens = goldens;
        }

        private async UniTask CaptureBackbufferPngAsync(string filePath, CancellationToken ct)
        {
            var completion = new UniTaskCompletionSource<Texture2D>();
            coroutineRunner.StartCoroutine(CaptureBackbufferCoroutine(completion));
            Texture2D texture = await completion.Task.AttachExternalCancellation(ct);

            try
            {
                File.WriteAllBytes(filePath, texture.EncodeToPNG());
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        private static IEnumerator CaptureBackbufferCoroutine(UniTaskCompletionSource<Texture2D> completion)
        {
            // The back buffer is only complete at the very end of the frame.
            yield return GameObjectExtensions.WAIT_FOR_END_OF_FRAME;

            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();

            if (!completion.TrySetResult(texture))
                Object.Destroy(texture);
        }

        private void ComputeStats(PlazaBenchResults results)
        {
            results.SampleCount = frameTimingCpuSamples.Count;
            results.InvalidFrameTimingSamples = invalidFrameTimingSamples;
            results.CsvFile = CSV_FILE_NAME;

            results.FrameTimingCpuMs = PlazaBenchResults.FrameStats.FromSamples(frameTimingCpuSamples);
            results.FrameTimingGpuMs = PlazaBenchResults.FrameStats.FromSamples(frameTimingGpuSamples);
            results.RecorderCpuMs = PlazaBenchResults.FrameStats.FromSamples(recorderCpuSamples);
            results.RecorderGpuMs = PlazaBenchResults.FrameStats.FromSamples(recorderGpuSamples);

            bool frameTimingUsable = results.SampleCount > 0 && invalidFrameTimingSamples * 2 < results.SampleCount;
            results.PrimaryTimingSource = frameTimingUsable ? "FrameTimingManager" : "ProfilerRecorder";
            results.CpuMs = frameTimingUsable ? results.FrameTimingCpuMs : results.RecorderCpuMs;
            results.GpuMs = frameTimingUsable ? results.FrameTimingGpuMs : results.RecorderGpuMs;
        }

        private static void FillEnvironmentInfo(PlazaBenchResults results)
        {
            results.AppVersion = Application.version;
            results.UnityVersion = Application.unityVersion;
            results.BuildGuid = Application.buildGUID;
            results.Platform = Application.platform.ToString();
            results.GraphicsDeviceName = SystemInfo.graphicsDeviceName;
            results.GraphicsDeviceVersion = SystemInfo.graphicsDeviceVersion;
            results.GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
            results.ScreenWidth = Screen.width;
            results.ScreenHeight = Screen.height;
            results.FullScreenMode = Screen.fullScreenMode.ToString();
            results.QualityLevel = QualitySettings.GetQualityLevel();
            results.QualityLevelName = QualitySettings.names[QualitySettings.GetQualityLevel()];
            results.FrameTimingManagerFeatureEnabled = FrameTimingManager.IsFeatureEnabled();
        }

        private static void DrivePose(ICinemachinePreset cinemachinePreset, in PlazaBenchPose pose)
        {
            cinemachinePreset.ForceFreeCameraPose(pose.Position);

            // The free camera aims from its own position, so the intent origin is the camera position.
            cinemachinePreset.ForceFreeCameraLookAt(new CameraLookAtIntent(pose.LookAt, pose.Position));
        }

        private static void ThrowIfTimedOut(float deadlineRealtime, string what)
        {
            if (UnityEngine.Time.realtimeSinceStartup > deadlineRealtime)
                throw new TimeoutException($"[PlazaBench] Timed out waiting for {what}.");
        }

        private static float[] ToArray(Vector3 v) =>
            new[] { v.x, v.y, v.z };
    }
}
