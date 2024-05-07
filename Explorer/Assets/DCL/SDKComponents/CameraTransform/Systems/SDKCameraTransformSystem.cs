using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraTransform.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms;
using ECS.Unity.Transforms.Components;
using ECS.Unity.Transforms.Systems;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.CameraTransform.Systems
{
    /// <summary>
    ///     This system creates a new entity using the reserved SpecialEntitiesID.CAMERA_ENTITY in order to create a TransformComponent to be on sync with the camera
    ///     This allows using engine.CameraEntity as a parent target in the SDK
    /// </summary>
    [UpdateInGroup(typeof(SyncedSimulationSystemGroup))]
    [UpdateBefore(typeof(UpdateTransformSystem))]
    [LogCategory(ReportCategory.CAMERA_TRANSFORM)]
    public partial class SDKCameraTransformSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly Dictionary<CRDTEntity, Entity> entitiesMap;
        private readonly ObjectProxy<Entity> cameraEntityProxy;
        private readonly World globalWorld;
        private readonly IComponentPool<Transform> transformPool;
        private readonly CRDTEntity sdkCameraEntity;
        private Entity cameraEntityMirror;

        private SDKCameraTransformSystem(World world, Dictionary<CRDTEntity, Entity> entitiesMap, ObjectProxy<Entity> cameraEntityProxy, ObjectProxy<World> globalWorldProxy, IComponentPool<Transform> transformPool) : base(world)
        {
            this.entitiesMap = entitiesMap;
            this.cameraEntityProxy = cameraEntityProxy;
            globalWorld = globalWorldProxy.Object;
            this.transformPool = transformPool;
            sdkCameraEntity = new CRDTEntity(SpecialEntitiesID.CAMERA_ENTITY);
        }

        public override void Initialize()
        {
            cameraEntityMirror = World.Create(sdkCameraEntity, new SDKCameraComponent());
            TransformComponent transform = transformPool.CreateTransformComponent(cameraEntityMirror, sdkCameraEntity);
            transform.Transform.SetParent(null);
            World.Add(cameraEntityMirror, transform);
            entitiesMap.Add(sdkCameraEntity, cameraEntityMirror);
        }

        protected override void Update(float t)
        {
            if (!cameraEntityProxy.Configured) return;
            UpdateCameraTransformQuery(World);
        }

        [Query]
        [All(typeof(SDKCameraComponent))]
        private void UpdateCameraTransform(ref TransformComponent transformComponent)
        {
            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(cameraEntityProxy.Object!);
            Transform cameraTransform = camera.Camera.transform;
            transformComponent.SetTransform(cameraTransform.position, cameraTransform.rotation, cameraTransform.localScale);
        }

        [Query]
        [All(typeof(SDKCameraComponent))]
        private void CleanUpCameraTransform(ref TransformComponent transformComponent)
        {
            transformPool.Release(transformComponent.Transform);
        }

        public void FinalizeComponents(in Query query)
        {
            CleanUpCameraTransformQuery(World);
        }
    }
}
