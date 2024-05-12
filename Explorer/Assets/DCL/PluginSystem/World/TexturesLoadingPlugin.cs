using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Textures;
using System;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;

        private readonly TexturesCache texturesCache = new ();

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner)
        {
            this.webRequestController = webRequestController;
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController);
        }

#region Interface Ambiguity
        UniTask IDCLPlugin.Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose()
        {
        }
#endregion
    }
}
