using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.CharacterTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.SDKComponents.TriggerArea.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using System;
using UnityEngine;

namespace DCL.SDKComponents.TriggerArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class TriggerAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly World globalWorld;

        public TriggerAreaHandlerSystem(World world, World globalWorld) : base(world)
        {
            this.globalWorld = globalWorld;
        }

        protected override void Update(float t)
        {
            SetupTriggerAreaQuery(World);
        }

        [Query]
        [None(typeof(CharacterTriggerAreaComponent), typeof(SDKTriggerAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupTriggerArea(Entity entity, in PBTriggerArea pbTriggerArea)
        {
            // TODO: make the CharacterTriggerAreaComponent more versatile, to accept scene entity colliders
            World.Add(entity, new SDKTriggerAreaComponent(), new CharacterTriggerAreaComponent(areaSize: Vector3.zero, targetOnlyMainPlayer: true));
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBTriggerArea), typeof(SDKTriggerAreaComponent))]
        private void HandleEntityDestruction(Entity entity)
        {
            OnExitedArea();
            // activeAreas.Remove(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBTriggerArea))]
        [All(typeof(SDKTriggerAreaComponent))]
        private void HandleComponentRemoval(Entity entity)
        {
            OnExitedArea();
            // activeAreas.Remove(entity);
            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        internal void OnEnteredArea()
        {
            // if (globalWorld.Has<InWorldCameraComponent>(cameraEntityProxy.Object))
            //     globalWorld.Add(cameraEntityProxy.Object, new ToggleInWorldCameraRequest { IsEnable = false, TargetCameraMode = targetCameraMode});
            //
            // ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            //
            // cameraModeBeforeLastAreaEnter = camera.Mode;
            // camera.Mode = targetCameraMode;
            // camera.AddCameraInputLock();
            //
            // sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.APPLIED));
        }

        internal void OnExitedArea()
        {
            // ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            //
            // camera.RemoveCameraInputLock();
            //
            // // If there are more locks then there is another newer camera mode area in place
            // if (camera.CameraInputChangeEnabled)
            //     camera.Mode = cameraModeBeforeLastAreaEnter == CameraMode.InWorld? CameraMode.ThirdPerson : cameraModeBeforeLastAreaEnter;
            //
            // sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.REMOVED));
        }

        [Query]
        [All(typeof(SDKTriggerAreaComponent))]
        private void FinalizeComponents(Entity entity)
        {
            // if (activeAreas.Remove(entity))
            //     OnExitedArea();

            World.Remove<SDKTriggerAreaComponent>(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }
    }
}



