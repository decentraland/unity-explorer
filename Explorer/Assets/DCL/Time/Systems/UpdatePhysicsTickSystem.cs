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
        private readonly PhysicsTickProvider physicsTickProvider;

        public UpdatePhysicsTickSystem(World world, PhysicsTickProvider physicsTickProvider) : base(world)
        {
            this.physicsTickProvider = physicsTickProvider;
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
            physicsTickProvider.Tick++;
        }
    }
}
