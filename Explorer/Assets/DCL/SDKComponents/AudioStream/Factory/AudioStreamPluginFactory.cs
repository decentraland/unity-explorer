using Arch.SystemGroups;
using DCL.Optimization.Pools;
using DCL.ResourcesUnloading;
using DCL.SDKComponents.AudioStream.Components;
using DCL.SDKComponents.AudioStream.Systems;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;
using SceneRunner.Scene;
using System.Collections.Generic;

#if AV_PRO_PRESENT
using RenderHeads.Media.AVProVideo;
#endif

namespace DCL.SDKComponents.AudioStream.Factory
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
