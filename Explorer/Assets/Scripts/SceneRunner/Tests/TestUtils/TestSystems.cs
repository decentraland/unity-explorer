using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using ECS.LifeCycle;

namespace SceneRunner.SceneRunner.Tests.TestUtils
{
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    public partial class InitializationTestSystem1 : BaseSystem<World, float>
    {
        public TestSystemInternal Internal;

        private InitializationTestSystem1(World world) : base(world) { }

        public override void Dispose()
        {
            Internal.Dispose();
        }

        public override void Initialize()
        {
            Internal.Initialize();
        }

        public override void BeforeUpdate(in float t)
        {
            Internal.BeforeUpdate(in t);
        }

        public override void Update(in float t)
        {
            Internal.Update(in t);
        }

        public override void AfterUpdate(in float t)
        {
            Internal.AfterUpdate(in t);
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SimulationTestSystem1 : BaseSystem<World, float>
    {
        public TestSystemInternal Internal;

        private SimulationTestSystem1(World world) : base(world) { }

        public override void Dispose()
        {
            Internal.Dispose();
        }

        public override void Initialize()
        {
            Internal.Initialize();
        }

        public override void BeforeUpdate(in float t)
        {
            Internal.BeforeUpdate(in t);
        }

        public override void Update(in float t)
        {
            Internal.Update(in t);
        }

        public override void AfterUpdate(in float t)
        {
            Internal.AfterUpdate(in t);
        }
    }

    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class FinalizeSimulationTestSystem : BaseSystem<World, float>, IFinalizeWorldSystem
    {
        public TestSystemInternal Internal { get; }

        public bool FinalizeCalled;

        private FinalizeSimulationTestSystem(World world) : base(world) { }

        public override void Dispose()
        {
            Internal.Dispose();
        }

        public override void Initialize()
        {
            Internal.Initialize();
        }

        public override void BeforeUpdate(in float t)
        {
            Internal.BeforeUpdate(in t);
        }

        public override void Update(in float t)
        {
            Internal.Update(in t);
        }

        public override void AfterUpdate(in float t)
        {
            Internal.AfterUpdate(in t);
        }

        public void FinalizeSDKComponents(in Query query)
        {
            FinalizeCalled = true;
        }
    }
}
