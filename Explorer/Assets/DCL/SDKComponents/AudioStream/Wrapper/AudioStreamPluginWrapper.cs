using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;

#if AV_PRO_PRESENT
using ECS.ComponentsPooling.Systems;
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.AudioStream.Wrapper
{
    public class AudioStreamPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioStreamPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner)
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

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, ISceneStateProvider sceneStateProvider, List<IFinalizeWorldSystem> finalizeWorldSystems)
        {
#if AV_PRO_PRESENT
            var mediaPlayerPool = componentPoolsRegistry.GetReferenceTypePool<MediaPlayer>();

            AudioStreamSystem.InjectToWorld(ref builder, mediaPlayerPool, sceneStateProvider);
            CleanUpAudioStreamSystem.InjectToWorld(ref builder, mediaPlayerPool);
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, AudioStreamComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }
    }
}
