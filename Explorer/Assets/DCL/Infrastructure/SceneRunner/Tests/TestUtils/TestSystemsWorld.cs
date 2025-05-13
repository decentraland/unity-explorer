using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.World;
using ECS.LifeCycle;
using SceneRunner.ECSWorld;
using System.Collections.Generic;

namespace SceneRunner.Tests.TestUtils
{
    public static class TestSystemsWorld
    {
        public static ECSWorldFacade Create()
        {
            var world = World.Create();
            var builder = new ArchSystemsWorldBuilder<World>(world);

            InitializationTestSystem1.InjectToWorld(ref builder);
            SimulationTestSystem1.InjectToWorld(ref builder);
            return new ECSWorldFacade(builder.Finish(), world, new PersistentEntities(), new List<IFinalizeWorldSystem>(), new List<ISceneIsCurrentListener>());
        }
    }
}
