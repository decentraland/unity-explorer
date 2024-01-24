using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioStream;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;

#if AV_PRO_PRESENT
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

            componentPoolsRegistry.AddGameObjectPool<MediaPlayer>(onRelease: mp => mp.CloseCurrentStream());
            cacheCleaner.Register(componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>());
#endif
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, ISceneStateProvider sceneStateProvider, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT
            var mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();

            VideoPlayerSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);
            // AudioStreamSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);

            // CleanUpAudioStreamSystem.InjectToWorld(ref builder, mediaPlayerPool);
            // finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, AudioStreamComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }
    }
}
