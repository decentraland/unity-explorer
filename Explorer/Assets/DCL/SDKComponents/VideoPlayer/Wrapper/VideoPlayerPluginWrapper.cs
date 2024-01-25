using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;

#if AV_PRO_PRESENT
using DCL.SDKComponents.AudioStream;
using DCL.SDKComponents.VideoPlayer.Systems;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.VideoPlayer.Wrapper
{
    public class VideoPlayerPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public VideoPlayerPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner)
        {
#if AV_PRO_PRESENT
            this.componentPoolsRegistry = componentPoolsRegistry;

            if (!componentPoolsRegistry.TryGetPool<MediaPlayer>(out _))
            {
                componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());
                cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
            }
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, ISceneStateProvider sceneStateProvider, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT
            IComponentPool<MediaPlayer> mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();
            VideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);
            CleanUpVideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool);
#endif
        }
    }
}
