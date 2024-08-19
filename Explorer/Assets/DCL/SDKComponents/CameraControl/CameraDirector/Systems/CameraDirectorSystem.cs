using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Diagnostics;
using Cinemachine;
using CrdtEcsBridge.Components.Transform;
using DCL.ECSComponents;
using DCL.Optimization.Pools;
using DCL.SDKComponents.CameraControl.CameraDirector.Components;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;

namespace DCL.SDKComponents.CameraControl.CameraDirector.Systems
{
    [UpdateInGroup(typeof(SyncedInitializationFixedUpdateThrottledGroup))]
    [LogCategory(ReportCategory.CAMERA_DIRECTOR)]
    public partial class CameraDirectorSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly IComponentPool<CinemachineVirtualCamera> poolRegistry;

        public CameraDirectorSystem(World world, IComponentPool<CinemachineVirtualCamera> poolRegistry) : base(world)
        {
            this.poolRegistry = poolRegistry;
        }

        protected override void Update(float t)
        {
            // UpdateCameraDirectorQuery(World);
            SetupVirtualCameraQuery(World);

            // HandleEntityDestructionQuery(World);
            // HandleComponentRemovalQuery(World);
        }

        [Query]
        [None(typeof(VirtualCameraComponent))]
        private void SetupVirtualCamera(in Entity entity, TransformComponent transform, PBVirtualCamera pbVirtualCamera)
        {
            var virtualCameraInstance = poolRegistry.Get();
            virtualCameraInstance.transform.SetParent(transform.Transform);
            virtualCameraInstance.transform.localPosition = UnityEngine.Vector3.zero;
            virtualCameraInstance.transform.localRotation = UnityEngine.Quaternion.identity;

            // TODO: Use pbVirtualCamera values for transition speed/time...

            World.Add(entity, new VirtualCameraComponent(virtualCameraInstance));
        }

        public void FinalizeComponents(in Query query)
        {
            // throw new NotImplementedException();
        }
    }
}
