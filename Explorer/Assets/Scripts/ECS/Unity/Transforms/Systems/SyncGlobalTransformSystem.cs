using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character;
using DCL.Character.Components;
using DCL.CharacterCamera;
using DCL.Utilities;
using ECS.Abstract;
using ECS.Groups;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Unity.Transforms.Systems
{
    /// <summary>
    ///     This system syncs the Camera and Player transforms to specially created entities in each SDK scene
    /// </summary>
    [UpdateInGroup(typeof(SyncedPreRenderingSystemGroup))]
    public partial class SyncGlobalTransformSystem : BaseUnityLoopSystem
    {
        private readonly ObjectProxy<World> globalWorldProxy;
        private readonly Entity cameraEntityProxy;
        private readonly Entity playerEntityProxy;
        private Transform? cameraTransform;
        private Transform? playerTransform;

        private SyncGlobalTransformSystem(World world,
            ObjectProxy<World> globalWorldProxy,
            in Entity cameraEntityProxy,
            in Entity playerEntityProxy) : base(world)
        {
            this.globalWorldProxy = globalWorldProxy;
            this.cameraEntityProxy = cameraEntityProxy;
            this.playerEntityProxy = playerEntityProxy;
        }

        public override void Initialize()
        {
            if (!globalWorldProxy.Configured)
                return;

            World globalWorld = globalWorldProxy.Object!;

            ref CameraComponent camera = ref globalWorld.Get<CameraComponent>(globalWorld.CacheCamera());
            ref CharacterTransform characterTransform = ref globalWorld.Get<CharacterTransform>(globalWorld.CachePlayer());

            cameraTransform = camera.Camera.transform;
            playerTransform = characterTransform.Transform;
        }

        protected override void Update(float t)
        {
            UpdateTransform(cameraEntityProxy, cameraTransform);
            UpdateTransform(playerEntityProxy, playerTransform);
        }

        private void UpdateTransform(Entity entityProxy, Transform? transform)
        {
            if (transform == null)
                return;

            ref TransformComponent transformComponent = ref World.TryGetRef<TransformComponent>(entityProxy, out bool exists);

            if (exists)
                transformComponent.SetWorldTransform(transform.position, transform.rotation, transform.localScale);
        }
    }
}
