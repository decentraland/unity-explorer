using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using RenderHeads.Media.AVProVideo;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.SDKComponents.AudioStream.Wrapper
{
    public class AudioStreamPluginWrapper
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public AudioStreamPluginWrapper(IComponentPoolsRegistry componentPoolsRegistry, CacheCleaner cacheCleaner)
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
            AudioStreamSystem.InjectToWorld(ref builder, componentPoolsRegistry, sceneStateProvider);
            finalizeWorldSystems.Add(ReleasePoolableComponentSystem<MediaPlayer, AudioStreamComponent>.InjectToWorld(ref builder, componentPoolsRegistry));
#endif
        }

        // private static MediaPlayer HandleCreation()
        // {
        //     var poolObject = GameObjectPool<MediaPlayer>.HandleCreation();
        //     poolObject.gameObject.TryAddComponent<AudioSource>();
        //     poolObject.gameObject.TryAddComponent<AudioOutput>();
        //     return poolObject;
        // }
    }
}
