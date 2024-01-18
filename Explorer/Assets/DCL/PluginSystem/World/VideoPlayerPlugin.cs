using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.PluginSystem.World.Dependencies;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.VideoPlayer.Systems;
using ECS.LifeCycle;
using RenderHeads.Media.AVProVideo;
using System.Collections.Generic;

namespace DCL.PluginSystem.World
{
    public class VideoPlayerPlugin : IDCLWorldPluginWithoutSettings
    {
        private readonly IComponentPool<MediaPlayer> mediaPlayerPool;

        public VideoPlayerPlugin(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner)
        {
            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: OnRelease);

            mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();
            cacheCleaner.Register(mediaPlayerPool);

            void OnRelease(MediaPlayer mp)
            {
                // mp.CloseCurrentStream();
            }
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
            VideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool);
        }

        public void InjectToEmptySceneWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in EmptyScenesWorldSharedDependencies dependencies) { }
    }
}
