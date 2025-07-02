using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CRDT;
using CrdtEcsBridge.Components.Transform;
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
using Utility;

namespace DCL.SDKComponents.CameraControl.MainCamera.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [UpdateBefore(typeof(MainCameraSystem))]
    [LogCategory(ReportCategory.SDK_CAMERA)]
    public partial class VirtualCameraSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineFreeLook> poolRegistry;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes;

        public VirtualCameraSystem(
            World world,
            IComponentPool<CinemachineFreeLook> poolRegistry,
            ISceneStateProvider sceneStateProvider,
            ParcelMathHelper.SceneCircumscribedPlanes sceneCircumscribedPlanes) : base(world)
        {
            this.poolRegistry = poolRegistry;
            this.sceneStateProvider = sceneStateProvider;
            this.sceneCircumscribedPlanes = sceneCircumscribedPlanes;
        }

        protected override void Update(float t)
        {
            SetupVirtualCameraQuery(World);

            HandleVirtualCameraRemovalQuery(World);
            HandleVirtualCameraEntityDestructionQuery(World);
            ClampVirtualCameraToSceneBoundsQuery(World);
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
            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance, VirtualCameraUtils.GetPBVirtualCameraLookAtCRDTEntity(pbVirtualCamera, crdtEntity)));
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
        private void ClampVirtualCameraToSceneBounds(in VirtualCameraComponent virtualCameraComponent, in SDKTransform sdkTransform, in TransformComponent transformComponent)
        {
            if (!sdkTransform.IsDirty) return;

            if (sceneCircumscribedPlanes.Contains(transformComponent.Transform.position))
            {
                virtualCameraComponent.virtualCameraInstance.transform.localPosition = Vector3.zero;
                return;
            }

            // Position is outside bounds, clamp to border with extra space
            Vector3 clampedPosition = sceneCircumscribedPlanes.GetNearestSceneBoundsPosition(transformComponent.Transform.position, 0.5f);
            virtualCameraComponent.virtualCameraInstance.transform.position = clampedPosition;
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
