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
        private Entity playerEntity;

        private Camera unityCamera;

        public DebugCameraTransformToPlayerTransformSystem(World world, Entity playerEntity, Camera unityCamera) : base(world)
        {
            this.playerEntity = playerEntity;
            this.unityCamera = unityCamera;
        }

        public override void Initialize()
        {
            World.Set(playerEntity, new TransformComponent()
            {
                Transform = unityCamera.transform,
            });
        }

        protected override void Update(float t)
        {
            World.Get<TransformComponent>(playerEntity).Transform = unityCamera.transform;
        }
    }
}
