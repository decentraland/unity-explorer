using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.MainCamera.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using ECS.Unity.Transforms.Components;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.CameraControl.MainCamera.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [UpdateBefore(typeof(MainCameraSystem))]
    [LogCategory(ReportCategory.SDK_MAIN_CAMERA)]
    public partial class VirtualCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineFreeLook> poolRegistry;
        private readonly ISceneStateProvider sceneStateProvider;

        public VirtualCameraSystem(
            World world,
            IComponentPool<CinemachineFreeLook> poolRegistry,
            ISceneStateProvider sceneStateProvider) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.sceneStateProvider = sceneStateProvider;
        }

        protected override void Update(float t)
        {
            SetupVirtualCameraQuery(World);

            HandleVirtualCameraRemovalQuery(World);
            HandleVirtualCameraEntityDestructionQuery(World);
        }

        [Query]
        [None(typeof(VirtualCameraComponent), typeof(DeleteEntityIntention))]
        private void SetupVirtualCamera(Entity entity, CRDTEntity crdtEntity, in PBVirtualCamera pbVirtualCamera, in TransformComponent transform)
        {
            if (!sceneStateProvider.IsCurrent) return;

            var virtualCameraInstance = poolRegistry.Get();
            virtualCameraInstance.transform.SetParent(transform.Transform);
            virtualCameraInstance.transform.localPosition = Vector3.zero;
            virtualCameraInstance.transform.localRotation = Quaternion.identity;
            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity.Id)));
        }

        [Query]
        [None(typeof(DeleteEntityIntention), typeof(PBVirtualCamera))]
        private void HandleVirtualCameraRemoval(Entity entity, in VirtualCameraComponent component)
        {
            component.virtualCameraInstance.enabled = false;
            poolRegistry.Release(component.virtualCameraInstance);
            World.Remove<VirtualCameraComponent>(entity);
        }

        [Query]
        [All(typeof(DeleteEntityIntention))]
        private void HandleVirtualCameraEntityDestruction(in VirtualCameraComponent component)
        {
            component.virtualCameraInstance.enabled = false;
            poolRegistry.Release(component.virtualCameraInstance);
        }

        [Query]
        private void FinalizeVirtualCameraComponents(in VirtualCameraComponent virtualCameraComponent)
        {
            poolRegistry.Release(virtualCameraComponent.virtualCameraInstance);
        }

        public void FinalizeComponents(in Query query)
        {
            FinalizeVirtualCameraComponentsQuery(World);
        }
    }
}
