using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.ECSWorld;
using SceneRunner.Tests.TestUtils;

namespace SceneRunner.Tests
{

    public class ECSWorldFacadeShould
    {

        public void SetUp()
        {
            world = World.Create();

            var builder = new ArchSystemsWorldBuilder<World>(world);

            initializationTestSystem1 = InitializationTestSystem1.InjectToWorld(ref builder);
            simulationTestSystem1 = SimulationTestSystem1.InjectToWorld(ref builder);

            ecsWorldFacade = new ECSWorldFacade( builder.Finish(), world, new[] { finalizeWorldSystem = Substitute.For<IFinalizeWorldSystem>() });
        }

        private ECSWorldFacade ecsWorldFacade;
        private World world;

        private InitializationTestSystem1 initializationTestSystem1;
        private SimulationTestSystem1 simulationTestSystem1;
        private IFinalizeWorldSystem finalizeWorldSystem;


        public void CallInitializeOnSystems()
        {
            ecsWorldFacade.Initialize();

            try
            {
                Assert.IsTrue(initializationTestSystem1.Internal.InitializeCalled);
                Assert.IsTrue(simulationTestSystem1.Internal.InitializeCalled);
            }
            finally { world.Dispose(); }
        }


        public void DisposeProperly()
        {
            ecsWorldFacade.Dispose();

            finalizeWorldSystem.Received(1).FinalizeComponents(Arg.Any<Query>());

            Assert.IsTrue(initializationTestSystem1.Internal.DisposeCalled);
            Assert.IsTrue(simulationTestSystem1.Internal.DisposeCalled);

            // In PURE_ECS the world is not removed from the list
            //Assert.IsFalse(World.Worlds.Contains(world));
        }
    }
}
