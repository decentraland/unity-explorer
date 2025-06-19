using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Time.Components;
using ECS.Abstract;

namespace DCL.Time.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class UpdatePhysicsTickSystem : BaseUnityLoopSystem
    {
        public UpdatePhysicsTickSystem(World world) : base(world)
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
            PhysicsTickProvider.Tick++;
        }
    }
}
