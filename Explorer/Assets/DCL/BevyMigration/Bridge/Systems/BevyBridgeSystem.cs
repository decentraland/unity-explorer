using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using Cinemachine;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.BevyMigration.Bridge.Generated;
using DCL.BevyMigration.Composite;
using DCL.CharacterCamera;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.Jobs;
using UnityEngine;

namespace DCL.BevyMigration.Bridge.Systems
{
    /// <summary>
    ///     The per-frame half of the render bridge (BevyMigration/docs/README.md § 8 "Topology"): gather transforms
    ///     with the Burst job, write and submit snapshot N, and hand the composite feature the output buffer that
    ///     holds completed frame N-1. Runs last in the frame so the transforms it gathers are final.
    /// </summary>
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class BevyBridgeSystem : BaseUnityLoopSystem
    {
        private const int SCAN_EVERY_FRAMES = 60;
        private const int BUDGET_LOG_EVERY_FRAMES = 600;
        private const double BUDGET_SMOOTHING = 0.05;
        private static readonly ReportData REPORT_DATA = new (ReportCategory.ALWAYS);

        private readonly RendererRegistry registry;
        private readonly SnapshotWriter writer;
        private readonly SceneContentScanner scanner;
        private readonly AvatarContentScanner avatars;
        private readonly ElementBinding<string> statusBinding;
        private readonly DebugWidgetVisibilityBinding visibility;
        private int framesUntilScan;
        private int framesUntilBudgetLog = BUDGET_LOG_EVERY_FRAMES;
        private double gatherMsAverage;
        private Camera? ownedCamera;
        private readonly List<(GameObject, int)> parkedVirtualCameras = new ();
        private int previousCullingMask;
        private readonly System.Diagnostics.Stopwatch stopwatch = new ();

        private static readonly QueryDescription CAMERA_QUERY = new QueryDescription().WithAll<CameraComponent>();

        private Entity cameraEntity = Entity.Null;
        private WorldRenderer appliedRenderer = WorldRenderer.Unity;

        internal BevyBridgeSystem(World world, RendererRegistry registry, SnapshotWriter writer, SceneContentScanner scanner, AvatarContentScanner avatars, ElementBinding<string> statusBinding, DebugWidgetVisibilityBinding visibility) : base(world)
        {
            this.registry = registry;
            this.writer = writer;
            this.scanner = scanner;
            this.avatars = avatars;
            this.statusBinding = statusBinding;
            this.visibility = visibility;
        }

        protected override void Update(float t)
        {
            WorldRenderer renderer = WorldRendererFlag.Current;

            if (renderer != appliedRenderer)
            {
                appliedRenderer = renderer;
                registry.SetUnityRenderingEnabled(renderer == WorldRenderer.Unity);

                if (renderer == WorldRenderer.Unity)
                {
                    BevyComposite.Source = null;
                    ReleaseCamera();
                }
            }

            if (renderer != WorldRenderer.Bevy || !BevyBridge.IsInitialized)
            {
                if (visibility.IsExpanded)
                    statusBinding.Value = BevyBridge.IsInitialized ? "bridge idle (world=unity)" : "bridge not initialized";

                return;
            }

            if (!TryGetCamera(out Camera? unityCamera) || unityCamera == null)
                return;

            TakeOverCamera(unityCamera);
            stopwatch.Restart();

            if (--framesUntilScan <= 0)
            {
                framesUntilScan = SCAN_EVERY_FRAMES;
                scanner.Scan();
                avatars.Begin();
                ScanAvatarsQuery(World);
                registry.SetUnityRenderingEnabled(false);
            }

            registry.RefreshFlags();
            JobHandle gather = writer.ScheduleGather();
            gather.Complete();
            BmResult submitted = writer.Submit(unityCamera, scanner.Sun, scanner.SceneLights);

            Texture2D? completed = BevyBridge.BeginFrame(out BmFrameStatus status);
            BevyComposite.Source = completed;
            TrackBudget();

            if (visibility.IsExpanded)
                statusBinding.Value = $"frame {writer.Frame} ({submitted}); completed {status.completed_frame} in buffer {status.completed_buffer}; {registry.Count}/{scanner.ScannedRenderers} renderers; {avatars.Avatars} avatars ({registry.BoneCount} bones); gather+submit {gatherMsAverage:F2} ms; {scanner.SceneLights.Count} lights; {scanner.Uploader.MeshCount} meshes, {scanner.Uploader.TextureCount} textures, {scanner.Uploader.MaterialCount} materials uploaded; in flight {status.in_flight}";
        }

        /// <summary>
        ///     Step 5: Unity renders no world. The main camera culls everything and clears to a solid colour (no
        ///     skybox pass); only the composite feature and the UI remain on it. Cinemachine drives the camera only
        ///     from virtual cameras whose layer is in the culling mask, so they are parked on an unused layer that
        ///     becomes the whole mask.
        /// </summary>
        private void TakeOverCamera(Camera camera)
        {
            if (ReferenceEquals(ownedCamera, camera)) return;

            ReleaseCamera();
            ownedCamera = camera;
            previousCullingMask = camera.cullingMask;
            int layer = FirstUnnamedLayer();

            foreach (CinemachineVirtualCameraBase virtualCamera in UnityEngine.Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include))
            {
                GameObject target = virtualCamera.gameObject;
                parkedVirtualCameras.Add((target, target.layer));
                target.layer = layer;
            }

            // The clear flags stay: Unity keeps drawing the skybox and the composite blends Bevy's frame over it.
            camera.cullingMask = 1 << layer;
        }

        private void ReleaseCamera()
        {
            foreach ((GameObject target, int layer) in parkedVirtualCameras)
                if (target != null)
                    target.layer = layer;

            parkedVirtualCameras.Clear();

            if (ownedCamera == null)
            {
                ownedCamera = null;
                return;
            }

            ownedCamera.cullingMask = previousCullingMask;
            ownedCamera = null;
        }

        private static int FirstUnnamedLayer()
        {
            for (var layer = 8; layer < 32; layer++)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                    return layer;

            return 31;
        }

        /// <summary>Per-frame gather plus submit cost, smoothed and logged every <see cref="BUDGET_LOG_EVERY_FRAMES" /> frames.</summary>
        private void TrackBudget()
        {
            stopwatch.Stop();
            double ms = stopwatch.Elapsed.TotalMilliseconds;
            gatherMsAverage = gatherMsAverage == 0 ? ms : gatherMsAverage + ((ms - gatherMsAverage) * BUDGET_SMOOTHING);

            if (--framesUntilBudgetLog > 0) return;

            framesUntilBudgetLog = BUDGET_LOG_EVERY_FRAMES;
            ReportHub.Log(REPORT_DATA, $"Bevy gather+submit {gatherMsAverage:F2} ms/frame over {registry.Count} renderers and {registry.BoneCount} bones");
        }

        [Query]
        private void ScanAvatars(in AvatarShapeComponent shape, in AvatarBase avatarBase)
        {
            avatars.Register(in shape, avatarBase);
        }

        private bool TryGetCamera(out Camera? unityCamera)
        {
            unityCamera = null;

            if (cameraEntity.IsNull() || !World.IsAlive(cameraEntity))
            {
                cameraEntity = World.GetSingleInstanceEntityOrNull(in CAMERA_QUERY);
                if (cameraEntity.IsNull()) return false;
            }

            unityCamera = World.Get<CameraComponent>(cameraEntity).Camera;
            return unityCamera != null;
        }
    }
}
