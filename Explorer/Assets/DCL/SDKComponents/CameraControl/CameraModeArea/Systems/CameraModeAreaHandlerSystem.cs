using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using DCL.CharacterCamera;
using DCL.SDKEntityTriggerArea.Components;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.InWorldCamera;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SDKComponents.CameraModeArea.Components;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;
using Utility.Arch;

namespace DCL.SDKComponents.CameraModeArea.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationSystemGroup))]
    [LogCategory(ReportCategory.CHARACTER_TRIGGER_AREA)]
    public partial class CameraModeAreaHandlerSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        // There's only 1 camera at a time, only 1 camera mode area effect at a time
        // and that area is only activated by the main player ('targetOnlyMainPlayer' property)
        // that's why we have these fields as static.
        private static CameraMode cameraModeBeforeLastAreaEnter;

        // Main player can enter an area while being already inside another one, but the last one
        // entered is the one in effect, so the list keeps entry order. Areas are keyed by world and
        // entity: scene worlds recycle entity ids, so the entity alone does not identify an area.
        private static readonly List<ActiveArea> ACTIVE_AREAS = new ();

        private readonly World globalWorld;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly IExposedCameraData cameraData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly ISceneData sceneData;

        public CameraModeAreaHandlerSystem(World world, World globalWorld, ObjectProxy<Entity> cameraEntityProxy, IExposedCameraData cameraData,
            ISceneRestrictionBusController sceneRestrictionBusController, ISceneData sceneData) : base(world)
        {
            this.globalWorld = globalWorld;
            this.cameraEntityProxy = cameraEntityProxy;
            this.cameraData = cameraData;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.sceneData = sceneData;
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
        [None(typeof(SDKEntityTriggerAreaComponent), typeof(CameraModeAreaComponent))]
        [All(typeof(TransformComponent))]
        private void SetupCameraModeArea(in Entity entity, ref PBCameraModeArea pbCameraModeArea)
        {
            World.Add(entity,
                new SDKEntityTriggerAreaComponent(areaSize: pbCameraModeArea.Area, targetOnlyMainPlayer: true),
                new CameraModeAreaComponent()
            );
        }

        [Query]
        [All(typeof(TransformComponent), typeof(CameraModeAreaComponent))]
        private void UpdateCameraModeArea(Entity entity, ref PBCameraModeArea pbCameraModeArea, ref SDKEntityTriggerAreaComponent sdkEntityTriggerAreaComponent, in CRDTEntity crdtEntity)
        {
            if (pbCameraModeArea.IsDirty)
                sdkEntityTriggerAreaComponent.UpdateAreaSize(pbCameraModeArea.Area);

            if (cameraData.CameraMode == CameraMode.SdkCamera) return;

            if (sdkEntityTriggerAreaComponent.EnteredEntitiesToBeProcessed.Count > 0)
            {
                OnEnteredCameraModeArea(entity, crdtEntity.Id, (CameraMode)pbCameraModeArea.Mode, sdkEntityTriggerAreaComponent.AreaSize);
                sdkEntityTriggerAreaComponent.TryClearEnteredAvatarsToBeProcessed();
            }
            else if (sdkEntityTriggerAreaComponent.ExitedEntitiesToBeProcessed.Count > 0)
            {
                OnExitedCameraModeArea(entity);
                sdkEntityTriggerAreaComponent.TryClearExitedAvatarsToBeProcessed();
            }
        }

        [Query]
        [All(typeof(DeleteEntityIntention), typeof(PBCameraModeArea))]
        private void HandleEntityDestruction(Entity entity)
        {
            OnExitedCameraModeArea(entity);
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBCameraModeArea))]
        [All(typeof(CameraModeAreaComponent))]
        private void HandleComponentRemoval(Entity entity)
        {
            OnExitedCameraModeArea(entity);
            World.Remove<CameraModeAreaComponent>(entity);
        }

        /// <summary>Applies the area's camera mode and lock; an area already in effect is ignored.</summary>
        internal void OnEnteredCameraModeArea(Entity areaEntity, int crdtEntityId, CameraMode targetCameraMode, Vector3 areaSize)
        {
            if (IndexOfActiveArea(areaEntity) >= 0) return;

            Entity cameraEntity = cameraEntityProxy.Object;

            if (globalWorld.Has<InWorldCameraComponent>(cameraEntity))
                globalWorld.Add(cameraEntity, new ToggleInWorldCameraRequest { IsEnable = false, TargetCameraMode = targetCameraMode });

            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);

            cameraModeBeforeLastAreaEnter = camera.Mode;
            camera.Mode = targetCameraMode;
            camera.AddCameraInputLock();

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.Applied));

            SceneShortInfo scene = sceneData.SceneShortInfo;

            ACTIVE_AREAS.Add(new ActiveArea(World, areaEntity, new CameraModeAreaLock
            {
                SceneName = scene.Name,
                SceneBaseParcel = scene.BaseParcel,
                CrdtEntityId = crdtEntityId,
                Mode = targetCameraMode,
                AreaSize = areaSize,
            }));

            // The camera ref above is no longer touched: the lock component is a structural change on the camera entity.
            PublishLock(cameraEntity);
        }

        /// <summary>Releases the area's lock; an area that is not in effect is ignored.</summary>
        internal void OnExitedCameraModeArea(Entity areaEntity)
        {
            int index = IndexOfActiveArea(areaEntity);
            if (index < 0) return;

            ACTIVE_AREAS.RemoveAt(index);

            Entity cameraEntity = cameraEntityProxy.Object;
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntity);

            camera.RemoveCameraInputLock();

            // If there are more locks then there is another newer camera mode area in place
            if (camera.CameraInputChangeEnabled)
                camera.Mode = cameraModeBeforeLastAreaEnter == CameraMode.InWorld ? CameraMode.ThirdPerson : cameraModeBeforeLastAreaEnter;

            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateCameraLocked(SceneRestrictionsAction.Removed));

            PublishLock(cameraEntity);
        }

        [Query]
        [All(typeof(CameraModeAreaComponent))]
        private void FinalizeComponents(Entity entity)
        {
            OnExitedCameraModeArea(entity);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeComponentsQuery(World);
        }

        private int IndexOfActiveArea(Entity areaEntity)
        {
            for (var i = 0; i < ACTIVE_AREAS.Count; i++)
            {
                ActiveArea area = ACTIVE_AREAS[i];

                if (area.World == World && area.Entity == areaEntity)
                    return i;
            }

            return -1;
        }

        /// <summary>
        ///     Mirrors the newest active area onto the camera entity, or removes the mirror once no area is in effect.
        /// </summary>
        private void PublishLock(Entity cameraEntity)
        {
            if (ACTIVE_AREAS.Count == 0)
            {
                globalWorld.TryRemove<CameraModeAreaLock>(cameraEntity);
                return;
            }

            CameraModeAreaLock newest = ACTIVE_AREAS[ACTIVE_AREAS.Count - 1].Lock;
            newest.ActiveAreaCount = ACTIVE_AREAS.Count;
            globalWorld.AddOrSet(cameraEntity, newest);
        }

        private readonly struct ActiveArea
        {
            public readonly World World;
            public readonly Entity Entity;
            public readonly CameraModeAreaLock Lock;

            public ActiveArea(World world, Entity entity, CameraModeAreaLock areaLock)
            {
                World = world;
                Entity = entity;
                Lock = areaLock;
            }
        }
    }
}
