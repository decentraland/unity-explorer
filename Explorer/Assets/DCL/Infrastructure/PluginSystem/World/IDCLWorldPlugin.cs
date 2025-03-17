using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public interface IDCLWorldPlugin<in TSettings> : IDCLWorldPlugin, IDCLPlugin<TSettings> where TSettings: IDCLPluginSettings, new() { }

    /// <summary>
    ///     Encapsulation to register dependencies and systems of the particular isolated functionality.
    ///     These dependencies are not shared with other plugins
    /// </summary>
    public interface IDCLWorldPlugin : IDCLPlugin
    {
        /// <summary>
        ///     Create dependencies and systems that should exist per scene
        /// </summary>
        void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder,
            in ECSWorldInstanceSharedDependencies sharedDependencies,
            in PersistentEntities persistentEntities,
            List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners);
    }
}
