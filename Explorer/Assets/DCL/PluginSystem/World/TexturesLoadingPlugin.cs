using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem.Global;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.WebRequests;
using ECS.LifeCycle;
using ECS.StreamableLoading.Cache.Disk;
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
        private readonly ArrayPool<byte> buffersPool;
        private readonly IDiskCache<Texture2DData> diskCache;
        private readonly bool compressionEnabled;

        private readonly TexturesCache<GetTextureIntention> texturesCache = new ();

        public TexturesLoadingPlugin(IWebRequestController webRequestController, CacheCleaner cacheCleaner, ITexturesFuse texturesFuse, ArrayPool<byte> buffersPool, IDiskCache<Texture2DData> diskCache, bool compressionEnabled)
        {
            this.webRequestController = webRequestController;
            this.texturesFuse = texturesFuse;
            this.buffersPool = buffersPool;
            this.diskCache = diskCache;
            this.compressionEnabled = compressionEnabled;
            cacheCleaner.Register(texturesCache);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
            LoadTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, buffersPool, texturesFuse, diskCache, compressionEnabled);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LoadGlobalTextureSystem.InjectToWorld(ref builder, texturesCache, webRequestController, buffersPool, texturesFuse, diskCache, compressionEnabled);
        }

        UniTask IDCLPlugin<NoExposedPluginSettings>.InitializeAsync(NoExposedPluginSettings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        void IDisposable.Dispose() { }
    }
}
