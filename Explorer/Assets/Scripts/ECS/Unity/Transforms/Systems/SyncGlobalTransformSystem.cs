using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.SDKComponents.TransformSync.Components;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    /// <summary>
    ///     This system syncs the Camera and Player transforms to specially created entities in each SDK scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [LogCategory(ReportCategory.CAMERA_TRANSFORM)]
    public partial class SyncGlobalTransformSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly ObjectProxy<World> globalWorldProxy;

        private SingleInstanceEntity cameraEntityProxy;
        private SingleInstanceEntity playerEntityProxy;
        private Entity cameraEntityMirror;

        private SyncGlobalTransformSystem(World world, ObjectProxy<World> globalWorldProxy) : base(world)
        {
            this.globalWorldProxy = globalWorldProxy;
        }

        public override void Initialize()
        {
            if (!globalWorldProxy.Configured)
                return;

            World globalWorld = globalWorldProxy.Object!;
            cameraEntityProxy = globalWorld.CacheCamera();
            playerEntityProxy = globalWorld.CachePlayer();
        }

        protected override void Update(float t)
        {
            UpdateCameraTransformQuery(World);
            UpdatePlayerTransformQuery(World);
        }

        [Query]
        [All(typeof(CameraTransformSync))]
        private void UpdateCameraTransform(ref TransformComponent transformComponent)
        {
            if (!globalWorldProxy.Configured) return;
            ref CameraComponent camera = ref globalWorldProxy.Object!.Get<CameraComponent>(cameraEntityProxy);
            Transform cameraTransform = camera.Camera.transform;
            transformComponent.SetTransform(cameraTransform.position, cameraTransform.rotation, cameraTransform.localScale);
        }

        [Query]
        [All(typeof(PlayerTransformSync))]
        private void UpdatePlayerTransform(ref TransformComponent transformComponent)
        {
            if (!globalWorldProxy.Configured) return;
            ref CharacterTransform characterTransform = ref globalWorldProxy.Object!.Get<CharacterTransform>(playerEntityProxy);
            Transform transform = characterTransform.Transform;
            transformComponent.SetTransform(transform.position, transform.rotation, transform.localScale);
        }

        public void FinalizeComponents(in Query query)
        {

        }
    }
}
