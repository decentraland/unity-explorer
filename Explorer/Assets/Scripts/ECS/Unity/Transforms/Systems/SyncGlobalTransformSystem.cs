using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;
using Utility;

namespace ECS.Unity.Transforms.Systems
{
    /// <summary>
    ///     This system syncs the Camera and Player transforms to specially created entities in each SDK scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    [ThrottlingEnabled]
    public partial class SyncGlobalTransformSystem : BaseUnityLoopSystem
    {
        private readonly Entity cameraEntityProxy;
        private readonly Entity playerEntityProxy;
        private readonly ExposedTransform playerTransform;
        private readonly IExposedCameraData cameraData;

        private SyncGlobalTransformSystem(World world,
            in Entity cameraEntityProxy,
            in Entity playerEntityProxy,
            ExposedTransform playerTransform,
            IExposedCameraData cameraData) : base(world)
        {
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntityProxy = playerEntityProxy;
            this.playerTransform = playerTransform;
            this.cameraData = cameraData;
        }

        protected override void Update(float t)
        {
            ref TransformComponent cameraTransform = ref World.Get<TransformComponent>(cameraEntityProxy);
            cameraTransform.SetWorldTransform(cameraData.WorldPosition.Value, cameraData.WorldRotation.Value, Vector3.one);

            ref TransformComponent playerTransformComponent = ref World.Get<TransformComponent>(playerEntityProxy);
            playerTransformComponent.SetWorldTransform(playerTransform.Position.Value, playerTransform.Rotation.Value, Vector3.one);
        }
    }
}
