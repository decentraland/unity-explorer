using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.CharacterTriggerArea.Components;
using DCL.CharacterTriggerArea.Systems;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [UpdateBefore(typeof(CharacterTriggerAreaCleanUpRegisteredCollisionsSystem))]
    [LogCategory(ReportCategory.CAMERA_MODE_AREA)]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private static CameraMode cameraModeBeforeLastAreaEnter; // There's only 1 camera at a time

        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntityProxy;

        public CameraModeAreaHandlerSystem(World world, ObjectProxy<World> globalWorldProxy, ObjectProxy<Entity> cameraEntityProxy) : base(world)
        {
            globalWorld = globalWorldProxy.Object;
            this.cameraEntityProxy = cameraEntityProxy;
        }

        protected override void Update(float t)
        {
            if (!cameraEntityProxy.Configured) return;

            UpdateCameraModeAreaQuery(World);
            SetupCameraModeAreaQuery(World);

            HandleComponentRemovalQuery(World);
            HandleEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent), typeof(CameraModeAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            World.Add(entity, new CameraModeAreaComponent(), new CharacterTriggerAreaComponent(areaSize: pbCameraModeArea.Area, targetOnlyMainPlayer: true));
        }

        [Query]
        [All(typeof(TransformComponent))]
        private void UpdateCameraModeArea(ref PBCameraModeArea pbCameraModeArea, ref CharacterTriggerAreaComponent characterTriggerAreaComponent)
        {
            if (characterTriggerAreaComponent.EnteredThisFrame!.Count > 0) { OnEnteredCameraModeArea((CameraMode)pbCameraModeArea.Mode); }
            else if (characterTriggerAreaComponent.ExitedThisFrame!.Count > 0) { OnExitedCameraModeArea(); }

            if (pbCameraModeArea.IsDirty)
            {
                characterTriggerAreaComponent.AreaSize = pbCameraModeArea.Area;
                characterTriggerAreaComponent.IsDirty = true;
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBCameraModeArea), typeof(CameraModeAreaComponent))]
        private void HandleEntityDestruction()
        {
            OnExitedCameraModeArea();
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea))]
        [All(typeof(CameraModeAreaComponent))]
        private void HandleComponentRemoval(Entity e)
        {
            OnExitedCameraModeArea();
            World.Remove<CameraModeAreaComponent>(e);
        }

        internal void OnEnteredCameraModeArea(CameraMode targetCameraMode)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            cameraModeBeforeLastAreaEnter = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.AddCameraInputLock();
        }

        internal void OnExitedCameraModeArea()
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);

            camera.RemoveCameraInputLock();

            // If there are more locks then there is another newer camera mode area in place
            if (camera.CameraInputLocks == 0)
                camera.Mode = cameraModeBeforeLastAreaEnter;
        }

        [Query]
        [All(typeof(CameraModeAreaComponent))]
        private void FinalizeComponents()
        {
            OnExitedCameraModeArea();
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
            World.Remove<CameraModeAreaComponent>(FinalizeComponents_QueryDescription);
        }
    }
}
