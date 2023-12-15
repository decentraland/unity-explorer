using Arch.SystemGroups;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Textures;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;

        private readonly TexturesCache texturesCache = new ();

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner)
        {
            this.webRequestController = webRequestController;
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, sharedDependencies.MutexSync);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
