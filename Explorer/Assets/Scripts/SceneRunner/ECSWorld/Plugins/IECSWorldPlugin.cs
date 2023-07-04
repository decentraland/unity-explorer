using Arch.Core;
using Arch.SystemGroups;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace SceneRunner.ECSWorld.Plugins
{
    /// <summary>
    ///     Encapsulation to register dependencies and systems of the particular isolated functionality.
    ///     These dependencies are not shared with other plugins
    /// </summary>
    public interface IECSWorldPlugin
    {
        /// <summary>
        ///     Create dependencies and systems that should exist per scene
        /// </summary>
        void InjectToWorld(
            ref ArchSystemsWorldBuilder<World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems);
    }
}
