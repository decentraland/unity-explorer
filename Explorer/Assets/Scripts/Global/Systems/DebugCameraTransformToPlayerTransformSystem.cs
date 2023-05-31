using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Unity.Transforms.Components;
using UnityEngine;

namespace ECS.Global.Systems
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class DebugCameraTransformToPlayerTransformSystem : BaseUnityLoopSystem
    {
        private World world;

        private Entity playerEntity;

        private Camera unityCamera;

        public DebugCameraTransformToPlayerTransformSystem(World world, Entity playerEntity, Camera unityCamera) : base(world)
        {
            this.world = world;
            this.playerEntity = playerEntity;
            this.unityCamera = unityCamera;
        }

        protected override void Update(float t)
        {
            world.Set(playerEntity, new TransformComponent()
            {
                Transform = unityCamera.transform
            });
        }
    }
}
