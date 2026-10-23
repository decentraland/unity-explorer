using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.BevyMigration.Bridge.Generated;
using DCL.BevyMigration.Composite;
using DCL.CharacterCamera;
using DCL.DebugUtilities.UIBindings;
using ECS.Abstract;
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
        private readonly RendererRegistry registry;
        private readonly SnapshotWriter writer;
        private readonly ElementBinding<string> statusBinding;
        private readonly DebugWidgetVisibilityBinding visibility;

        private static readonly QueryDescription CAMERA_QUERY = new QueryDescription().WithAll<CameraComponent>();

        private Entity cameraEntity = Entity.Null;
        private WorldRenderer appliedRenderer = WorldRenderer.Unity;

        internal BevyBridgeSystem(World world, RendererRegistry registry, SnapshotWriter writer, ElementBinding<string> statusBinding, DebugWidgetVisibilityBinding visibility) : base(world)
        {
            this.registry = registry;
            this.writer = writer;
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
                    BevyComposite.Source = null;
            }

            if (renderer != WorldRenderer.Bevy || !BevyBridge.IsInitialized)
            {
                if (visibility.IsExpanded)
                    statusBinding.Value = BevyBridge.IsInitialized ? "bridge idle (world=unity)" : "bridge not initialized";

                return;
            }

            if (!TryGetCamera(out Camera? unityCamera) || unityCamera == null)
                return;

            registry.RefreshFlags();
            JobHandle gather = writer.ScheduleGather();
            gather.Complete();
            BmResult submitted = writer.Submit(unityCamera);

            Texture2D? completed = BevyBridge.BeginFrame(out BmFrameStatus status);
            BevyComposite.Source = completed;

            if (visibility.IsExpanded)
                statusBinding.Value = $"frame {writer.Frame} submitted ({submitted}); completed {status.completed_frame} in buffer {status.completed_buffer}; {registry.Count} renderers; in flight {status.in_flight}";
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
