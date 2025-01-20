using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Textures;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;

namespace DCL.PluginSystem.World
{
    public class TexturesLoadingPlugin : IDCLWorldPluginWithoutSettings, IDCLGlobalPluginWithoutSettings
    {
        private readonly IWebRequestController webRequestController;
        private readonly ITexturesFuse texturesFuse;

        private readonly TexturesCache<GetTextureIntention> texturesCache = new ();
        private readonly ArrayPool<byte> buffersPool = ArrayPool<byte>.Create(1024 * 1024 * 50, 50);

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, ITexturesFuse texturesFuse)
        {
            this.webRequestController = webRequestController;
            this.texturesFuse = texturesFuse;
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, buffersPool, texturesFuse);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, buffersPool, texturesFuse);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
