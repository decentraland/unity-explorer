using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.Abstract;
using ECS.Input.Component.Physics;

namespace ECS.Input.Systems.Physics
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class UpdateInputPhysicsTickSystem : BaseUnityLoopSystem
    {
        public UpdateInputPhysicsTickSystem(World world) : base(world)
        {
            World.Create<PhysicsTickComponent>();
        }

        protected override void Update(float t)
        {
            UpdateTickQuery(World);
        }

        [Query]
        private void UpdateTick(ref PhysicsTickComponent tickComponent)
        {
            tickComponent.Tick++;
        }
    }
}
