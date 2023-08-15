using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Input.Component;
using ECS.Abstract;

namespace DCL.Input.Systems
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
